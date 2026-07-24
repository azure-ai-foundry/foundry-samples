#!/usr/bin/env bash
# validate-sample.sh — reusable single-sample validator (Build Readiness Level 3 / Load).
#
# Ported from .azure-pipelines/validation.yml Stage 2, which duplicated the same
# per-sample body across five per-language jobs (C#, Python, TypeScript, Java, Go).
# That duplication is collapsed here into ONE function set: the sample.yaml command
# runner lives once, and only the genuinely per-language default differs.
#
# Single responsibility: validate ONE sample directory. The caller owns the
# `while read dir` loop (per-sample granularity for validation/<pipeline>/<sample-path>
# statuses) and the toolchain SETUP (setup-dotnet / setup-python / ... actions).
# This script assumes the required toolchain is already on PATH and reports ERROR if not.
#
# Usage:
#   validate-sample.sh --language <csharp|python|typescript|java|go> \
#                      --sample-dir <path> [--results-dir <dir>]
#
# Verdict / exit codes — the classifier. This split is load-bearing: downstream lanes
# and the sync gate trust it, and ERROR must NEVER be treated as a sample failure
# (that is what protects good samples from being quarantined when our infra breaks).
#
#   0  PASS   the sample loads at L3
#             (sample.yaml build/validate/test all exit 0, or the language default succeeds)
#   1  FAIL   the SAMPLE is broken
#             (a sample.yaml command exits non-zero; or the language default's
#              compile/build/py_compile exits non-zero)
#   2  ERROR  OUR INFRA is sick — page us, do not quarantine
#             (bad/missing args, unknown language, missing sample dir, unreadable or
#              malformed sample.yaml, a required toolchain binary missing from PATH,
#              or yq unavailable when a sample.yaml must be read)
#
# Gray area (honest v1): dependency-resolution failures (pip install / npm install)
# stay FAIL — usually the sample's declared deps are wrong — but are log-tagged
# `[classify:gray]` so a later advisory pass can audit registry/network mis-classifications.
#
# Outputs:
#   - prints `verdict=pass|fail|error` on stdout (also appended to $GITHUB_OUTPUT if set)
#   - if --results-dir is given, appends the sample path to
#     passed.txt | failed.txt | errored.txt in that directory.
#
# NOTE: `set -e` is intentionally NOT used. The classifier owns every exit code, so a
# failing external command must be caught (`if ! cmd`) and routed to fail()/error(),
# never allowed to abort the script with its own status.
set -uo pipefail

LANGUAGE=""
SAMPLE_DIR=""
RESULTS_DIR=""
SAMPLE_YAML_FAIL_STEP=""
PYTHON_VENV_DIR=""

usage() {
    cat <<'EOF'
Usage: validate-sample.sh --language <lang> --sample-dir <path> [--results-dir <dir>]

  --language     One of: csharp | python | typescript | java | go (frozen set).
  --sample-dir   Path to the single sample directory to validate.
  --results-dir  Optional. Directory to append passed.txt/failed.txt/errored.txt.

Exit codes: 0 = pass, 1 = fail (sample broken), 2 = error (infra broken).
EOF
}

# --- Classifier emit helpers -------------------------------------------------
# Each terminates the process with the verdict's exit code so no path can fall
# through without emitting exactly one verdict.

emit_verdict() {
    # $1 = pass|fail|error, $2 = exit code
    local verdict="$1" code="$2"
    echo "verdict=${verdict}"
    if [ -n "${GITHUB_OUTPUT:-}" ]; then
        echo "verdict=${verdict}" >> "$GITHUB_OUTPUT"
    fi
    if [ -n "$RESULTS_DIR" ]; then
        mkdir -p "$RESULTS_DIR"
        case "$verdict" in
            pass)  echo "$SAMPLE_DIR" >> "$RESULTS_DIR/passed.txt" ;;
            fail)  echo "$SAMPLE_DIR" >> "$RESULTS_DIR/failed.txt" ;;
            error) echo "$SAMPLE_DIR" >> "$RESULTS_DIR/errored.txt" ;;
        esac
    fi
    exit "$code"
}

pass() {
    echo "PASS: ${SAMPLE_DIR:-<no-dir>}"
    emit_verdict pass 0
}

fail() {
    # $1 = reason (optional)
    echo "FAIL: ${SAMPLE_DIR:-<no-dir>}${1:+ — $1}"
    emit_verdict fail 1
}

error() {
    # $1 = reason (optional). ERROR is infra-sick: page us, never quarantine.
    echo "ERROR: ${1:-infra failure} (sample=${SAMPLE_DIR:-<no-dir>})" >&2
    emit_verdict error 2
}

