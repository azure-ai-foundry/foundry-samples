#!/usr/bin/env bash
# run-tests.sh — Phase-1 exit gate for validate-sample.sh (all 5 languages).
#
# Proves the classifier's three-way split on REAL toolchains:
#   - <lang>-good    => exit 0 / verdict=pass   (language default succeeds)
#   - <lang>-broken  => exit 1 / verdict=fail   (default fails at the compile step)
#   - toolchain gone => exit 2 / verdict=error  (required binary absent from PATH)
# for lang in { csharp, python, typescript, java, go }.
#
# Plus, toolchain-free proofs:
#   - shared run_sample_yaml path: sample.yaml validate:true => pass, :false => fail (needs yq)
#   - L4 contract: declared pass/fail/error, required environment, and undeclared no-op
#   - guardrails: unknown language, missing --sample-dir, missing --language => error
#   - --results-dir plumbing lands each sample path in passed/failed/errored.txt
#
# Plus P1.3 (ADO 5449687) dependency-install transport classification:
#   - pip/npm install against a forced-unreachable registry (127.0.0.1:1) => error (exit 2)
#   - an unaccompanied resolution failure (package not found, no transport) => fail (exit 1)
#
# Designed to run on a GitHub Actions ubuntu-latest runner AFTER setup-dotnet /
# setup-python / setup-node / setup-java / setup-go have put the toolchains on PATH
# (that is the "callable from GH Actions on a real runner" proof). It also runs
# locally: any language whose toolchain is absent has its good/broken cases SKIPPED
# (BLOCKED-on-env) while its ERROR case — which needs no toolchain — still runs.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/../validate-sample.sh"
FIX="$HERE/fixtures"
BASH_BIN="$(command -v bash)"

LANGS="csharp python typescript java go"

# language -> the toolchain binary its default requires (drives run-vs-skip + scrub).
reqbin() {
    case "$1" in
        csharp)     echo dotnet ;;
        python)     echo python ;;
        typescript) echo npm ;;
        java)       echo mvn ;;
        go)         echo go ;;
    esac
}

# Whether ALL binaries a language default needs are present (drives run-vs-skip of
# the good/broken cases). Some toolchains need more than their headline binary:
#   - typescript: npm's build path shells out to node -> need both.
#   - java: mvn needs a real JDK (javac + a valid JAVA_HOME) to compile -> require javac.
# The scrub ERROR case still keys off reqbin (the binary require_tool hits first), so
# it runs regardless.
toolchain_ready() {
    case "$1" in
        typescript) command -v node  >/dev/null 2>&1 && command -v npm >/dev/null 2>&1 ;;
        java)       command -v javac >/dev/null 2>&1 && command -v mvn >/dev/null 2>&1 ;;
        *)          command -v "$(reqbin "$1")" >/dev/null 2>&1 ;;
    esac
}

# --- bootstrap yq if missing (the workflow provides it via a setup step) ------
if ! command -v yq >/dev/null 2>&1; then
    BIN="${HOME:-/tmp}/.local/bin"
    mkdir -p "$BIN"
    echo "yq not found; bootstrapping static binary into $BIN ..."
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL https://github.com/mikefarah/yq/releases/download/v4.44.3/yq_linux_amd64 \
            -o "$BIN/yq" && chmod +x "$BIN/yq" || true
    fi
    export PATH="$BIN:$PATH"
fi
YQ_OK=false
command -v yq >/dev/null 2>&1 && YQ_OK=true

PASS_N=0
FAIL_N=0
SKIP_N=0

# check <desc> <expected_exit> <expected_verdict> -- <command...>
check() {
    local desc="$1" exp_code="$2" exp_verdict="$3"
    shift 3
    [ "$1" = "--" ] && shift
    local out code verdict
    out="$("$@" 2>&1)"
    code=$?
    verdict="$(printf '%s\n' "$out" | sed -n 's/^verdict=\(pass\|fail\|error\)$/\1/p' | tail -1)"
    if [ "$code" = "$exp_code" ] && [ "$verdict" = "$exp_verdict" ]; then
        echo "  PASS  $desc  (exit=$code verdict=$verdict)"
        PASS_N=$((PASS_N + 1))
    else
        echo "  FAIL  $desc  expected exit=$exp_code verdict=$exp_verdict, got exit=$code verdict=${verdict:-<none>}"
        printf '%s\n' "$out" | sed 's/^/        | /'
        FAIL_N=$((FAIL_N + 1))
    fi
}

