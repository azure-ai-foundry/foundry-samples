#!/usr/bin/env bash
set -euo pipefail

resource_id=""
endpoint=""
source_resource_id=""
source_endpoint=""
list_mode=false
passthrough=()

while [[ $# -gt 0 ]]; do
	case "$1" in
		--resource-id) resource_id="$2"; shift 2 ;;
		--endpoint) endpoint="$2"; shift 2 ;;
		--source-resource-id) source_resource_id="$2"; shift 2 ;;
		--source-endpoint) source_endpoint="$2"; shift 2 ;;
		--list) list_mode=true; passthrough+=("--list"); shift ;;
		*) passthrough+=("$1"); shift ;;
	esac
done

if [[ -z "$resource_id" ]]; then
	echo "❌ Missing required --resource-id"
	exit 1
fi

parse_resource_id() {
	local id="$1"
	python3 - <<'PY' "$id"
import re, sys
value = sys.argv[1]
m = re.match(r'/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/[^/]+/[^/]+/([^/]+)(?:/projects/([^/]+))?', value)
if not m:
		sys.exit(1)
project = m.group(4) if m.group(4) else m.group(3)
print(m.group(1))
print(m.group(2))
print(m.group(3))
print(project)
PY
}

mapfile -t target_parts < <(parse_resource_id "$resource_id") || { echo "❌ Could not parse resource ID"; exit 1; }
target_subscription="${target_parts[0]}"
target_resource_name="${target_parts[2]}"
target_project_name="${target_parts[3]}"

if [[ -z "$endpoint" ]]; then
	endpoint="https://${target_resource_name}.services.ai.azure.com/api/projects/${target_project_name}"
fi

source_tenant=""
if [[ -n "$source_resource_id" ]]; then
	mapfile -t source_parts < <(parse_resource_id "$source_resource_id") || { echo "❌ Could not parse --source-resource-id"; exit 1; }
	source_subscription="${source_parts[0]}"
	source_resource_name="${source_parts[2]}"
	source_project_name="${source_parts[3]}"
	if [[ -z "$source_endpoint" ]]; then
		source_endpoint="https://${source_resource_name}.services.ai.azure.com/api/projects/${source_project_name}"
	fi
fi

command -v az >/dev/null || { echo "❌ Azure CLI not found"; exit 1; }
command -v python3 >/dev/null || { echo "❌ Python 3 not found"; exit 1; }

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
requirements_path="$script_dir/requirements.txt"
python3 -c "import importlib.util, sys; mods=['requests','azure.identity','azure.ai.projects','pandas','urllib3']; missing=[m for m in mods if importlib.util.find_spec(m) is None]; sys.exit(0 if not missing else 1)" || python3 -m pip install -r "$requirements_path"

az account show >/dev/null 2>&1 || az login --use-device-code >/dev/null
az account set --subscription "$target_subscription"
tenant_id="$(az account show --query tenantId -o tsv | tr -d '\r')"
[[ -n "$tenant_id" ]] || { echo "❌ Could not discover tenant ID"; exit 1; }

if [[ -n "${source_subscription:-}" && "$source_subscription" != "$target_subscription" ]]; then
	source_tenant="$(az account show --subscription "$source_subscription" --query tenantId -o tsv 2>/dev/null | tr -d '\r')"
fi
[[ -n "$source_tenant" ]] || source_tenant="$tenant_id"

target_token="$(az account get-access-token --scope https://ai.azure.com/.default --tenant "$tenant_id" --query accessToken -o tsv | tr -d '\r')"
source_token="$target_token"
if [[ "$source_tenant" != "$tenant_id" ]]; then
	source_token="$(az account get-access-token --scope https://ai.azure.com/.default --tenant "$source_tenant" --query accessToken -o tsv | tr -d '\r')"
fi
source_compat_token="$(az account get-access-token --scope https://cognitiveservices.azure.com/.default --tenant "$source_tenant" --query accessToken -o tsv | tr -d '\r')"

export PRODUCTION_TOKEN="$target_token"
export AZ_TOKEN="$source_token"
export AZ_TOKEN_SCOPE="https://ai.azure.com/.default"
if [[ -n "$source_compat_token" ]]; then
	export OPENAI_COMPAT_TOKEN="$source_compat_token"
	export OPENAI_COMPAT_TOKEN_SCOPE="https://cognitiveservices.azure.com/.default"
fi

cd "$script_dir"
migration_args=()
if [[ -n "$source_endpoint" ]]; then
	migration_args+=("--project-endpoint" "$source_endpoint")
	[[ "$source_tenant" != "$tenant_id" ]] && migration_args+=("--source-tenant" "$source_tenant")
else
	migration_args+=("--project-endpoint" "$endpoint")
fi
migration_args+=("--production-resource" "$target_resource_name" "--production-subscription" "$target_subscription" "--production-tenant" "$tenant_id" "--production-endpoint" "$endpoint")
migration_args+=("${passthrough[@]}")

python3 v1_to_v2_migration.py "${migration_args[@]}"
