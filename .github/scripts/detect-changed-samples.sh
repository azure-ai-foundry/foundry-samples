#!/usr/bin/env bash
# detect-changed-samples.sh — reusable in-job change detector for the public-first
# validation pipeline. Ports the private ADO `DetectChanges` stage (job AnalyzeChanges,
# step detectChanges) from .azure-pipelines/validation.yml into a GitHub-Actions-native,
# dependency-light script (git + coreutils only).
#
# Single responsibility: answer "which sample directories were affected by this push/PR?"
# by mapping every changed file under <samples-root>/ to its NEAREST ANCESTOR sample.yaml
# directory, then deduping. The caller (P2.1/P2.2 validate lanes) consumes the result as
# GitHub Actions job outputs via needs.<detect>.outputs.* and fans out — deriving each
# sample's --language from its path (samples/<language>/...); this script deliberately does
# NOT pre-group by language (see "Design" below).
#
# Usage:
#   detect-changed-samples.sh [--base-ref <ref>] [--event <pull_request|push|dispatch>]
#                             [--target-branch <branch>] [--samples-root samples]
#                             [--output-dir <dir>] [--print-base-ref]
#
# Base-ref resolution (faithful to ADO DetectChanges):
#   --base-ref <ref>     wins outright (used by tests + callers that already know the base).
#   event pull_request   git fetch origin <target> (best-effort) then BASE_REF=origin/<target>,
#                        where <target> is --target-branch or $GITHUB_BASE_REF.
#   otherwise (push/etc) BASE_REF=HEAD~1.
#
# Outputs (the contract P2 consumes):
#   has_changes  "true" | "false"   — false == docs-only / no sample touched (short-circuit flag)
#   count        integer            — number of affected sample dirs
#   samples      JSON array         — deduped, sorted affected sample dirs (repo-relative)
# Written to $GITHUB_OUTPUT when set; always printed to stdout. When --output-dir is given,
# also writes changed_files.txt + unique_changed_samples.txt (mirrors ADO's artifacts).
#
# Exit codes (2-way — intentionally simpler than validate-sample.sh's 0/1/2 classifier;
# detection has no "sample broken" tier):
#   0  OK      detection completed — INCLUDING an empty (docs-only) result.
#   1  ERROR   fail loud: bad args, not a git repo, or the git diff itself errored.
#
# FAIL-LOUD invariant (ADO 5247751 lesson): a git diff *error* must NEVER be swallowed into
# an empty "nothing changed" result — that fail-opens the sync gate. A diff that SUCCEEDS
# with zero lines is legitimate docs-only (has_changes=false, exit 0). The two are never
# conflated: only `git diff` returning non-zero triggers the exit-1 refusal below.
#
# NOTE: `set -e` is intentionally NOT used; error paths are routed through error() so no
# external command can abort the script with an unclassified status.
set -uo pipefail

BASE_REF=""
EVENT=""
TARGET_BRANCH=""
SAMPLES_ROOT="samples"
OUTPUT_DIR=""
PRINT_BASE_REF=false

usage() {
    cat <<'EOF'
Usage: detect-changed-samples.sh [options]

  --base-ref <ref>        Explicit base ref to diff HEAD against (overrides resolution).
  --event <name>          Event kind: pull_request | push | dispatch (default: $GITHUB_EVENT_NAME or push).
  --target-branch <br>    PR target branch (default: $GITHUB_BASE_REF). Used only for pull_request.
  --samples-root <dir>    Root under which samples live (default: samples).
  --output-dir <dir>      If set, write changed_files.txt + unique_changed_samples.txt here.
  --print-base-ref        Resolve and print the base ref, then exit 0 (no fetch, no diff).

Exit codes: 0 = detection ok (incl. empty/docs-only), 1 = error (fail loud).
EOF
}

# error <reason>: fail loud — print to stderr and exit 1. Never emit a fake empty result.
error() {
    echo "ERROR: ${1:-detection failure}" >&2
    exit 1
}