# check_l4 <desc> <expected_exit> <expected_verdict> <expected_declared> -- <command...>
check_l4() {
    local desc="$1" exp_code="$2" exp_verdict="$3" exp_declared="$4"
    shift 4
    [ "$1" = "--" ] && shift
    local out code verdict declared
    out="$("$@" 2>&1)"
    code=$?
    verdict="$(printf '%s\n' "$out" | sed -n 's/^verdict=\(pass\|fail\|error\)$/\1/p' | tail -1)"
    declared="$(printf '%s\n' "$out" | sed -n 's/^l4_declared=\(true\|false\)$/\1/p' | tail -1)"
    if [ "$code" = "$exp_code" ] && [ "$verdict" = "$exp_verdict" ] && [ "$declared" = "$exp_declared" ]; then
        echo "  PASS  $desc  (exit=$code verdict=$verdict l4_declared=$declared)"
        PASS_N=$((PASS_N + 1))
    else
        echo "  FAIL  $desc  expected exit=$exp_code verdict=$exp_verdict l4_declared=$exp_declared, got exit=$code verdict=${verdict:-<none>} l4_declared=${declared:-<none>}"
        printf '%s\n' "$out" | sed 's/^/        | /'
        FAIL_N=$((FAIL_N + 1))
    fi
}

assert_file_has() {
    # $1 = file, $2 = substring
    if grep -qF "$2" "$1" 2>/dev/null; then
        echo "  PASS  $(basename "$1") contains $2"
        PASS_N=$((PASS_N + 1))
    else
        echo "  FAIL  $(basename "$1") missing $2"
        FAIL_N=$((FAIL_N + 1))
    fi
}

RESULTS="$(mktemp -d)"
OUTPUTS="$(mktemp)"

# SCRUB_BIN: a PATH holding ONLY coreutils the script needs to REACH its
# require_tool check (ls to detect *.csproj, mkdir for --results-dir, etc.) but
# NONE of the language toolchains (dotnet/python/node/npm/mvn/go). This proves the
# "required toolchain binary missing -> ERROR" branch deterministically, regardless
# of what is preinstalled on the runner.
SCRUB_BIN="$(mktemp -d)"
for tool in ls mkdir cat rm sed grep dirname basename cp mv; do
    src="$(command -v "$tool" 2>/dev/null)"
    [ -n "$src" ] && ln -s "$src" "$SCRUB_BIN/$tool"
done
trap 'rm -rf "$RESULTS" "$SCRUB_BIN" "$OUTPUTS"' EXIT

echo "=============================================================="
echo " Per-language classifier (good=0 / broken=1 / toolchain-gone=2)"
echo "=============================================================="
for lang in $LANGS; do
    bin="$(reqbin "$lang")"
    echo "== $lang (requires: $bin) =="

    # ERROR tier — needs NO toolchain (scrubbed PATH), so it always runs.
    check "$lang: toolchain missing ($bin) -> error" 2 error -- \
        env "PATH=$SCRUB_BIN" "$BASH_BIN" "$SCRIPT" --language "$lang" --sample-dir "$FIX/$lang-good"

    # PASS/FAIL tier — needs the real toolchain. Skip (not fail) when it is absent.
    if toolchain_ready "$lang"; then
        check "$lang: good -> pass"   0 pass -- bash "$SCRIPT" --language "$lang" --sample-dir "$FIX/$lang-good"   --results-dir "$RESULTS"
        check "$lang: broken -> fail" 1 fail -- bash "$SCRIPT" --language "$lang" --sample-dir "$FIX/$lang-broken" --results-dir "$RESULTS"
    else
        echo "  SKIP  $lang good/broken — toolchain '$bin' absent (BLOCKED-on-env; the runner has it)"
        SKIP_N=$((SKIP_N + 2))
    fi
done

echo ""
echo "=============================================================="
echo " Shared run_sample_yaml path (sample.yaml; needs yq, no toolchain)"
echo "=============================================================="
if [ "$YQ_OK" = true ]; then
    check "sample.yaml validate:true -> pass"  0 pass -- bash "$SCRIPT" --language csharp --sample-dir "$FIX/csharp-yaml-good"   --results-dir "$RESULTS"
    check "sample.yaml validate:false -> fail" 1 fail -- bash "$SCRIPT" --language csharp --sample-dir "$FIX/csharp-yaml-broken" --results-dir "$RESULTS"
