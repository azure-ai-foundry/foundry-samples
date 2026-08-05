#!/usr/bin/env bash
# Structural regression gate for the single required `trusted` job.
#
# Root cause pinned here: a job-level `if:` skip concludes `skipped`, and GitHub treats a
# skipped required check as satisfied. The job must run and fail forks explicitly after L3.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKFLOW_SOURCE="$HERE/../../workflows/validate.yml"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

PASS_N=0
FAIL_N=0

pass() {
    echo "  PASS  $1"
    PASS_N=$((PASS_N + 1))
}

fail() {
    echo "  FAIL  $1"
    FAIL_N=$((FAIL_N + 1))
}

expect_count() {
    local desc="$1" pattern="$2" expected="$3" file="${4:-$WORKFLOW}" count
    count="$(grep -cE "$pattern" "$file" 2>/dev/null || true)"
    if [ "$count" = "$expected" ]; then
        pass "$desc (count=$count)"
    else
        fail "$desc expected count=$expected got=$count"
    fi
}

expect_has() {
    local desc="$1" pattern="$2" file="${3:-$WORKFLOW}"
    if grep -qE "$pattern" "$file"; then
        pass "$desc"
    else
        fail "$desc"
    fi
}

expect_not_has() {
    local desc="$1" pattern="$2" file="${3:-$WORKFLOW}"
    if grep -qE "$pattern" "$file"; then
        fail "$desc"
    else
        pass "$desc"
    fi
}

extract_step() {
    local name="$1" output="$2"
    awk -v wanted="$name" '
        $0 == "      - name: " wanted { in_step=1 }
        in_step && seen && /^      - (name:|uses:)/ { exit }
        in_step { print; seen=1 }
    ' "$TRUSTED" > "$output"
}

if [ ! -f "$WORKFLOW_SOURCE" ]; then
    echo "FAIL: workflow not found: $WORKFLOW_SOURCE"
    exit 1
fi

# Keep the hermetic structure assertions stable in Windows worktrees where Git may check out
# YAML with CRLF. The committed workflow remains untouched.
WORKFLOW="$TMP/validate.yml"
tr -d '\r' < "$WORKFLOW_SOURCE" > "$WORKFLOW"

# Extract the trusted job by top-level job indentation. It is currently the final job, but this
# stops correctly if a later top-level job is added.
awk '
    /^  trusted:$/ { in_trusted=1 }
    in_trusted && seen && /^  [A-Za-z0-9_-]+:$/ { exit }
    in_trusted { print; seen=1 }
' "$WORKFLOW" > "$TMP/trusted.yml"
TRUSTED="$TMP/trusted.yml"

steps_line="$(grep -n '^    steps:$' "$TRUSTED" | cut -d: -f1 | head -1)"
if [ -z "$steps_line" ]; then
    echo "FAIL: trusted job has no steps block"
    exit 1
fi
head -n "$((steps_line - 1))" "$TRUSTED" > "$TMP/trusted-job.yml"

echo "=============================================================="
echo " validate.yml required trusted-gate structure"
echo "=============================================================="

expect_count "workflow name remains validate" '^name: validate$' 1
expect_count "pull_request trigger remains present" '^  pull_request:$' 1
expect_not_has "pull_request_target is absent" '^[[:space:]]*pull_request_target:'
expect_count "exactly one trusted job exists" '^  trusted:$' 1
expect_count "trusted has exactly one job-level condition" '^    if:' 1 "$TMP/trusted-job.yml"
expect_count "trusted job runs after dependency failure unless cancelled" '^    if: \$\{\{ !cancelled\(\) \}\}$' 1 "$TMP/trusted-job.yml"
expect_not_has "trusted job has no matrix" '^    (strategy:|matrix:)' "$TRUSTED"
expect_has "trusted job keeps L4-validation environment" '^    environment: L4-validation$' "$TRUSTED"
expect_not_has "job-level fork skip is absent" 'head\.repo\.fork != true' "$TMP/trusted-job.yml"
expect_not_has "job-level draft skip is absent" 'pull_request\.draft' "$TMP/trusted-job.yml"

extract_step "Validate sample detection contract" "$TMP/detect-contract.yml"
first_step="$(tail -n "+$((steps_line + 1))" "$TRUSTED" | grep -m1 -E '^      - (name:|uses:)')"
if [ "$first_step" = "      - name: Validate sample detection contract" ]; then
    pass "detection contract is the first trusted step"
else
    fail "detection contract must be the first trusted step (got: ${first_step:-<none>})"
fi
contract_line="$(grep -nF -- '- name: Validate sample detection contract' "$TRUSTED" | cut -d: -f1)"
checkout_line="$(grep -nF -- '- uses: actions/checkout@v4' "$TRUSTED" | cut -d: -f1 | head -1)"
if [ -n "$contract_line" ] && [ -n "$checkout_line" ] && [ "$contract_line" -lt "$checkout_line" ]; then
    pass "detection contract runs before checkout"
else
    fail "detection contract must run before checkout"
