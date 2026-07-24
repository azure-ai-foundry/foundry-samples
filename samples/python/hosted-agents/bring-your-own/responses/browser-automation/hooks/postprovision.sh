#!/bin/sh
set -e

echo "========================================"
echo "  Playwright Workspace Connection Setup"
echo "========================================"

# ── Check if already configured ───────────────────────────────────────────────

_AZD_ENV_CACHE=$(azd env get-values 2>/dev/null || true)

_azd_get() {
    printf '%s' "$_AZD_ENV_CACHE" | grep "^${1}=" | head -1 | sed 's/^[^=]*="//' | sed 's/"$//'
}

CONNECTION_CONFIGURED=$(_azd_get PLAYWRIGHT_CONNECTION_CONFIGURED)
EXISTING_TOOLBOX=$(_azd_get TOOLBOX_NAME)

if [ "$CONNECTION_CONFIGURED" = "true" ] && [ -n "$EXISTING_TOOLBOX" ]; then
    echo "Playwright connection already configured (toolbox: $EXISTING_TOOLBOX)"
    exit 0
fi

SCRIPT_DIR=$(dirname "$0")

# ── Step 1: Setup Playwright connection (skip if already done) ────────────────

if [ "$CONNECTION_CONFIGURED" != "true" ]; then
    . "$SCRIPT_DIR/setup-playwright.sh"
else
    echo "Playwright connection exists. Skipping connection setup..."
fi

# ── Step 2: Setup Toolbox ─────────────────────────────────────────────────────

. "$SCRIPT_DIR/setup-toolbox.sh"