# require_tool <bin>: a required toolchain binary must be on PATH, else ERROR.
require_tool() {
    command -v "$1" >/dev/null 2>&1 || error "required toolchain binary not found on PATH: $1"
}

# ensure_yq: lazy precondition — only needed to read a sample.yaml. yq setup itself
# belongs to the caller/workflow; the script only asserts its presence.
ensure_yq() {
    command -v yq >/dev/null 2>&1 || error "yq not available on PATH (required to read sample.yaml)"
}

# --- Part 1: the once-only shared sample.yaml runner -------------------------
# Byte-identical across all five ADO jobs today. Reads build -> validate -> test
# and runs them in order; the first non-zero command is a sample FAIL.
#
# Returns: 0 = ran >=1 command and all passed (caller should PASS)
#          1 = a command exited non-zero    (caller should FAIL)
#          3 = no sample.yaml, or it declared no commands (caller runs the default)
# Unreadable/malformed YAML calls error() directly (ERROR 2).
#
# For Python the per-sample venv is already activated in the parent shell before this
# runs, so the `( cd ... && eval )` subshell inherits it — keeping this function
# identical for all five languages (mirrors ADO, which sourced the venv per command).
run_sample_yaml() {
    local yaml="$SAMPLE_DIR/sample.yaml"
    [ -f "$yaml" ] || return 3

    ensure_yq

    if ! yq eval '.' "$yaml" >/dev/null 2>&1; then
        error "sample.yaml is unreadable or malformed: $yaml"
    fi

    local had_cmd=false cmd cmd_type
    for cmd_type in build validate test; do
        cmd="$(yq eval ".${cmd_type} // \"\"" "$yaml" 2>/dev/null)"
        if [ -n "$cmd" ] && [ "$cmd" != "null" ]; then
            had_cmd=true
            echo "Running $cmd_type: $cmd"
            if ! ( cd "$SAMPLE_DIR" && eval "$cmd" ); then
                SAMPLE_YAML_FAIL_STEP="$cmd_type"
                return 1
            fi
        fi
    done

    [ "$had_cmd" = true ] && return 0
    return 3
}

# --- Part 2: per-language defaults (grounded in validation.yml Stage 2) -------
# Each ends by calling pass() or fail(). require_tool guards are placed only where
# the tool is actually invoked, preserving ADO's "no build file present => PASS" edges.

