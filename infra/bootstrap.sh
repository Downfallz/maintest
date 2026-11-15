#!/usr/bin/env bash
# The one-time setup the hosted table needs before .github/workflows/deploy.yml can deploy it (ADR 0082),
# run by the subscription's owner in Azure Cloud Shell (or anywhere `az login` has been run as them).
#
#   infra/bootstrap.sh setup      # the resource group, the deploying identity, the reading identity, the sign-in registration
#   infra/bootstrap.sh redirect   # after the first deployment: the sign-in's redirect URIs (DOMAIN=<host> adds the table's own)
#
# Safe to run again: everything it makes it looks for first. It creates no secret. What it prints at the
# end are ids, which the workflow reads as repository variables; with the GitHub CLI signed in, it sets them.
#
# Settings (environment): REPO (default Downfallz/maintest), LOCATION (default canadacentral),
# RESOURCE_GROUP (default downfall-table), PREFIX (default downfall). PREFIX begins every resource's name;
# it is handed to the workflow as a repository variable, which hands it to infra/main.bicep as namePrefix, so
# the names this script looks for are the names the template makes.
set -euo pipefail

REPO="${REPO:-Downfallz/maintest}"
LOCATION="${LOCATION:-canadacentral}"
RESOURCE_GROUP="${RESOURCE_GROUP:-downfall-table}"
PREFIX="${PREFIX:-downfall}"
DEPLOYER_NAME="${PREFIX}-table-deploy"
OPS_NAME="${PREFIX}-table-ops"
SIGN_IN_NAME="${PREFIX}-table-signin"
APP_NAME="${PREFIX}-table"
ENVIRONMENT="azure"
# The environment ops-report.yml runs in (ADR 0185): the reading identity trusts it and nothing else.
OPS_ENVIRONMENT="ops"

# The template's own bounds, and a storage account's alphabet: its name is the prefix and a hash.
if [[ ! "$PREFIX" =~ ^[a-z0-9]{3,11}$ ]]; then
  echo "PREFIX must be 3 to 11 lowercase letters or digits; '$PREFIX' is not." >&2
  exit 2
fi

# The roles infra/main.bicep assigns to the identities of the API, the worker and the job: Storage Blob Data
# Contributor for the recordings, Storage Table Data Contributor for the index of the tables (ADR 0161),
# Storage Queue Data Contributor for the queues the bots' turns go through (ADR 0162), and Web PubSub Service
# Owner on the relay (ADR 0165).
BLOB_CONTRIBUTOR="ba92f5b4-2d11-453d-a403-e96b0029c9fe"
TABLE_CONTRIBUTOR="0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3"
QUEUE_CONTRIBUTOR="974c5e8b-45b9-4653-ba55-5f855dd0fb88"
# Web PubSub Service Owner on the relay (ADR 0165): each process says through it what moved, and the API signs the
# pages' addresses.
RELAY_OWNER="12cf5a90-567b-43ae-8102-96cf46c7d9b4"
# Log Analytics Reader on the workspace, for the identities of the log alerts (ADR 0185).
LOG_READER="73c42c96-874c-492b-b04d-ab87d138a893"

# What the reading identity holds on the resource group (ADR 0185): it reads the resources, their metrics, the
# console lines and the cost, and writes nothing. No data role: it reads no recording, index or queue message.
OPS_ROLES=("Reader" "Log Analytics Reader" "Cost Management Reader")

# The application id of the registration with this display name, created when there is none.
registration() {
  local name="$1"
  shift
  local id
  id="$(az ad app list --display-name "$name" --query "[0].appId" --output tsv)"
  if [[ -z "$id" ]]; then
    id="$(az ad app create --display-name "$name" --sign-in-audience AzureADMyOrg "$@" --query appId --output tsv)"
  fi
  echo "$id"
  return 0
}

# The object id of a registration's service principal, created when there is none.
principal() {
  local app_id="$1"
  local id
  id="$(az ad sp list --filter "appId eq '$app_id'" --query "[0].id" --output tsv)"
  if [[ -z "$id" ]]; then
    id="$(az ad sp create --id "$app_id" --query id --output tsv)"
  fi
  echo "$id"
  return 0
}

