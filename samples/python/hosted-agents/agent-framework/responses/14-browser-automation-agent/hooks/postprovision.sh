#!/bin/sh
set -e

echo "========================================"
echo "  Playwright Workspace Connection Setup"
echo "========================================"

# ── Check if already configured ───────────────────────────────────────────────

CONNECTION_CONFIGURED=""
EXISTING_TOOLBOX=""
if azd env get-value PLAYWRIGHT_CONNECTION_CONFIGURED >/dev/null 2>&1; then
    CONNECTION_CONFIGURED=$(azd env get-value PLAYWRIGHT_CONNECTION_CONFIGURED 2>/dev/null)
fi
if azd env get-value TOOLBOX_NAME >/dev/null 2>&1; then
    EXISTING_TOOLBOX=$(azd env get-value TOOLBOX_NAME 2>/dev/null)
fi

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