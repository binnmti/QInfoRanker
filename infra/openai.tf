# Azure OpenAI (Cognitive Services) for AI scoring

resource "azurerm_cognitive_account" "openai" {
  name                  = "QInfoRankerAI"
  location              = var.openai_location
  resource_group_name   = azurerm_resource_group.main.name
  kind                  = "OpenAI"
  sku_name              = "S0"
  custom_subdomain_name = "qinforankerai"

  network_acls {
    default_action = "Allow"
  }

  tags = local.common_tags
}

# Model deployment: gpt-5-nano (for filtering)
resource "azurerm_cognitive_deployment" "filtering" {
  name                 = var.filtering_model
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = "gpt-4o-mini" # Actual model name (gpt-5-nano is deployment name)
    version = "2024-07-18"
  }

  sku {
    name     = "GlobalStandard"
    capacity = 30 # TPM in thousands
  }
}

# Model deployment: o3-mini (for ensemble scoring)
resource "azurerm_cognitive_deployment" "ensemble" {
  name                 = var.ensemble_model
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = "o3-mini"
    version = "2025-01-31"
  }

  sku {
    name     = "GlobalStandard"
    capacity = 30 # TPM in thousands
  }
}

# Outputs
output "openai_endpoint" {
  description = "Azure OpenAI endpoint URL"
  value       = azurerm_cognitive_account.openai.endpoint
}

output "openai_key" {
  description = "Azure OpenAI primary key"
  value       = azurerm_cognitive_account.openai.primary_access_key
  sensitive   = true
}

output "filtering_deployment_name" {
  description = "Filtering model deployment name"
  value       = azurerm_cognitive_deployment.filtering.name
}

output "ensemble_deployment_name" {
  description = "Ensemble model deployment name"
  value       = azurerm_cognitive_deployment.ensemble.name
}
