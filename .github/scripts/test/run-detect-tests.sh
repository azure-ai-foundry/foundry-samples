#!/usr/bin/env bash
# run-detect-tests.sh — hermetic exit gate for detect-changed-samples.sh.
#
# Sibling to run-tests.sh (the P1.1 validator harness). Where that harness needs real
# toolchains, THIS one needs only git + coreutils, so it runs fully GREEN both locally
# and on a runner — no BLOCKED-on-env cases. It builds a throwaway git repo with a
# crafted samples/ tree, makes controlled commits, and asserts the detector's outputs
# over specific base..HEAD ranges.
#
# Proves the three acceptance criteria of ADO 5449686 plus the fail-loud invariant:
#   (a) a changed file inside a sample resolves to that sample's nearest sample.yaml dir
#   (b) a docs-only change yields has_changes=false / samples=[] / exit 0  (short-circuit)
#   (c) multiple changed files in one sample dir dedupe to a single entry
#   (d) a deeply-nested changed file resolves to its nearest ancestor sample.yaml (walk-up)
#   (e) two different samples changed -> both present, sorted
#   (f) FAIL LOUD: a nonexistent base ref makes git diff error -> exit 1 (ADO 5247751)
#   (g) base-ref RESOLUTION: pull_request -> origin/<target>, push -> HEAD~1 (--print-base-ref)
#   (h) GITHUB_OUTPUT hygiene: an empty (docs-only) result writes clean count=0 /
#       has_changes=false / samples=[] with no bare-'0' line (grep -c regression guard)
# Also asserts --output-dir writes changed_files.txt + unique_changed_samples.txt.
set -uo pipefail

# The unit harness asserts on the detector's STDOUT only. It must NOT leak into the
# runner's real $GITHUB_OUTPUT — otherwise the ~10 detector invocations below append
# has_changes/count/samples lines to the step's output file and GitHub rejects it
# ("Unable to process file command 'output'"). The GITHUB_OUTPUT emission is proven
# separately by the detect->consume plumbing job in the selftest workflow.
unset GITHUB_OUTPUT GITHUB_ENV GITHUB_STATE 2>/dev/null || true

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/../detect-changed-samples.sh"

PASS_N=0
FAIL_N=0

pass() { echo "  PASS  $1"; PASS_N=$((PASS_N + 1)); }
fail() { echo "  FAIL  $1"; FAIL_N=$((FAIL_N + 1)); }

# run_detect <base-ref> [extra args...] -> populates OUT (stdout+stderr) and RC.
run_detect() {
    local base="$1"; shift
    OUT="$(bash "$SCRIPT" --base-ref "$base" "$@" 2>&1)"
    RC=$?
}

# field <key> — extract the last `key=value` line the script printed to stdout.
field() { printf '%s\n' "$OUT" | sed -n "s/^$1=\(.*\)$/\1/p" | tail -1; }

# expect_out <desc> <expected-value> — assert the `samples=` JSON equals expected.
expect_samples() {
    local desc="$1" want="$2" got
    got="$(field samples)"
    if [ "$got" = "$want" ]; then pass "$desc  (samples=$got)"; else fail "$desc  expected samples=$want got=${got:-<none>}"; fi
}
expect_field() {
    local desc="$1" key="$2" want="$3" got
    got="$(field "$key")"
    if [ "$got" = "$want" ]; then pass "$desc  ($key=$got)"; else fail "$desc  expected $key=$want got=${got:-<none>}"; fi
}
expect_rc() {
    local desc="$1" want="$2"
    if [ "$RC" = "$want" ]; then pass "$desc  (exit=$RC)"; else fail "$desc  expected exit=$want got=$RC"; fi
}

# --- Build a throwaway git repo with a crafted samples/ tree -----------------
REPO="$(mktemp -d)"
trap 'rm -rf "$REPO"' EXIT
cd "$REPO" || { echo "cannot cd to temp repo"; exit 1; }

git init -q
git config user.email t@t.test
git config user.name  test
git config commit.gpgsign false