# A role held on a condition: granted with it, and, when the principal already holds the role there on another
# condition, that assignment replaced by one on this condition. A condition is not changed by granting the role
# again, so a bootstrap run over an earlier one would otherwise keep the earlier condition for good.
grant_on_condition() {
  local principal_id="$1" role="$2" scope="$3" condition="$4"
  local held id
  held="$(az role assignment list --assignee "$principal_id" --role "$role" --scope "$scope" --query "[0].condition" --output tsv)"
  id="$(az role assignment list --assignee "$principal_id" --role "$role" --scope "$scope" --query "[0].id" --output tsv)"
  if [[ -n "$id" && "$held" != "$condition" ]]; then
    az role assignment delete --ids "$id" --output none
  fi
  grant "$principal_id" "$role" "$scope" --condition "$condition" --condition-version 2.0
  return 0
}

# A registration trusted for one environment of this repository, unless it already is.
federate() {
  local app_id="$1" environment="$2"
  local subject="repo:${REPO}:environment:${environment}"
  if [[ -z "$(az ad app federated-credential list --id "$app_id" --query "[?subject=='$subject'].name" --output tsv)" ]]; then
    az ad app federated-credential create --id "$app_id" --output none --parameters "{
      \"name\": \"github-${environment}\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"$subject\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }"
  fi
  return 0
}

# A GitHub environment only `main` may deploy to, with the GitHub CLI when it is signed in (ADR 0185): a federated
# identity trusts an environment, and an environment with no branch rule is one any branch's workflow may name.
main_only_environment() {
  local environment="$1"
  if ! command -v gh >/dev/null 2>&1 || ! gh auth status >/dev/null 2>&1; then
    echo "  Restrict the '$environment' environment to main by hand (infra/runbooks/ops-identity.md, step 3)."
    return 0
  fi
  local by_hand="  Restrict the '$environment' environment to main by hand (infra/runbooks/ops-identity.md, step 3)."
  # A signed-in account that is not the repository's admin is refused: the rest of the setup goes on.
  gh api --method PUT "repos/$REPO/environments/$environment" --input - --silent \
    <<<'{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}' || { echo "$by_hand"; return 0; }
  local held
  held="$(gh api "repos/$REPO/environments/$environment/deployment-branch-policies" --jq '.branch_policies[] | select(.name == "main") | .name')" || { echo "$by_hand"; return 0; }
  if [[ -z "$held" ]]; then
    gh api --method POST "repos/$REPO/environments/$environment/deployment-branch-policies" -f name=main -f type=branch --silent || echo "$by_hand"
  fi
  return 0
}

# A role for a principal on a scope, unless it already holds it there.
grant() {
  local principal_id="$1" role="$2" scope="$3"
  shift 3
  local held
  held="$(az role assignment list --assignee "$principal_id" --role "$role" --scope "$scope" --query "length(@)" --output tsv)"
  if [[ "$held" == "0" ]]; then
    az role assignment create --assignee-object-id "$principal_id" --assignee-principal-type ServicePrincipal \
      --role "$role" --scope "$scope" "$@" --output none
  fi
  return 0
}

# A repository variable, set with the GitHub CLI when it is signed in, and printed either way.
variable() {
  local name="$1" value="$2"
  if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
    gh variable set "$name" --repo "$REPO" --body "$value"
  fi
  printf '  %-24s %s\n' "$name" "$value"
  return 0
}

