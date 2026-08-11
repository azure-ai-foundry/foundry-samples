#!/usr/bin/env sh
# azd postprovision hook (POSIX / sh).
#
# Runs automatically after `azd provision`. It creates the two bundled Agent
# Skills in the Foundry project and then creates the toolbox that serves them,
# storing TOOLBOX_ENDPOINT so the agent can reach it — collapsing the manual
# "Building the toolbox from zero" steps into a single `azd provision`.
#
# The bundled toolbox.yaml references the skills by name, so the skills must
# exist before `azd ai toolbox create` runs.

set -e

# Run from the project directory (the parent of hooks/) so toolbox.yaml and
# skills/ resolve no matter where azd invokes the hook from.
cd "$(CDPATH= cd "$(dirname "$0")/.." && pwd)"

echo "Provisioning the bundled skills..."
# Create each skills/<name>/SKILL.md as a Foundry skill. 'create' fails if the
# skill already exists (e.g. a repeat azd provision), so fall back to 'update',
# which adds a new default version and keeps history.
for skill_dir in skills/*/; do
  name="$(basename "$skill_dir")"
  file="${skill_dir}SKILL.md"
  echo "  Ensuring skill '$name' from $file..."
  azd ai skill create "$name" --file "$file" --no-prompt \
    || azd ai skill update "$name" --file "$file" --no-prompt
done

echo "Creating the skills toolbox..."
# Toolbox versions are immutable and 'create' has no upsert flag, so skip it if
# the toolbox already exists (e.g. on a repeat azd provision). 'toolbox show'
# exits 0 when the toolbox exists and non-zero when it does not.
if azd ai toolbox show maf-skills-toolbox >/dev/null 2>&1; then
  echo "Toolbox maf-skills-toolbox already exists; skipping create."
else
  azd ai toolbox create maf-skills-toolbox --from-file ./toolbox.yaml --no-prompt
fi

# The toolbox's unversioned MCP alias is deterministic from the project endpoint
# and always resolves to the default version.
PROJ="$(azd env get-value FOUNDRY_PROJECT_ENDPOINT | sed 's#/*$##')"
TOOLBOX="$PROJ/toolboxes/maf-skills-toolbox/mcp?api-version=v1"
azd env set TOOLBOX_ENDPOINT "$TOOLBOX"

echo "Done. TOOLBOX_ENDPOINT = $TOOLBOX"