else
    echo "  SKIP  sample.yaml pass/fail — yq unavailable (BLOCKED-on-env)"
    SKIP_N=$((SKIP_N + 2))
fi

echo ""
echo "=============================================================="
echo " Per-sample L4 declaration contract (toolchain-free; needs yq)"
echo "=============================================================="
if [ "$YQ_OK" = true ]; then
    check_l4 "L4 declared command + inherited env -> pass" 0 pass true -- \
        env "GITHUB_OUTPUT=$OUTPUTS" "SKIP_PROVISION=true" "L4_TEST_ENDPOINT=https://stub.invalid" \
            bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-good" --results-dir "$RESULTS"
    check_l4 "L4 cold caller keeps SKIP_PROVISION=false -> pass" 0 pass true -- \
        env "SKIP_PROVISION=false" \
            bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-cold" --results-dir "$RESULTS"
    check_l4 "L4 undeclared -> clean no-op" 0 pass false -- \
        env "GITHUB_OUTPUT=$OUTPUTS" \
            bash "$SCRIPT" --level 4 --sample-dir "$FIX/csharp-yaml-good" --results-dir "$RESULTS"
    check_l4 "L4 explicit exit 1 -> fail" 1 fail true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-fail" --results-dir "$RESULTS"
    check_l4 "L4 explicit exit 2 -> error" 2 error true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-infra" --results-dir "$RESULTS"
    check_l4 "L4 exit 1 with transport-like text -> fail" 1 fail true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-transport-like" --results-dir "$RESULTS"
    check_l4 "L4 unexpected exit 7 -> fail" 1 fail true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-unexpected" --results-dir "$RESULTS"
    check_l4 "L4 missing required env -> error" 2 error true -- \
        env -u L4_TEST_ENDPOINT "SKIP_PROVISION=true" \
            bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-good" --results-dir "$RESULTS"
    check_l4 "L4 missing SKIP_PROVISION -> error" 2 error true -- \
        env -u SKIP_PROVISION "L4_TEST_ENDPOINT=https://stub.invalid" \
            bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-good" --results-dir "$RESULTS"
    check_l4 "L4 invalid declaration shape -> error" 2 error true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-invalid" --results-dir "$RESULTS"
    check_l4 "L4 missing command -> error" 2 error true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-missing-command" --results-dir "$RESULTS"
    check_l4 "L4 invalid required_env -> error" 2 error true -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-invalid-env" --results-dir "$RESULTS"
    check "L4 malformed sample.yaml -> error" 2 error -- \
        env "SKIP_PROVISION=true" bash "$SCRIPT" --level 4 --sample-dir "$FIX/l4-malformed" --results-dir "$RESULTS"
else
    echo "  SKIP  L4 contract cases — yq unavailable (BLOCKED-on-env)"
    SKIP_N=$((SKIP_N + 13))
fi

echo ""
echo "=============================================================="
echo " Guardrails (ERROR tier, no toolchain required)"
echo "=============================================================="
check "unknown language -> error"   2 error -- bash "$SCRIPT" --language rust   --sample-dir "$FIX/csharp-good"
check "missing sample-dir -> error" 2 error -- bash "$SCRIPT" --language csharp --sample-dir "$FIX/does-not-exist"
check "missing --language -> error" 2 error -- bash "$SCRIPT" --sample-dir "$FIX/csharp-good"
check "unknown --level -> error" 2 error -- bash "$SCRIPT" --level 5 --sample-dir "$FIX/csharp-good"
check_l4 "L4 absent sample.yaml -> no-op without yq" 0 pass false -- \
    env "PATH=$SCRUB_BIN" "$BASH_BIN" "$SCRIPT" --level 4 --sample-dir "$FIX/csharp-good"

