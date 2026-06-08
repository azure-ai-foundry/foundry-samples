#!/usr/bin/env bash
# public-overlay/.github/scripts/mirror-back.sh
#
# Replay non-sync-App commits from public main into private as reviewable PRs.

set -euo pipefail

ZERO_SHA="0000000000000000000000000000000000000000"
PRIMARY_SYNC_BOT="foundry-samples-repo-sync[bot]"
LEGACY_SYNC_BOT="foundry-samples-sync[bot]"

log() {
    echo "[mirror-back] $*" >&2
}

usage() {
    cat >&2 <<'EOF'
Usage: mirror-back.sh

Environment:
  PUBLIC_REPO_PATH   Checkout of public repo (default: public-repo)
  PRIVATE_REPO_PATH  Checkout of private repo (default: private-repo)
  PUBLIC_REPO        owner/name of public repo (default: github.repository)
  PRIVATE_REPO       owner/name of private repo (default: microsoft-foundry/foundry-samples-pr)
  BEFORE_SHA         push event before SHA
  AFTER_SHA          push event after SHA
  PUBLIC_SHA         single public commit SHA for workflow_dispatch/testing
  DRY_RUN            1/true to print intended PR body without writes
  MIRROR_SKIP_PUSH   1 to skip git push in local tests
EOF
}

slugify() {
    local input="$1"
    local slug
    slug=$(printf '%s' "$input" \
        | tr '[:upper:]' '[:lower:]' \
        | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//; s/-+/-/g' \
        | cut -c1-80 \
        | sed -E 's/-+$//')
    if [[ -z "$slug" ]]; then
        slug="public-change"
    fi
    printf '%s\n' "$slug"
}

assignee_from_email() {
    local email="$1"
    if [[ "$email" =~ ^[0-9]+\+([^@]+)@users\.noreply\.github\.com$ ]]; then
        printf '%s\n' "${BASH_REMATCH[1]}"
    fi
}

if [[ "${1:-}" == "--slug" ]]; then
    slugify "${2:-}"
    exit 0
fi
if [[ "${1:-}" == "--assignee-from-email" ]]; then
    assignee_from_email "${2:-}"
    exit 0
fi
if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

PUBLIC_REPO_PATH="${PUBLIC_REPO_PATH:-public-repo}"
PRIVATE_REPO_PATH="${PRIVATE_REPO_PATH:-private-repo}"
PUBLIC_REPO="${PUBLIC_REPO:-${GITHUB_REPOSITORY:-microsoft-foundry/foundry-samples}}"
PRIVATE_REPO="${PRIVATE_REPO:-microsoft-foundry/foundry-samples-pr}"
DRY_RUN="${DRY_RUN:-0}"
MIRROR_SKIP_PUSH="${MIRROR_SKIP_PUSH:-0}"

for path in "$PUBLIC_REPO_PATH/.git" "$PRIVATE_REPO_PATH/.git"; do
    if [[ ! -e "$path" ]]; then
        echo "ERROR: required git repo not found: $path" >&2
        exit 1
    fi
done

is_truthy() {
    [[ "${1:-}" == "1" || "${1:-}" == "true" || "${1:-}" == "True" ]]
}

is_sync_bot_identity() {
    local value="$1"
    [[ "$value" == "$PRIMARY_SYNC_BOT" ]] && return 0
    [[ "$value" == "$LEGACY_SYNC_BOT" ]] && return 0
    [[ "$value" == *"$PRIMARY_SYNC_BOT@users.noreply.github.com" ]] && return 0
    [[ "$value" == *"$LEGACY_SYNC_BOT@users.noreply.github.com" ]] && return 0
    return 1
}

should_skip_commit() {
    local sha="$1"
    local author_name author_email committer_name committer_email
    author_name=$(git -C "$PUBLIC_REPO_PATH" show -s --format='%an' "$sha")
    author_email=$(git -C "$PUBLIC_REPO_PATH" show -s --format='%ae' "$sha")
    committer_name=$(git -C "$PUBLIC_REPO_PATH" show -s --format='%cn' "$sha")
    committer_email=$(git -C "$PUBLIC_REPO_PATH" show -s --format='%ce' "$sha")

    for value in "$author_name" "$author_email" "$committer_name" "$committer_email"; do
        if is_sync_bot_identity "$value"; then
            return 0
        fi
    done
    return 1
}

