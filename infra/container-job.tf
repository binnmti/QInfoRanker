# Azure Container Apps Job for scheduled collection

# Container Registry for storing worker images
resource "azurerm_container_registry" "main" {
  name                = var.acr_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = true

  tags = local.common_tags
}

# Log Analytics Workspace for Container Apps
resource "azurerm_log_analytics_workspace" "main" {
  name                = "qinforanker-log"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = local.common_tags
}

# Container Apps Environment
resource "azurerm_container_app_environment" "main" {
  name                       = "qinforanker-cae"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  tags = local.common_tags
}

# Container Apps Job - Scheduled Collection
resource "azurerm_container_app_job" "collection" {
  name                         = "qinforanker-job"
  location                     = azurerm_resource_group.main.location
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id

  replica_timeout_in_seconds = 7200 # 2 hours max (initial run may take longer)
  replica_retry_limit        = 1

  # Scheduled trigger
  schedule_trigger_config {
    cron_expression          = var.collection_schedule
    parallelism              = 1
    replica_completion_count = 1
  }

  # Container template
  template {
    container {
      name   = "worker"
      image  = var.worker_image != "" ? var.worker_image : "${azurerm_container_registry.main.login_server}/qinforanker-worker:latest"
      cpu    = 0.5
      memory = "1Gi"

      # Environment variables
      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;Command Timeout=120;"
      }

      env {
        name  = "AzureOpenAI__Endpoint"
        value = azurerm_cognitive_account.openai.endpoint
      }

      env {
        name        = "AzureOpenAI__ApiKey"
        secret_name = "openai-api-key"
      }

      env {
        name  = "UseSqlite"
        value = "false"
      }

      env {
        name  = "Scoring__Preset"
        value = "QualityFocused"
      }

      env {
        name  = "BatchScoring__FilteringPreset"
        value = "Normal"
      }

      env {
        name  = "BatchScoring__Filtering__DeploymentName"
        value = var.filtering_model
      }

      env {
        name  = "EnsembleScoring__DeploymentName"
        value = var.ensemble_model
      }

      env {
        name  = "EnsembleScoring__BatchSize"
        value = "5"
      }

      env {
        name  = "Scoring__EnsembleRelevanceThreshold"
        value = "6"
      }

      env {
        name  = "AzureOpenAI__DeploymentName"
        value = var.filtering_model
      }
    }
  }

  # Secrets
  secret {
    name  = "openai-api-key"
    value = azurerm_cognitive_account.openai.primary_access_key
  }

  # Registry credentials
  registry {
    server               = azurerm_container_registry.main.login_server
    username             = azurerm_container_registry.main.admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = azurerm_container_registry.main.admin_password
  }

  tags = local.common_tags
}

# Outputs
output "container_registry_login_server" {
  description = "Container Registry login server"
  value       = azurerm_container_registry.main.login_server
}

output "container_registry_admin_username" {
  description = "Container Registry admin username"
  value       = azurerm_container_registry.main.admin_username
}

output "container_registry_admin_password" {
  description = "Container Registry admin password"
  value       = azurerm_container_registry.main.admin_password
  sensitive   = true
}

output "container_app_job_name" {
  description = "Container Apps Job name"
  value       = azurerm_container_app_job.collection.name
}