echo ""
echo "=============================================================="
echo " --results-dir plumbing (passed/failed/errored.txt)"
echo "=============================================================="
# An ERROR case with --results-dir must land in errored.txt (toolchain-free scrub).
env "PATH=$SCRUB_BIN" "$BASH_BIN" "$SCRIPT" --language csharp --sample-dir "$FIX/csharp-good" --results-dir "$RESULTS" >/dev/null 2>&1 || true
assert_file_has "$RESULTS/errored.txt" "$FIX/csharp-good"
if [ "$YQ_OK" = true ]; then
    # The yq sample.yaml cases above already appended to passed/failed.txt.
    assert_file_has "$RESULTS/passed.txt" "$FIX/csharp-yaml-good"
    assert_file_has "$RESULTS/passed.txt" "$FIX/l4-good"
    assert_file_has "$RESULTS/passed.txt" "$FIX/l4-cold"
    assert_file_has "$RESULTS/failed.txt" "$FIX/l4-fail"
    assert_file_has "$RESULTS/errored.txt" "$FIX/l4-infra"
    assert_file_has "$RESULTS/errored.txt" "$FIX/l4-malformed"
    assert_file_has "$RESULTS/failed.txt" "$FIX/l4-transport-like"
    assert_file_has "$RESULTS/failed.txt" "$FIX/l4-unexpected"
    assert_file_has "$RESULTS/failed.txt" "$FIX/csharp-yaml-broken"
    assert_file_has "$OUTPUTS" "l4_declared=true"
    assert_file_has "$OUTPUTS" "l4_declared=false"
fi

echo ""
echo "=============================================================="
echo " Dependency-install transport classification (P1.3 / ADO 5449687)"
echo "=============================================================="
# Proves the failure-vs-error split on dependency RESOLUTION — the P1.3 gap:
#   - a forced-unreachable registry (localhost blackhole) => ERROR (exit 2), NEVER fail.
#   - an unaccompanied resolution failure (package not found) => FAIL (exit 1).
# Both are hermetic: 127.0.0.1:1 has nothing listening (=> connection refused), and the FAIL
# case uses PIP_NO_INDEX + an empty --find-links so pip resolves nothing WITHOUT any network.
EMPTY_LINKS="$(mktemp -d)"

# pip: blackhole index => transport evidence => ERROR. requirements.txt names a REAL package,
# so pip ALSO prints its misleading "No matching distribution" conclusion after exhausting
# retries — this case proves transport evidence WINS over that generic line (the precedence fix).
# The FAIL case (no index at all) prints the SAME "No matching distribution" WITHOUT transport
# evidence => must stay FAIL. Same fixture, opposite verdicts: the whole point of P1.3.
if toolchain_ready python; then
    check "pip install: registry unreachable -> error" 2 error -- \
        env "PIP_INDEX_URL=http://127.0.0.1:1" "PIP_RETRIES=1" "PIP_DEFAULT_TIMEOUT=5" \
            bash "$SCRIPT" --language python --sample-dir "$FIX/python-deps" --results-dir "$RESULTS"
    check "pip install: unresolvable package (no transport) -> fail" 1 fail -- \
        env "PIP_NO_INDEX=1" "PIP_FIND_LINKS=$EMPTY_LINKS" \
            bash "$SCRIPT" --language python --sample-dir "$FIX/python-deps" --results-dir "$RESULTS"
else
    echo "  SKIP  pip transport cases — python toolchain absent (BLOCKED-on-env; the runner has it)"
    SKIP_N=$((SKIP_N + 2))
fi

# npm: blackhole registry => connect ECONNREFUSED => ERROR (exit 2).
if toolchain_ready typescript; then
    check "npm install: registry unreachable -> error" 2 error -- \
        env "NPM_CONFIG_REGISTRY=http://127.0.0.1:1" "NPM_CONFIG_FETCH_RETRIES=0" \
            bash "$SCRIPT" --language typescript --sample-dir "$FIX/typescript-deps" --results-dir "$RESULTS"
else
    echo "  SKIP  npm transport case — node/npm toolchain absent (BLOCKED-on-env; the runner has it)"
    SKIP_N=$((SKIP_N + 1))
fi
rm -rf "$EMPTY_LINKS"

echo ""
echo "==================================================="
echo "  checks passed: $PASS_N   failed: $FAIL_N   skipped: $SKIP_N"
echo "==================================================="

if [ "$FAIL_N" -ne 0 ]; then
    echo "Phase-1 exit gate: RED — a check failed."
    exit 1
fi
if [ "$SKIP_N" -ne 0 ] || [ "$YQ_OK" != true ]; then
    echo "Phase-1 exit gate: PARTIAL — some cases were BLOCKED-on-env (missing toolchain/yq)."
    echo "A full GREEN requires every toolchain present (i.e. a real Actions runner)."
    exit 3
fi
echo "Phase-1 exit gate: GREEN — all 5 languages proved pass=0 / fail=1 / error=2."