fi
expect_has "contract reads needs.detect.result" 'DETECT_RESULT: \$\{\{ needs\.detect\.result \}\}' "$TMP/detect-contract.yml"
expect_has "contract reads has_changes output" 'HAS_CHANGES: \$\{\{ needs\.detect\.outputs\.has_changes \}\}' "$TMP/detect-contract.yml"
expect_has "contract reads count output" 'COUNT: \$\{\{ needs\.detect\.outputs\.count \}\}' "$TMP/detect-contract.yml"
expect_has "contract reads samples output" 'SAMPLES: \$\{\{ needs\.detect\.outputs\.samples \}\}' "$TMP/detect-contract.yml"
expect_has "contract requires successful dependency" 'DETECT_RESULT.*!=.*success' "$TMP/detect-contract.yml"
expect_has "contract restricts has_changes domain" 'true\|false' "$TMP/detect-contract.yml"
expect_has "contract parses samples as JSON array" 'type == "array"' "$TMP/detect-contract.yml"
expect_has "contract validates sample path shape" 'startswith\("samples/"\)' "$TMP/detect-contract.yml"
expect_has "contract fails non-zero" '^[[:space:]]*exit 1$' "$TMP/detect-contract.yml"
expect_not_has "contract cannot ignore failures" 'continue-on-error:' "$TMP/detect-contract.yml"

# Execute the inline contract hermetically. Static assertions prove its position and Actions
# wiring; these cases prove the shell logic rejects dependency/output corruption.
awk '
    /^        run: \|$/ { in_run=1; next }
    in_run {
        sub(/^          /, "")
        print
    }
' "$TMP/detect-contract.yml" > "$TMP/detect-contract.sh"

run_contract_case() {
    local desc="$1" expected="$2" result="$3" has_changes="$4" count="$5" samples="$6"
    local output="$TMP/contract-output.txt" rc
    if DETECT_RESULT="$result" HAS_CHANGES="$has_changes" COUNT="$count" SAMPLES="$samples" \
        bash "$TMP/detect-contract.sh" > "$output" 2>&1; then
        rc=0
    else
        rc=$?
    fi
    if [ "$rc" = "$expected" ]; then
        pass "$desc (exit=$rc)"
    else
        fail "$desc expected exit=$expected got=$rc: $(tr '\n' '|' < "$output")"
    fi
}

run_contract_case "valid changed-sample contract passes" 0 success true 1 '["samples/python/foo"]'
run_contract_case "valid docs-only contract passes" 0 success false 0 '[]'
run_contract_case "failed detect result fails closed" 1 failure false 0 '[]'
run_contract_case "malformed samples JSON fails closed" 1 success true 1 '{'
run_contract_case "non-array samples fails closed" 1 success true 1 '{"sample":"samples/python/foo"}'
run_contract_case "true with empty samples fails closed" 1 success true 0 '[]'
run_contract_case "false with samples fails closed" 1 success false 1 '["samples/python/foo"]'
run_contract_case "count mismatch fails closed" 1 success true 2 '["samples/python/foo"]'

extract_step "Short-circuit docs-only PRs before secrets" "$TMP/docs-only.yml"
expect_has "docs-only success is same-repo guarded" 'head\.repo\.fork != true' "$TMP/docs-only.yml"

extract_step "Validate changed samples to L3 (in-job parallel)" "$TMP/l3.yml"
extract_step "Reject fork until maintainer promotion" "$TMP/fork-reject.yml"
expect_has "fork rejection is fork-only" 'head\.repo\.fork == true' "$TMP/fork-reject.yml"
expect_has "fork rejection runs after an L3 failure" '!cancelled\(\)' "$TMP/fork-reject.yml"
expect_has "fork rejection checks OIDC request surface" 'ACTIONS_ID_TOKEN_REQUEST_(URL|TOKEN)' "$TMP/fork-reject.yml"
expect_has "fork rejection emits promotion error" '::error::.*promot' "$TMP/fork-reject.yml"
expect_has "fork rejection exits non-zero" '^[[:space:]]*exit 1$' "$TMP/fork-reject.yml"

l3_line="$(grep -nF -- '- name: Validate changed samples to L3 (in-job parallel)' "$TRUSTED" | cut -d: -f1)"
fork_line="$(grep -nF -- '- name: Reject fork until maintainer promotion' "$TRUSTED" | cut -d: -f1)"
if [ -n "$l3_line" ] && [ -n "$fork_line" ] && [ "$l3_line" -lt "$fork_line" ]; then
    pass "credential-free L3 runs before fork rejection"
else
    fail "credential-free L3 must run before fork rejection"
fi

# Any step containing a current credential/L4 marker must carry an explicit same-repo guard.
if awk '
    function flush() {
        if (active && credentialed && !same_repo) {
            print "unguarded credentialed step: " step
            bad=1
        }
    }
    /^      - (name:|uses:)/ {
        flush()
        active=1
        step=$0
        credentialed=0
        same_repo=0
    }
    active && /azure\/login|vars\.AZURE_|az account|get-access-token|L4 smoke/ {
        credentialed=1
    }
    active && /head\.repo\.fork != true/ {
        same_repo=1
    }
    END {
        flush()
        exit bad
    }
' "$TRUSTED"; then
    pass "every credentialed/L4 step has a same-repo guard"
else
    fail "every credentialed/L4 step must have a same-repo guard"
fi

extract_step "Trusted verdict" "$TMP/verdict.yml"
expect_has "trusted success verdict is same-repo guarded" 'head\.repo\.fork != true' "$TMP/verdict.yml"

echo ""
echo "==================================================="
echo "  checks passed: $PASS_N   failed: $FAIL_N"
echo "==================================================="

if [ "$FAIL_N" -ne 0 ]; then
    echo "workflow structure exit gate: RED"
    exit 1
fi
echo "workflow structure exit gate: GREEN"
