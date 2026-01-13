# QInfoRanker Azure Infrastructure
# Terraform configuration for deploying the application to Azure

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
  }

  # Optional: Configure backend for state storage
  # backend "azurerm" {
  #   resource_group_name  = "tfstate"
  #   storage_account_name = "tfstateqinforanker"
  #   container_name       = "tfstate"
  #   key                  = "qinforanker.tfstate"
  # }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
    cognitive_account {
      purge_soft_delete_on_destroy = true
    }
  }
  subscription_id = var.subscription_id
}

# Azure AD provider for app registration
provider "azuread" {
  tenant_id = data.azurerm_client_config.current.tenant_id
}

# Data source for current Azure configuration
data "azurerm_client_config" "current" {}

locals {
  # Simple naming - use project_name directly
  project = var.project_name

  # Common tags for all resources
  common_tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}
