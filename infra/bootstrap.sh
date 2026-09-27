#!/usr/bin/env bash
# The one-time setup the hosted table needs before .github/workflows/deploy.yml can deploy it (ADR 0082),
# run by the subscription's owner in Azure Cloud Shell (or anywhere `az login` has been run as them).
#
#   infra/bootstrap.sh setup      # the resource group, the deploying identity, the sign-in registration
#   infra/bootstrap.sh redirect   # after the first deployment: the sign-in's redirect URI
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
SIGN_IN_NAME="${PREFIX}-table-signin"
APP_NAME="${PREFIX}-table"
ENVIRONMENT="azure"

# The template's own bounds, and a storage account's alphabet: its name is the prefix and a hash.
if [[ ! "$PREFIX" =~ ^[a-z0-9]{3,11}$ ]]; then
  echo "PREFIX must be 3 to 11 lowercase letters or digits; '$PREFIX' is not." >&2
  exit 2
fi

# The one role infra/main.bicep assigns: Storage Blob Data Contributor, to the app's own identity.
BLOB_CONTRIBUTOR="ba92f5b4-2d11-453d-a403-e96b0029c9fe"

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
  local subscription tenant scope deployer deployer_principal sign_in operator condition subject

  subscription="$(az account show --query id --output tsv)"
  tenant="$(az account show --query tenantId --output tsv)"
  echo "Subscription $subscription, tenant $tenant, $RESOURCE_GROUP in $LOCATION, for $REPO."

  # The providers a fresh subscription has not registered yet: nothing deploys without them.
  local namespace
  for namespace in Microsoft.App Microsoft.OperationalInsights Microsoft.Storage; do
    az provider register --namespace "$namespace" --wait --output none
  done

  az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
  scope="/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP"

  # The deploying identity: trusted for this repository's `azure` environment and nothing else, and allowed
  # to change this one resource group. It may assign exactly one role, the one the template assigns.
  deployer="$(registration "$DEPLOYER_NAME")"
  deployer_principal="$(principal "$deployer")"
  subject="repo:${REPO}:environment:${ENVIRONMENT}"
  if [[ -z "$(az ad app federated-credential list --id "$deployer" --query "[?subject=='$subject'].name" --output tsv)" ]]; then
    az ad app federated-credential create --id "$deployer" --output none --parameters "{
      \"name\": \"github-${ENVIRONMENT}\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"$subject\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }"
  fi
  grant "$deployer_principal" Contributor "$scope"
  condition="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$BLOB_CONTRIBUTOR})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {$BLOB_CONTRIBUTOR}))"
  grant "$deployer_principal" "Role Based Access Control Administrator" "$scope" \
    --condition "$condition" --condition-version 2.0

  # The registration the platform signs the operator in with. No client secret: the platform uses the
  # implicit flow, which needs ID tokens issued, and only the people listed may hold a session (ADR 0082).
  sign_in="$(registration "$SIGN_IN_NAME" --enable-id-token-issuance true)"
  principal "$sign_in" >/dev/null
  operator="$(az ad signed-in-user show --query id --output tsv)"

  echo
  echo "Repository variables (Settings > Secrets and variables > Actions > Variables):"
  variable AZURE_CLIENT_ID "$deployer"
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

  uri="https://$fqdn/.auth/login/aad/callback"
  az ad app update --id "$sign_in" --web-redirect-uris "$uri" --enable-id-token-issuance true --output none
  echo "Sign-in redirects to $uri. The lobby is https://$fqdn/lobby."
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
