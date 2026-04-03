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
[[ -n "$endpoint" ]] || endpoint="https://${target_resource_name}.services.ai.azure.com/api/projects/${target_project_name}"

if [[ -n "$source_resource_id" ]]; then
  mapfile -t source_parts < <(parse_resource_id "$source_resource_id") || { echo "❌ Could not parse --source-resource-id"; exit 1; }
  source_resource_name="${source_parts[2]}"
  source_project_name="${source_parts[3]}"
  [[ -n "$source_endpoint" ]] || source_endpoint="https://${source_resource_name}.services.ai.azure.com/api/projects/${source_project_name}"
fi

command -v az >/dev/null || { echo "❌ Azure CLI not found"; exit 1; }
command -v docker >/dev/null || { echo "❌ Docker not found"; exit 1; }
docker info >/dev/null 2>&1 || { echo "❌ Docker is not running"; exit 1; }

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_dir"
docker build -t v1-to-v2-migration .

az account show >/dev/null 2>&1 || az login --use-device-code >/dev/null
az account set --subscription "$target_subscription"
tenant_id="$(az account show --query tenantId -o tsv | tr -d '\r')"
[[ -n "$tenant_id" ]] || { echo "❌ Could not discover tenant ID"; exit 1; }

ai_token="$(az account get-access-token --scope https://ai.azure.com/.default --tenant "$tenant_id" --query accessToken -o tsv | tr -d '\r')"
compat_token="$(az account get-access-token --scope https://cognitiveservices.azure.com/.default --tenant "$tenant_id" --query accessToken -o tsv | tr -d '\r')"

migration_args=()
if [[ -n "$source_endpoint" ]]; then
  migration_args+=("--project-endpoint" "$source_endpoint")
else
  migration_args+=("--project-endpoint" "$endpoint")
fi
migration_args+=("--production-resource" "$target_resource_name" "--production-subscription" "$target_subscription" "--production-tenant" "$tenant_id" "--production-endpoint" "$endpoint")
migration_args+=("${passthrough[@]}")

docker_env=(
  --network host
  -e DOCKER_CONTAINER=true
  -e TARGET_SUBSCRIPTION="$target_subscription"
  -e AZ_TOKEN="$ai_token"
  -e AZ_TOKEN_SCOPE=https://ai.azure.com/.default
  -e PRODUCTION_TOKEN="$ai_token"
)
[[ -d "$HOME/.azure" ]] && docker_env+=( -v "$HOME/.azure:/home/migration/.azure" )
if [[ -n "$compat_token" ]]; then
  docker_env+=( -e OPENAI_COMPAT_TOKEN="$compat_token" -e OPENAI_COMPAT_TOKEN_SCOPE=https://cognitiveservices.azure.com/.default )
fi
if [[ -f .env ]]; then
  while IFS= read -r line; do
    [[ "$line" =~ ^[^#=]+=(.*)$ ]] || continue
    docker_env+=( -e "$line" )
  done < .env
fi

docker run --rm -it "${docker_env[@]}" v1-to-v2-migration "${migration_args[@]}"