# ADO validation.yml L285-298
default_validate_csharp() {
    if ls "$SAMPLE_DIR"/*.csproj 1>/dev/null 2>&1; then
        require_tool dotnet
        local proj
        for proj in "$SAMPLE_DIR"/*.csproj; do
            echo "Building: $proj"
            if ! dotnet build "$proj" --verbosity minimal; then
                fail "dotnet build failed: $proj"
            fi
        done
        pass
    else
        echo "No .csproj found in $SAMPLE_DIR"
        pass
    fi
}

# ADO validation.yml L368-439. Venv is created + activated by python_setup_venv
# (called before the sample.yaml/default branch, mirroring ADO's up-front venv).
default_validate_python() {
    if [ -f "$SAMPLE_DIR/requirements.txt" ]; then
        echo "Installing requirements..."
        if ! pip install -r "$SAMPLE_DIR/requirements.txt" -q; then
            echo "[classify:gray] dependency-resolution failure (pip install) — kept FAIL per v1 gray-area rule"
            fail "pip install failed"
        fi
    fi
    local pyfile
    for pyfile in "$SAMPLE_DIR"/*.py; do
        [ -f "$pyfile" ] || continue
        echo "Checking: $pyfile"
        if ! python -m py_compile "$pyfile"; then
            fail "py_compile failed: $pyfile"
        fi
    done
    pass
}

# ADO validation.yml L528-569
default_validate_typescript() {
    if [ -f "$SAMPLE_DIR/package.json" ]; then
        require_tool npm
        echo "Installing dependencies..."
        if ! ( cd "$SAMPLE_DIR" && npm install --silent ); then
            echo "[classify:gray] dependency-resolution failure (npm install) — kept FAIL per v1 gray-area rule"
            fail "npm install failed"
        fi
        if ( cd "$SAMPLE_DIR" && npm run build --if-present ); then
            pass
        else
            fail "npm run build failed"
        fi
    else
        require_tool node
        local jsfile tsfile
        for jsfile in "$SAMPLE_DIR"/*.js; do
            [ -f "$jsfile" ] || continue
            echo "Checking: $jsfile"
            if ! node --check "$jsfile"; then
                fail "node --check failed: $jsfile"
            fi
        done
        for tsfile in "$SAMPLE_DIR"/*.ts; do
            [ -f "$tsfile" ] || continue
            echo "TypeScript file without package.json, skipping: $tsfile"
        done
        pass
    fi
}

# ADO validation.yml L669-694
default_validate_java() {
    if [ -f "$SAMPLE_DIR/pom.xml" ]; then
        require_tool mvn
        echo "Building with Maven..."
        if ( cd "$SAMPLE_DIR" && mvn compile -q ); then
            pass
        else
            fail "mvn compile failed"
        fi
    elif [ -f "$SAMPLE_DIR/build.gradle" ] || [ -f "$SAMPLE_DIR/build.gradle.kts" ]; then
        echo "Building with Gradle..."
        # Prefer the sample-provided wrapper; only require a system gradle when there is none.
        if [ ! -f "$SAMPLE_DIR/gradlew" ]; then
            require_tool gradle
        fi
        if ( cd "$SAMPLE_DIR" && { ./gradlew build -q 2>/dev/null || gradle build -q; } ); then
            pass
        else
            fail "gradle build failed"
        fi
    else
        echo "No pom.xml or build.gradle found in $SAMPLE_DIR"
        pass
    fi
}

# ADO validation.yml L787-811
default_validate_go() {
    require_tool go
    if [ -f "$SAMPLE_DIR/go.mod" ]; then
        echo "Building Go module..."
        if ( cd "$SAMPLE_DIR" && go build ./... ); then
            pass
        else
            fail "go build ./... failed"
        fi
    else
        local gofile
        for gofile in "$SAMPLE_DIR"/*.go; do
            [ -f "$gofile" ] || continue
            echo "Checking: $gofile"
            if ! ( cd "$SAMPLE_DIR" && go build "$(basename "$gofile")" ); then
                fail "go build failed: $gofile"
            fi
        done
        pass
    fi
}

# Python isolation pre-step (mirrors ADO: venv created up front, torn down on exit).
cleanup_python_venv() {
    [ -n "$PYTHON_VENV_DIR" ] || return 0
    command -v deactivate >/dev/null 2>&1 && deactivate || true
    rm -rf "$PYTHON_VENV_DIR"
}

python_setup_venv() {
    require_tool python
    PYTHON_VENV_DIR="$SAMPLE_DIR/.venv"
    echo "Creating virtual environment..."
    if ! python -m venv "$PYTHON_VENV_DIR"; then
        error "failed to create Python venv (toolchain/infra failure): $PYTHON_VENV_DIR"
    fi
    # shellcheck disable=SC1091
    source "$PYTHON_VENV_DIR/bin/activate" || error "failed to activate Python venv: $PYTHON_VENV_DIR"
}

# --- Argument parsing --------------------------------------------------------
while [ $# -gt 0 ]; do
    case "$1" in
        --language)    LANGUAGE="${2:-}";    shift $(( $# > 1 ? 2 : 1 )) ;;
        --sample-dir)  SAMPLE_DIR="${2:-}";  shift $(( $# > 1 ? 2 : 1 )) ;;
        --results-dir) RESULTS_DIR="${2:-}"; shift $(( $# > 1 ? 2 : 1 )) ;;
        -h|--help)     usage; exit 0 ;;
        *)             usage >&2; error "unknown argument: $1" ;;
    esac
done

# --- Guards (ordered so the ERROR tier is provable without any toolchain) -----
case "$LANGUAGE" in
    csharp|python|typescript|java|go) ;;
    "") error "missing required --language" ;;
    *)  error "unsupported language '$LANGUAGE' (frozen set: csharp|python|typescript|java|go)" ;;
esac

[ -n "$SAMPLE_DIR" ] || error "missing required --sample-dir"
[ -d "$SAMPLE_DIR" ] || error "sample directory not found: $SAMPLE_DIR"
SAMPLE_DIR="${SAMPLE_DIR%/}"

echo "=== validate-sample: language=$LANGUAGE dir=$SAMPLE_DIR ==="

# Python creates its isolated venv before the sample.yaml/default branch (ADO parity),
# so sample.yaml commands also run inside the venv.
if [ "$LANGUAGE" = python ]; then
    trap cleanup_python_venv EXIT
    python_setup_venv
fi

# Part 1: sample.yaml commands take precedence over language defaults.
run_sample_yaml
rc=$?
case "$rc" in
    0) pass ;;
    1) fail "sample.yaml '${SAMPLE_YAML_FAIL_STEP:-?}' command exited non-zero" ;;
    3) : ;;  # no commands declared — fall through to the language default
esac

# Part 2: language default.
case "$LANGUAGE" in
    csharp)     default_validate_csharp ;;
    python)     default_validate_python ;;
    typescript) default_validate_typescript ;;
    java)       default_validate_java ;;
    go)         default_validate_go ;;
esac

# Unreachable: every default_validate_* ends in pass()/fail().
error "internal: no verdict emitted for language=$LANGUAGE"