commit_list() {
    if [[ -n "${PUBLIC_SHA:-}" ]]; then
        git -C "$PUBLIC_REPO_PATH" rev-parse --verify "${PUBLIC_SHA}^{commit}"
        return 0
    fi

    local before="${BEFORE_SHA:-}"
    local after="${AFTER_SHA:-${GITHUB_SHA:-}}"
    if [[ -z "$after" ]]; then
        echo "ERROR: set PUBLIC_SHA or AFTER_SHA/GITHUB_SHA" >&2
        exit 1
    fi

    if [[ -z "$before" || "$before" == "$ZERO_SHA" ]]; then
        git -C "$PUBLIC_REPO_PATH" rev-parse --verify "${after}^{commit}"
    else
        git -C "$PUBLIC_REPO_PATH" rev-list --reverse "${before}..${after}"
    fi
}

discover_public_pr() {
    local sha="$1"
    gh api \
        -H "Accept: application/vnd.github.groot-preview+json" \
        "/repos/${PUBLIC_REPO}/commits/${sha}/pulls" 2>/dev/null \
        || printf '[]'
}

json_first_field() {
    local field="$1"
    local payload
    payload="$(cat)"
    JSON_PAYLOAD="$payload" JSON_FIELD="$field" python3 - <<'PY'
import json
import os

field = os.environ["JSON_FIELD"]
try:
    payload = json.loads(os.environ.get("JSON_PAYLOAD", "[]"))
except json.JSONDecodeError:
    payload = []
item = payload[0] if isinstance(payload, list) and payload else {}
value = item.get(field, "") if isinstance(item, dict) else ""
print(value or "")
PY
}

