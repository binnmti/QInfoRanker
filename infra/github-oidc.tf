# GitHub Actions OIDC Authentication
# Enables passwordless authentication from GitHub Actions to Azure

# Azure AD Application for GitHub Actions
resource "azuread_application" "github_actions" {
  display_name = "GitHub Actions - ${var.project_name}"
}

# Service Principal for GitHub Actions
resource "azuread_service_principal" "github_actions" {
  client_id = azuread_application.github_actions.client_id
}

# Federated Credential for GitHub Actions (main branch)
resource "azuread_application_federated_identity_credential" "github_main" {
  application_id = azuread_application.github_actions.id
  display_name   = "github-main-branch"
  description    = "GitHub Actions deployment from main branch"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_owner}/${var.github_repo}:ref:refs/heads/main"
}

# Federated Credential for GitHub Actions (production environment)
resource "azuread_application_federated_identity_credential" "github_production" {
  application_id = azuread_application.github_actions.id
  display_name   = "github-production-env"
  description    = "GitHub Actions deployment from production environment"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_owner}/${var.github_repo}:environment:production"
}

# Federated Credential for GitHub Actions (Pull Requests)
resource "azuread_application_federated_identity_credential" "github_pr" {
  application_id = azuread_application.github_actions.id
  display_name   = "github-pull-request"
  description    = "GitHub Actions for pull requests"
  audiences      = ["api://AzureADTokenExchange"]
  issuer         = "https://token.actions.githubusercontent.com"
  subject        = "repo:${var.github_owner}/${var.github_repo}:pull_request"
}

# Role Assignment: Contributor on Resource Group
resource "azurerm_role_assignment" "github_contributor" {
  scope                = azurerm_resource_group.main.id
  role_definition_name = "Contributor"
  principal_id         = azuread_service_principal.github_actions.object_id
}

# Role Assignment: AcrPush for Container Registry
resource "azurerm_role_assignment" "github_acr_push" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPush"
  principal_id         = azuread_service_principal.github_actions.object_id
}

# Role Assignment: Website Contributor for App Service deployment
resource "azurerm_role_assignment" "github_website_contributor" {
  scope                = azurerm_linux_web_app.main.id
  role_definition_name = "Website Contributor"
  principal_id         = azuread_service_principal.github_actions.object_id
}

# Outputs for GitHub Secrets
output "github_actions_client_id" {
  description = "Client ID for GitHub Actions OIDC (set as AZURE_CLIENT_ID secret)"
  value       = azuread_application.github_actions.client_id
}

output "github_actions_tenant_id" {
  description = "Tenant ID for GitHub Actions OIDC (set as AZURE_TENANT_ID secret)"
  value       = data.azurerm_client_config.current.tenant_id
}

output "github_actions_subscription_id" {
  description = "Subscription ID for GitHub Actions OIDC (set as AZURE_SUBSCRIPTION_ID secret)"
  value       = data.azurerm_client_config.current.subscription_id
}
