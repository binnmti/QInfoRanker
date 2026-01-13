# Azure Easy Auth (Entra ID Authentication)
# This file configures authentication for the App Service

# Azure AD Application Registration
resource "azuread_application" "webapp" {
  display_name = "QInfoRanker Web App"

  # Web app redirect URIs
  web {
    redirect_uris = [
      "https://qinforanker.azurewebsites.net/.auth/login/aad/callback",
      "https://qinforanker.azurewebsites.net/signin-oidc"
    ]
    implicit_grant {
      id_token_issuance_enabled = true
    }
  }

  # Required permissions
  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000" # Microsoft Graph

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
  }
}

# Service Principal for the application
resource "azuread_service_principal" "webapp" {
  client_id = azuread_application.webapp.client_id
}

# Application Password (Client Secret)
resource "azuread_application_password" "webapp" {
  application_id    = azuread_application.webapp.id
  display_name      = "Terraform managed"
  end_date_relative = "8760h" # 1 year
}

# Store client secret in Key Vault
resource "azurerm_key_vault_secret" "auth_client_secret" {
  name         = "Auth--ClientSecret"
  value        = azuread_application_password.webapp.value
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Output
output "auth_client_id" {
  description = "Application (client) ID for authentication"
  value       = azuread_application.webapp.client_id
}

output "auth_tenant_id" {
  description = "Tenant ID for authentication"
  value       = data.azurerm_client_config.current.tenant_id
}