mkdir -p samples/python/quickstart/foo
mkdir -p samples/csharp/quickstart/bar
mkdir -p samples/python/deep/a/b/c/src
mkdir -p samples/docs                      # docs area under samples/, NO sample.yaml
printf 'name: foo\n'  > samples/python/quickstart/foo/sample.yaml
printf 'print("v1")\n' > samples/python/quickstart/foo/main.py
printf 'name: bar\n'  > samples/csharp/quickstart/bar/sample.yaml
printf 'x\n'          > samples/csharp/quickstart/bar/Program.cs
printf 'y\n'          > samples/csharp/quickstart/bar/extra.cs
printf 'name: deep\n' > samples/python/deep/a/b/c/sample.yaml
printf 'deep\n'       > samples/python/deep/a/b/c/src/x.py
printf '# top\n'      > samples/docs/README.md
printf '# repo\n'     > README.md            # root doc, entirely outside samples/
git add -A >/dev/null
git commit -qm "base"
BASE="$(git rev-parse HEAD)"

echo "=============================================================="
echo " detect-changed-samples: acceptance cases (git + coreutils only)"
echo "=============================================================="

# (a) change a file inside foo -> resolves to foo
printf 'print("v2")\n' > samples/python/quickstart/foo/main.py
git commit -qam "touch foo/main.py"
run_detect "$BASE"
expect_rc      "(a) changed sample -> ok"                 0
expect_field   "(a) has_changes true"        has_changes  true
expect_field   "(a) count 1"                 count        1
expect_samples "(a) resolves to foo"         '["samples/python/quickstart/foo"]'
BASE="$(git rev-parse HEAD)"

# (b) docs-only: change a file under samples/ with no sample.yaml ancestor + a root doc
printf '# top v2\n' > samples/docs/README.md
printf '# repo v2\n' > README.md
git commit -qam "docs only"
run_detect "$BASE"
expect_rc      "(b) docs-only -> ok"                      0
expect_field   "(b) has_changes false"      has_changes   false
expect_field   "(b) count 0"                count         0
expect_samples "(b) empty set"              '[]'
BASE="$(git rev-parse HEAD)"

# (c) multiple files in ONE sample dir -> dedupe to a single entry
printf 'x2\n' > samples/csharp/quickstart/bar/Program.cs
printf 'y2\n' > samples/csharp/quickstart/bar/extra.cs
git commit -qam "two files in bar"
run_detect "$BASE"
expect_rc      "(c) multi-file one dir -> ok"             0
expect_field   "(c) count 1 (deduped)"      count         1
expect_samples "(c) single bar entry"       '["samples/csharp/quickstart/bar"]'
BASE="$(git rev-parse HEAD)"

# (d) deeply-nested changed file -> nearest ancestor sample.yaml (walk-up)
printf 'deep v2\n' > samples/python/deep/a/b/c/src/x.py
git commit -qam "nested deep change"
run_detect "$BASE"
expect_rc      "(d) nested -> ok"                         0
expect_samples "(d) walks up to c"          '["samples/python/deep/a/b/c"]'
BASE="$(git rev-parse HEAD)"

# (e) two different samples changed -> both present, sorted (csharp < python)
printf 'print("v3")\n' > samples/python/quickstart/foo/main.py
printf 'x3\n'          > samples/csharp/quickstart/bar/Program.cs
git commit -qam "two samples"
run_detect "$BASE"
expect_rc      "(e) two samples -> ok"                    0
expect_field   "(e) count 2"                count         2
expect_samples "(e) both, sorted"           '["samples/csharp/quickstart/bar","samples/python/quickstart/foo"]'
BASE="$(git rev-parse HEAD)"

echo ""
echo "=============================================================="
echo " FAIL-LOUD invariant (ADO 5247751)"
echo "=============================================================="
# (f) nonexistent base ref -> git diff errors -> exit 1, and NOT a fake empty set
OUT="$(bash "$SCRIPT" --base-ref does-not-exist-ref 2>&1)"; RC=$?
expect_rc "(f) bad base ref -> fail loud" 1
if printf '%s\n' "$OUT" | grep -q "refusing to continue"; then
    pass "(f) prints refusal (no fake empty set)"
