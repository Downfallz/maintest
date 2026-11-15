#!/usr/bin/env bash
# What the last green gate saw, kept per clone under the git directory and never committed.
#
#   scripts/gate/state.sh tree      # the working tree's fingerprint: tracked and untracked files, ignored ones left out
#   scripts/gate/state.sh record [tree]   # scripts/verify.sh, once every step passed: the gate saw that tree
#   scripts/gate/state.sh changed   # the paths changed since the last green gate, markdown left out
#
# With no gate recorded, `changed` starts from where the branch left main, so a session that edited nothing
# lists nothing. The fingerprint is the id of the tree `git add -A` would commit, written through a copy of the
# index, so the real index, the stash and the branch are left as they were.
set -uo pipefail

state_dir() {
  local dir
  dir="$(git rev-parse --git-path claude-gate)" || return 1
  mkdir -p "$dir" && echo "$dir"
  return $?
}

working_tree() {
  local index scratch tree status
  index="$(git rev-parse --git-path index)" || return 1
  scratch="$(mktemp)"
  if [[ -f "$index" ]]; then
    cp "$index" "$scratch"
  fi
  tree="$(GIT_INDEX_FILE="$scratch" git add -A > /dev/null 2>&1 && GIT_INDEX_FILE="$scratch" git write-tree)"
  status=$?
  rm -f "$scratch"
  [[ $status -eq 0 ]] && echo "$tree"
  return $status
}

# The tree the next check starts from: the last green gate's, or where this branch left main, or HEAD.
starting_tree() {
  local dir="$1"
  local base
  if [[ -s "$dir/verified" ]]; then
    cat "$dir/verified"
    return 0
  fi
  if base="$(git merge-base HEAD origin/main 2> /dev/null)"; then
    git rev-parse "$base^{tree}"
    return $?
  fi
  git rev-parse 'HEAD^{tree}'
  return $?
}

command="${1:-}"
case "$command" in
  tree)
    working_tree
    ;;
  record)
    # verify.sh passes the tree it started on: a file edited while the gate ran was not what it tested.
    dir="$(state_dir)" || exit 1
    tree="${2:-$(working_tree)}" || exit 1
    echo "$tree" > "$dir/verified"
    ;;
  changed)
    dir="$(state_dir)" || exit 1
    from="$(starting_tree "$dir")" || exit 1
    to="$(working_tree)" || exit 1
    git diff --name-only "$from" "$to" | grep -v '\.md$'
    exit 0
    ;;
  *)
    echo "usage: scripts/gate/state.sh tree|record|changed" >&2
    exit 2
    ;;
esac