build_body() {
    local sha="$1" short="$2" public_url="$3" pr_url="$4" clean_status="$5"
    cat <<EOF
<!-- public-mirror-sha:$sha -->
This PR mirrors a commit that landed directly on public \`${PUBLIC_REPO}\` main so private \`${PRIVATE_REPO}\` can regain structural parity before the next private→public sync. It is intentionally opened for human review rather than auto-merged so reverse-direction changes stay observable.

- Public commit: ${public_url}
- Public PR: ${pr_url:-not discovered}
- Replay status: ${clean_status}
- Public SHA: \`${sha}\`
- Short SHA: \`${short}\`

If this PR is closed without merging, future mirror-back runs will treat this public SHA as intentionally handled and skip automatic replay.
EOF
}

json_array_has_items() {
    JSON_PAYLOAD="$(cat)" python3 - <<'PY'
import json
import os
import sys

try:
    payload = json.loads(os.environ.get("JSON_PAYLOAD", "[]"))
except json.JSONDecodeError:
    payload = []
sys.exit(0 if isinstance(payload, list) and len(payload) > 0 else 1)
PY
}

existing_pr_for_sha() {
    local branch="$1" sha="$2" marker="public-mirror-sha:$sha"
    local by_branch by_marker
    by_branch=$(gh pr list --repo "$PRIVATE_REPO" --state all --head "$branch" --limit 1 --json number 2>/dev/null || printf '[]')
    if printf '%s' "$by_branch" | json_array_has_items; then
        return 0
    fi

    by_marker=$(gh pr list --repo "$PRIVATE_REPO" --state all --search "\"$marker\" in:body" --limit 1 --json number 2>/dev/null || printf '[]')
    if printf '%s' "$by_marker" | json_array_has_items; then
        return 0
    fi

    return 1
}

checkout_private_base() {
    git -C "$PRIVATE_REPO_PATH" checkout main --quiet
    git -C "$PRIVATE_REPO_PATH" reset --hard HEAD --quiet
}

push_branch() {
    local branch="$1"
    if is_truthy "$MIRROR_SKIP_PUSH"; then
        log "MIRROR_SKIP_PUSH=1; not pushing $branch"
    else
        git -C "$PRIVATE_REPO_PATH" push origin "$branch"
    fi
}

create_pr() {
    local branch="$1" title="$2" body_file="$3" draft_flag="$4" assignee="$5"
    local args=(pr create --repo "$PRIVATE_REPO" --base main --head "$branch" --title "$title" --body-file "$body_file" --label public-mirror)
    if [[ "$draft_flag" == "draft" ]]; then
        args+=(--draft)
    fi
    if [[ -n "$assignee" ]]; then
        args+=(--assignee "$assignee")
    fi
    gh "${args[@]}"
}

create_tracking_issue() {
    local title="$1" body_file="$2"
    gh issue create --repo "$PRIVATE_REPO" --title "$title" --body-file "$body_file" --label triage
}

replay_commit() {
    local sha="$1"
    local short subject pr_json pr_title pr_url title_source slug branch public_url title body_file clean_status assignee author_name author_email

    short="$(git -C "$PUBLIC_REPO_PATH" rev-parse --short=8 "$sha")"
    subject="$(git -C "$PUBLIC_REPO_PATH" show -s --format='%s' "$sha")"
    author_name="$(git -C "$PUBLIC_REPO_PATH" show -s --format='%an' "$sha")"
    author_email="$(git -C "$PUBLIC_REPO_PATH" show -s --format='%ae' "$sha")"

    if should_skip_commit "$sha"; then
        log "Skipping sync-App commit $short"
        return 0
    fi

    pr_json="$(discover_public_pr "$sha")"
    pr_title="$(printf '%s' "$pr_json" | json_first_field title)"
    pr_url="$(printf '%s' "$pr_json" | json_first_field html_url)"
    title_source="${pr_title:-$subject}"
    slug="$(slugify "$title_source")"
    branch="mirror/public-${short}-${slug}"
    title="Mirror: ${title_source} (foundry-samples@${short})"
    public_url="https://github.com/${PUBLIC_REPO}/commit/${sha}"
    body_file="${PRIVATE_REPO_PATH}/.mirror-back-pr-body-${short}.md"

    if existing_pr_for_sha "$branch" "$sha"; then
        log "Skipping $short; existing mirror PR found by branch or marker"
        return 0
    fi

    clean_status="clean"
    build_body "$sha" "$short" "$public_url" "$pr_url" "$clean_status" > "$body_file"

    if is_truthy "$DRY_RUN"; then
        echo "DRY RUN: would create branch $branch"
        echo "DRY RUN: would create PR titled: $title"
        cat "$body_file"
        rm -f "$body_file"
        return 0
    fi

    checkout_private_base
    git -C "$PRIVATE_REPO_PATH" checkout -B "$branch" --quiet

    local patch_file="${PRIVATE_REPO_PATH}/.mirror-back-${short}.patch"
    git -C "$PUBLIC_REPO_PATH" format-patch -1 --stdout "$sha" > "$patch_file"

    if GIT_COMMITTER_NAME="$PRIMARY_SYNC_BOT" \
       GIT_COMMITTER_EMAIL="${PRIMARY_SYNC_BOT}@users.noreply.github.com" \
       git -C "$PRIVATE_REPO_PATH" am --committer-date-is-author-date "$patch_file" >/dev/null 2>&1; then
        rm -f "$patch_file"
        push_branch "$branch"
        create_pr "$branch" "$title" "$body_file" "" ""
        rm -f "$body_file"
        return 0
    fi

    log "Clean replay failed for $short; creating draft conflict PR"
    git -C "$PRIVATE_REPO_PATH" am --abort >/dev/null 2>&1 || true
    clean_status="conflict — draft PR requires manual resolution"
    build_body "$sha" "$short" "$public_url" "$pr_url" "$clean_status" > "$body_file"

    if ! git -C "$PRIVATE_REPO_PATH" apply -3 "$patch_file" >/dev/null 2>&1; then
        true
    fi
    rm -f "$patch_file"

    if git -C "$PRIVATE_REPO_PATH" diff --quiet && git -C "$PRIVATE_REPO_PATH" diff --cached --quiet; then
        mkdir -p "$PRIVATE_REPO_PATH/.github/public-mirror-conflicts"
        cat > "$PRIVATE_REPO_PATH/.github/public-mirror-conflicts/${short}.md" <<EOF
# Public mirror replay conflict

Public commit ${sha} could not be replayed cleanly onto private main, and Git could not leave conflict markers automatically. Re-apply ${public_url} manually.
EOF
    fi

    git -C "$PRIVATE_REPO_PATH" add -A
    GIT_AUTHOR_NAME="$author_name" GIT_AUTHOR_EMAIL="$author_email" \
    GIT_COMMITTER_NAME="$PRIMARY_SYNC_BOT" \
    GIT_COMMITTER_EMAIL="${PRIMARY_SYNC_BOT}@users.noreply.github.com" \
        git -C "$PRIVATE_REPO_PATH" commit -m "Mirror conflict for public ${short}" --quiet || true

    assignee="$(assignee_from_email "$author_email")"
    push_branch "$branch"
    create_pr "$branch" "$title" "$body_file" "draft" "$assignee"

    local issue_body="${PRIVATE_REPO_PATH}/.mirror-back-issue-${short}.md"
    cat > "$issue_body" <<EOF
Mirror-back could not replay ${public_url} cleanly onto private main.

- Draft replay PR branch: \`${branch}\`
- Public PR: ${pr_url:-not discovered}
- Suggested assignee: ${assignee:-unknown}

Resolve the draft PR manually, then close this tracking issue.
EOF
    create_tracking_issue "Mirror-back conflict: ${title_source} (${short})" "$issue_body"
    rm -f "$body_file" "$issue_body"
}

main() {
    local commits=()
    mapfile -t commits < <(commit_list)
    if [[ ${#commits[@]} -eq 0 ]]; then
        log "No commits to inspect"
        return 0
    fi

    local sha
    for sha in "${commits[@]}"; do
        replay_commit "$sha"
    done
}

main "$@"
