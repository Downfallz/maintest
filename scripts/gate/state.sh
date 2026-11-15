#!/usr/bin/env bash
# What the last green gate saw, kept per clone under the git directory and never committed.
#
#   scripts/gate/state.sh tree            # the working tree's fingerprint: tracked and untracked, ignored left out
#   scripts/gate/state.sh record [tree]   # scripts/verify.sh, once every step passed: the gate saw that tree
#   scripts/gate/state.sh changed         # the paths changed since the last green gate on this branch
#   scripts/gate/state.sh running         # exits 0 while a gate runs (scripts/verify.sh keeps the mark)
#
# Markdown counts: the architecture tests read the instruction files, the plans and the indexes. With no gate
# recorded on this branch, `changed` starts from where the branch left main, so a session that edited nothing
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
  # A name only: git refuses an empty file as an index, and a missing one is a fresh index.
  scratch="$(mktemp -u)"
  if [[ -f "$index" ]]; then
    # The index's own time kept: git re-reads a file changed in the second the index was written only when it
    # can tell, and a copy stamped now would pass such a file for unchanged.
    cp -p "$index" "$scratch"
  fi
  tree="$(GIT_INDEX_FILE="$scratch" git add -A > /dev/null 2>&1 && GIT_INDEX_FILE="$scratch" git write-tree)"
  status=$?
  rm -f "$scratch"
  [[ $status -eq 0 ]] && echo "$tree"
  return $status
}

branch_name() {
  git rev-parse --abbrev-ref HEAD 2> /dev/null || echo "HEAD"
  return 0
}

# The tree the check starts from: the last green gate's on this branch while git still holds it, or where this
# branch left main, or HEAD. The recorded tree is an object nothing refers to, so `git gc` may prune it.
starting_tree() {
  local dir="$1"
  local base branch tree
  if [[ -s "$dir/verified" ]]; then
    read -r branch tree < "$dir/verified"
    if [[ "$branch" == "$(branch_name)" ]] && git cat-file -e "$tree^{tree}" 2> /dev/null; then
      echo "$tree"
      return 0
    fi
  fi
  if base="$(git merge-base HEAD origin/main 2> /dev/null)"; then
    git rev-parse "$base^{tree}"
    return $?
  fi
  git rev-parse 'HEAD^{tree}'
  return $?
}

running() {
  local dir="$1"
  local pid
  [[ -s "$dir/running" ]] || return 1
  read -r pid _ < "$dir/running"
  [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2> /dev/null
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
    tree="${2:-}"
    if [[ -z "$tree" ]]; then
      tree="$(working_tree)" || exit 1
    fi
    echo "$(branch_name) $tree" > "$dir/verified"
    ;;
  changed)
    dir="$(state_dir)" || exit 1
    from="$(starting_tree "$dir")" || exit 1
    to="$(working_tree)" || exit 1
    # NUL-separated, so a path with a quote or a non-ASCII letter is printed as it is, not quoted.
    git diff -z --name-only "$from" "$to" | tr '\0' '\n'
    exit "${PIPESTATUS[0]}"
    ;;
  running)
    dir="$(state_dir)" || exit 1
    running "$dir"
    ;;
  *)
    echo "usage: scripts/gate/state.sh tree|record [tree]|changed|running" >&2
    exit 2
    ;;
esac