setup() {
  local subscription tenant scope deployer deployer_principal ops ops_principal role sign_in operator condition

  subscription="$(az account show --query id --output tsv)"
  tenant="$(az account show --query tenantId --output tsv)"
  echo "Subscription $subscription, tenant $tenant, $RESOURCE_GROUP in $LOCATION, for $REPO."

  # The providers a fresh subscription has not registered yet: nothing deploys without them. Insights holds the
  # alerts, Consumption the budget and CostManagement the cost the report reads (ADR 0185).
  local namespace
  for namespace in Microsoft.App Microsoft.Network Microsoft.OperationalInsights Microsoft.SignalRService Microsoft.Storage \
    Microsoft.Insights Microsoft.Consumption Microsoft.CostManagement; do
    az provider register --namespace "$namespace" --wait --output none
  done

  az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
  scope="/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP"

  # The deploying identity: trusted for this repository's `azure` environment and nothing else, and allowed
  # to change this one resource group. It may assign the roles the template assigns, and no other.
  deployer="$(registration "$DEPLOYER_NAME")"
  deployer_principal="$(principal "$deployer")"
  federate "$deployer" "$ENVIRONMENT"
  grant "$deployer_principal" Contributor "$scope"
  condition="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$BLOB_CONTRIBUTOR, $TABLE_CONTRIBUTOR, $QUEUE_CONTRIBUTOR, $RELAY_OWNER, $LOG_READER})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$BLOB_CONTRIBUTOR, $TABLE_CONTRIBUTOR, $QUEUE_CONTRIBUTOR, $RELAY_OWNER, $LOG_READER}))"
  grant_on_condition "$deployer_principal" "Role Based Access Control Administrator" "$scope" "$condition"

  # The reading identity (ADR 0185): trusted for the `ops` environment alone, and allowed to read this one
  # resource group, its metrics, its logs and its cost.
  ops="$(registration "$OPS_NAME")"
  ops_principal="$(principal "$ops")"
  federate "$ops" "$OPS_ENVIRONMENT"
  for role in "${OPS_ROLES[@]}"; do
    grant "$ops_principal" "$role" "$scope"
  done
  main_only_environment "$OPS_ENVIRONMENT"

  # The registration the platform signs the operator in with. No client secret: the platform uses the
  # implicit flow, which needs ID tokens issued, and only the people listed may hold a session (ADR 0082).
  sign_in="$(registration "$SIGN_IN_NAME" --enable-id-token-issuance true)"
  principal "$sign_in" >/dev/null
  operator="$(az ad signed-in-user show --query id --output tsv)"

  echo
  echo "Repository variables (Settings > Secrets and variables > Actions > Variables):"
  variable AZURE_CLIENT_ID "$deployer"
  variable AZURE_OPS_CLIENT_ID "$ops"
  variable AZURE_TENANT_ID "$tenant"
  variable AZURE_SUBSCRIPTION_ID "$subscription"
  variable AZURE_RESOURCE_GROUP "$RESOURCE_GROUP"
  variable TABLE_SIGNIN_CLIENT_ID "$sign_in"
  variable TABLE_OPERATORS "$operator"
  variable TABLE_NAME_PREFIX "$PREFIX"
  echo
  echo "Next: run the 'Deploy the table' workflow, make the downfall-table package public, then run"
  echo "'infra/bootstrap.sh redirect' once (infra/README.md, steps 3 to 5)."
  return 0
}

redirect() {
  local fqdn sign_in uri
  fqdn="$(az resource show --resource-group "$RESOURCE_GROUP" --resource-type Microsoft.App/containerApps \
    --name "$APP_NAME" --query properties.configuration.ingress.fqdn --output tsv)"
  sign_in="$(az ad app list --display-name "$SIGN_IN_NAME" --query "[0].appId" --output tsv)"
  if [[ -z "$fqdn" || -z "$sign_in" ]]; then
    echo "Nothing to point at yet: run 'setup', then deploy once." >&2
    return 1
  fi

  # The platform's address, and the table's own domain when DOMAIN names it (ADR 0175): both stay listed, so the
  # admin panel signs in at either while the domain is brought in.
  local uris=("https://$fqdn/.auth/login/aad/callback")
  if [[ -n "${DOMAIN:-}" ]]; then
    uris+=("https://$DOMAIN/.auth/login/aad/callback")
  fi
  az ad app update --id "$sign_in" --web-redirect-uris "${uris[@]}" --enable-id-token-issuance true --output none
  echo "Sign-in redirects to ${uris[*]}. The lobby is https://${DOMAIN:-$fqdn}/."
  return 0
}

command="${1:-setup}"
case "$command" in
  setup) setup ;;
  redirect) redirect ;;
  *)
    echo "Usage: $0 [setup|redirect]" >&2
    exit 2
    ;;
esac