else
    fail "(f) missing refusal message"
fi

echo ""
echo "=============================================================="
echo " Base-ref RESOLUTION (--print-base-ref, no network)"
echo "=============================================================="
# (g) pull_request event -> origin/<target>; push -> HEAD~1
G="$(bash "$SCRIPT" --event pull_request --target-branch main --print-base-ref 2>&1)"; GRC=$?
if [ "$GRC" = 0 ] && [ "$G" = "origin/main" ]; then pass "(g) pull_request -> origin/main"; else fail "(g) pull_request expected origin/main got '$G' (rc=$GRC)"; fi
G="$(bash "$SCRIPT" --event push --print-base-ref 2>&1)"; GRC=$?
if [ "$GRC" = 0 ] && [ "$G" = "HEAD~1" ]; then pass "(g) push -> HEAD~1"; else fail "(g) push expected HEAD~1 got '$G' (rc=$GRC)"; fi
# env fallback: GITHUB_BASE_REF supplies the target when --target-branch is absent
G="$(GITHUB_EVENT_NAME=pull_request GITHUB_BASE_REF=release bash "$SCRIPT" --print-base-ref 2>&1)"; GRC=$?
if [ "$GRC" = 0 ] && [ "$G" = "origin/release" ]; then pass "(g) env pull_request -> origin/release"; else fail "(g) env pull_request expected origin/release got '$G' (rc=$GRC)"; fi

echo ""
echo "=============================================================="
echo " --output-dir artifacts"
echo "=============================================================="
# The last real diff (e) had 2 samples; re-run it with --output-dir and assert files.
ART="$(mktemp -d)"
bash "$SCRIPT" --base-ref "$(git rev-parse HEAD~1)" --output-dir "$ART" >/dev/null 2>&1
if [ -f "$ART/unique_changed_samples.txt" ] && [ -f "$ART/changed_files.txt" ]; then
    if grep -q "samples/python/quickstart/foo" "$ART/unique_changed_samples.txt"; then
        pass "--output-dir wrote unique_changed_samples.txt"
    else
        fail "--output-dir unique_changed_samples.txt missing expected entry"
    fi
else
    fail "--output-dir did not write expected artifact files"
fi
rm -rf "$ART"

echo ""
echo "=============================================================="
echo " GITHUB_OUTPUT hygiene on EMPTY result (regression: bare '0')"
echo "=============================================================="
# An empty (docs-only) result must write ONLY clean key=value lines to the real
# $GITHUB_OUTPUT. Regression guard for the grep -c double-'0' bug that emitted a
# bare "0" line and made GitHub reject the step ("Invalid format '0'").
# Diffing HEAD against itself is a guaranteed empty-but-successful diff.
GO="$(mktemp)"
GITHUB_OUTPUT="$GO" bash "$SCRIPT" --base-ref HEAD >/dev/null 2>&1
if grep -qxE '[0-9]+' "$GO"; then
    fail "(h) empty result leaked a bare numeric line into GITHUB_OUTPUT: $(tr '\n' '|' < "$GO")"
elif [ "$(grep -c '^count=0$' "$GO")" = 1 ] \
     && grep -q '^has_changes=false$' "$GO" \
     && grep -q '^samples=\[\]$' "$GO"; then
    pass "(h) empty result writes clean count=0 / has_changes=false / samples=[]"
else
    fail "(h) empty-result GITHUB_OUTPUT malformed: $(tr '\n' '|' < "$GO")"
fi
rm -f "$GO"

echo ""
echo "==================================================="
echo "  checks passed: $PASS_N   failed: $FAIL_N"
echo "==================================================="

if [ "$FAIL_N" -ne 0 ]; then
    echo "detect exit gate: RED — a check failed."
    exit 1
fi
echo "detect exit gate: GREEN — all detection acceptance cases + fail-loud proved."