# --- Argument parsing --------------------------------------------------------
while [ $# -gt 0 ]; do
    case "$1" in
        --base-ref)      BASE_REF="${2:-}";      shift $(( $# > 1 ? 2 : 1 )) ;;
        --event)         EVENT="${2:-}";         shift $(( $# > 1 ? 2 : 1 )) ;;
        --target-branch) TARGET_BRANCH="${2:-}"; shift $(( $# > 1 ? 2 : 1 )) ;;
        --samples-root)  SAMPLES_ROOT="${2:-}";  shift $(( $# > 1 ? 2 : 1 )) ;;
        --output-dir)    OUTPUT_DIR="${2:-}";    shift $(( $# > 1 ? 2 : 1 )) ;;
        --print-base-ref) PRINT_BASE_REF=true;   shift ;;
        -h|--help)       usage; exit 0 ;;
        *)               usage >&2; error "unknown argument: $1" ;;
    esac
done

# --- Guards ------------------------------------------------------------------
git rev-parse --git-dir >/dev/null 2>&1 || error "not a git repository (cwd=$(pwd))"
[ -n "$SAMPLES_ROOT" ] || error "missing/empty --samples-root"
SAMPLES_ROOT="${SAMPLES_ROOT%/}"

# --- Base-ref resolution -----------------------------------------------------
# Known limitations (faithful port of the frozen ADO DetectChanges design; see
# the P1.2 close-out for the risk analysis). These are documented, accepted
# characteristics — not bugs — but the next engineer should know the sharp edges:
#
#   1. PR diff is TWO-DOT (`diff origin/<target> HEAD`), not merge-base
#      (three-dot). If <target> advances after the PR diverged, the diff can
#      surface files the PR never touched -> extra samples validated. This is a
#      FALSE POSITIVE (wasteful, never fail-open) and is usually neutralized by
#      GitHub's synthetic PR merge-commit checkout (refs/pull/N/merge).
#
#   2. Non-PR (push/dispatch) base is `HEAD~1` -> only the FINAL commit
#      transition. A direct multi-commit push can miss samples changed in
#      earlier commits (the event's `before` SHA is ignored). Merge/squash
#      commits are fine (HEAD~1 first-parent captures the whole delta). This is
#      the one direction that could FALSE-NEGATIVE on the push path.
#
#   3. The fetch below is BEST-EFFORT (`|| true`). If a fresh fetch fails but a
#      stale `origin/<target>` already exists, the diff runs against the stale
#      ref -> exit 0 with a wrong answer. Only fails loud when NO usable ref
#      remains. Low-probability on a fresh runner (checkout populates the ref
#      seconds earlier) but in tension with the fail-loud principle (ADO 5247751).
#
# Tracked refinement (base-ref robustness): use github.event.before on push and
# stop swallowing fetch failure. Do NOT change the frozen logic here ad hoc.
resolve_base_ref() {
    if [ -n "$BASE_REF" ]; then
        echo "$BASE_REF"
        return 0
    fi
    local ev="$EVENT"
    [ -n "$ev" ] || ev="${GITHUB_EVENT_NAME:-push}"
    if [ "$ev" = "pull_request" ]; then
        local target="$TARGET_BRANCH"
        [ -n "$target" ] || target="${GITHUB_BASE_REF:-}"
        [ -n "$target" ] || error "pull_request event but no target branch (--target-branch / \$GITHUB_BASE_REF)"
        # Best-effort fetch (ADO parity). If offline/absent, the diff below fails loud.
        if [ "$PRINT_BASE_REF" != true ]; then
            git fetch origin "$target" --quiet 2>/dev/null || true
        fi
        echo "origin/$target"
    else
        echo "HEAD~1"
    fi
}

BASE_REF="$(resolve_base_ref)" || exit 1

if [ "$PRINT_BASE_REF" = true ]; then
    echo "$BASE_REF"
    exit 0
fi

# --- Diff (FAIL LOUD on error; empty-but-successful is legit docs-only) -------
CHANGED_FILES="$(mktemp)"
trap 'rm -f "$CHANGED_FILES"' EXIT
# core.quotepath=false keeps non-ASCII paths literal (no octal escaping) so dirname works.
if ! git -c core.quotepath=false diff --name-only "$BASE_REF" HEAD -- "$SAMPLES_ROOT/" > "$CHANGED_FILES"; then
    error "git diff '$BASE_REF'..HEAD failed; refusing to continue with an empty change set (would fail-open the gate)"
fi

# --- Walk each changed file UP to its nearest ancestor sample.yaml dir --------
UNIQUE="$(mktemp)"
trap 'rm -f "$CHANGED_FILES" "$UNIQUE"' EXIT
{
    while IFS= read -r f; do
        [ -z "$f" ] && continue
        dir="$(dirname "$f")"
        while [ "$dir" != "$SAMPLES_ROOT" ] && [ "$dir" != "." ] && [ "$dir" != "/" ]; do
            if [ -f "$dir/sample.yaml" ]; then
                echo "$dir"
                break
            fi
            dir="$(dirname "$dir")"
        done
    done < "$CHANGED_FILES"
} | sort -u > "$UNIQUE"

# --- Compute outputs ---------------------------------------------------------
# `grep -c . file` prints 0 AND exits 1 on an empty file; a `|| echo 0` fallback
# then double-prints, making COUNT="0\n0". That later leaks a bare "0" line into
# $GITHUB_OUTPUT, which GitHub Actions rejects ("Invalid format '0'") — crashing
# the detect step on any docs-only PR. Guard on file-non-empty so COUNT is always
# a single clean integer.
if [ -s "$UNIQUE" ]; then
    COUNT="$(grep -c . "$UNIQUE")"
else
    COUNT=0
fi
if [ "$COUNT" -gt 0 ]; then HAS_CHANGES="true"; else HAS_CHANGES="false"; fi

# Build a JSON array (dependency-light; escape backslash and double-quote).
json_escape() { printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'; }
SAMPLES_JSON="["
first=true
while IFS= read -r line; do
    [ -z "$line" ] && continue
    if [ "$first" = true ]; then first=false; else SAMPLES_JSON="$SAMPLES_JSON,"; fi
    SAMPLES_JSON="$SAMPLES_JSON\"$(json_escape "$line")\""
done < "$UNIQUE"
SAMPLES_JSON="$SAMPLES_JSON]"

# --- Emit --------------------------------------------------------------------
echo "=== detect-changed-samples: base=$BASE_REF root=$SAMPLES_ROOT ==="
echo "has_changes=$HAS_CHANGES"
echo "count=$COUNT"
echo "samples=$SAMPLES_JSON"
if [ "$COUNT" -gt 0 ]; then
    echo "affected sample dirs:"
    sed 's/^/  - /' "$UNIQUE"
else
    echo "no affected samples (docs-only / nothing under a sample.yaml) — short-circuit"
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
    {
        echo "has_changes=$HAS_CHANGES"
        echo "count=$COUNT"
        echo "samples=$SAMPLES_JSON"
    } >> "$GITHUB_OUTPUT"
fi

if [ -n "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR" || error "could not create --output-dir: $OUTPUT_DIR"
    cp "$CHANGED_FILES" "$OUTPUT_DIR/changed_files.txt"
    cp "$UNIQUE" "$OUTPUT_DIR/unique_changed_samples.txt"
fi

exit 0
