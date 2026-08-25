#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$repo_root"

if [[ "$(git branch --show-current)" != "master" ]]; then
  echo "error: scene-issue intake must run from the local master branch" >&2
  exit 1
fi

if ! git diff --cached --quiet; then
  echo "error: the index already contains staged changes; commit or unstage them first" >&2
  exit 1
fi

git fetch origin master

local_head="$(git rev-parse HEAD)"
remote_head="$(git rev-parse origin/master)"
if [[ "$local_head" != "$remote_head" ]]; then
  echo "error: local master must exactly match origin/master before publishing captures" >&2
  echo "local:  $local_head" >&2
  echo "remote: $remote_head" >&2
  exit 1
fi

open_root="SceneIssues/open"
if [[ ! -d "$open_root" ]]; then
  echo "error: missing $open_root" >&2
  exit 1
fi

new_captures=()
while IFS= read -r -d '' capture_dir; do
  relative_dir="${capture_dir#./}"
  issue_file="$relative_dir/issue.json"
  if [[ ! -f "$issue_file" ]]; then
    echo "error: capture directory has no issue.json: $relative_dir" >&2
    exit 1
  fi
  if git cat-file -e "HEAD:$issue_file" 2>/dev/null; then
    continue
  fi
  issue_status="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1])).get("status", ""))' "$issue_file")"
  if [[ "$issue_status" != "open" ]]; then
    echo "error: new capture must have status=open: $issue_file (found '$issue_status')" >&2
    exit 1
  fi
  new_captures+=("$relative_dir")
done < <(find "$open_root" -mindepth 1 -maxdepth 1 -type d -print0 | sort -z)

if (( ${#new_captures[@]} == 0 )); then
  echo "No new scene issues to publish."
  exit 0
fi

git add -- "${new_captures[@]}"

unexpected_paths=()
while IFS= read -r staged_path; do
  matched=false
  for capture_dir in "${new_captures[@]}"; do
    if [[ "$staged_path" == "$capture_dir/"* ]]; then
      matched=true
      break
    fi
  done
  if [[ "$matched" == false ]]; then
    unexpected_paths+=("$staged_path")
  fi
done < <(git diff --cached --name-only)

if (( ${#unexpected_paths[@]} > 0 )); then
  git restore --staged -- "${new_captures[@]}"
  echo "error: refusing to commit unexpected staged paths:" >&2
  printf '  %s\n' "${unexpected_paths[@]}" >&2
  exit 1
fi

count=${#new_captures[@]}
git commit -m "Capture $count new scene issue(s)"

if ! git push origin HEAD:refs/heads/master; then
  echo "error: capture commit was created locally but could not be pushed" >&2
  echo "resolve the remote update, then push with: git push origin HEAD:master" >&2
  exit 1
fi

echo "Published $count new scene issue(s) to origin/master:"
printf '  %s\n' "${new_captures[@]}"
