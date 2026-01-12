# Azure App Service for Blazor Server Application

# App Service Plan (Basic B1)
resource "azurerm_service_plan" "main" {
  name                = "qinforanker-plan"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku

  tags = local.common_tags
}

# App Service (Web App)
resource "azurerm_linux_web_app" "main" {
  name                = "qinforanker"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id

  # Enable HTTPS only
  https_only = true

  # Site configuration
  site_config {
    # .NET 8 runtime
    application_stack {
      dotnet_version = "8.0"
    }

    # Always On for SignalR stability
    always_on = true

    # WebSocket support for SignalR
    websockets_enabled = true

    # Health check
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 5

    # Minimum TLS version
    minimum_tls_version = "1.2"

    # CORS settings
    cors {
      allowed_origins     = ["https://*.azurewebsites.net"]
      support_credentials = true
    }
  }

  # Application settings
  # Key Vault URI を設定すると、アプリが Key Vault から直接シークレットを取得
  app_settings = {
    # Key Vault configuration - secrets are loaded automatically
    "KeyVault__Uri" = azurerm_key_vault.main.vault_uri

    # Non-secret settings
    "SeedSampleData"  = "false"
    "ASPNETCORE_URLS" = "http://+:8080"

    # Scoring settings (non-sensitive)
    "Scoring__Preset"                          = "QualityFocused"
    "Scoring__EnsembleRelevanceThreshold"      = "6"
    "AzureOpenAI__DeploymentName"              = var.filtering_model
    "BatchScoring__FilteringPreset"            = "Normal"
    "BatchScoring__Filtering__DeploymentName"  = var.filtering_model
    "EnsembleScoring__DeploymentName"          = var.ensemble_model
    "EnsembleScoring__BatchSize"               = "5"
    "WeeklySummary__DeploymentName"            = var.ensemble_model

    # Authentication settings (for Easy Auth)
    "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET" = azuread_application_password.webapp.value

    # Azure AD authentication settings (for app-level auth)
    "AzureAd__ClientId"    = azuread_application.webapp.client_id
    "AzureAd__TenantId"    = data.azurerm_client_config.current.tenant_id
    "AzureAd__Instance"    = "https://login.microsoftonline.com/"
    "AzureAd__CallbackPath" = "/signin-oidc"

    # Database settings
    "UseSqlite"                          = "false"
    "ConnectionStrings__DefaultConnection" = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    # Azure OpenAI settings
    "AzureOpenAI__Endpoint" = var.openai_endpoint
    "AzureOpenAI__ApiKey"   = var.openai_api_key
  }

  # Managed Identity for Key Vault access
  identity {
    type = "SystemAssigned"
  }

  # Logs configuration
  logs {
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
    application_logs {
      file_system_level = "Information"
    }
  }

  # Azure Easy Auth (Entra ID)
  auth_settings_v2 {
    auth_enabled           = var.enable_authentication
    require_authentication = false                # Allow anonymous for /top10
    unauthenticated_action = "AllowAnonymous"     # App controls which pages need auth

    # Login settings
    login {
      token_store_enabled = true
    }

    # Microsoft Entra ID (Azure AD) provider
    active_directory_v2 {
      client_id                  = azuread_application.webapp.client_id
      tenant_auth_endpoint       = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/v2.0"
      client_secret_setting_name = "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET"

      # Only allow your tenant
      allowed_audiences = [
        azuread_application.webapp.client_id
      ]
    }
  }

  tags = local.common_tags
}

# Grant Web App access to Key Vault
resource "azurerm_key_vault_access_policy" "webapp" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_linux_web_app.main.identity[0].principal_id

  secret_permissions = [
    "Get",
    "List"
  ]
}

# Output
output "webapp_url" {
  description = "Web App URL"
  value       = "https://${azurerm_linux_web_app.main.default_hostname}"
}

output "webapp_name" {
  description = "Web App name"
  value       = azurerm_linux_web_app.main.name
}

output "webapp_principal_id" {
  description = "Web App Managed Identity Principal ID"
  value       = azurerm_linux_web_app.main.identity[0].principal_id
}
