#!/usr/bin/env bash
# Creates on GitHub the labels `.github/labels.json` declares and the repository does not have yet, so an
# issue a session or CI files with one never fails, or invents it, for want of the label. It only adds: a
# label that exists keeps its colour and its description, and one the file does not name is left alone.
#
#   ensure-labels.sh [<labels.json>]
#
# Inputs (environment): GH_TOKEN, GH_REPO. Needs `issues: write`.
set -euo pipefail

file="${1:-.github/labels.json}"

# GitHub compares label names without case, so the comparison does too.
existing="$(gh label list --limit 1000 --json name | jq -r '.[].name | ascii_downcase')"

created=0
while IFS=$'\t' read -r name description; do
  if grep -qxF -- "${name,,}" <<<"$existing"; then
    continue
  fi
  gh label create "$name" --description "$description"
  created=$((created + 1))
done < <(jq -r 'to_entries[] | [.key, .value] | @tsv' "$file")

echo "Labels created: ${created}."
