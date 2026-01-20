# Variables for QInfoRanker Azure Infrastructure

variable "subscription_id" {
  description = "Azure Subscription ID"
  type        = string
}

variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
  default     = "qinforanker"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "japaneast"
}

# SQL Database settings
variable "sql_admin_login" {
  description = "SQL Server administrator login name"
  type        = string
  default     = "qinforankeradmin"
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}

# Azure OpenAI settings
variable "openai_location" {
  description = "Azure region for OpenAI (some models are only available in specific regions)"
  type        = string
  default     = "eastus2"
}

variable "openai_endpoint" {
  description = "Azure OpenAI endpoint URL (optional if not deploying App Service)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "openai_api_key" {
  description = "Azure OpenAI API key (optional if not deploying App Service)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "filtering_model" {
  description = "Model deployment name for filtering stage"
  type        = string
  default     = "gpt-5-nano"
}

variable "ensemble_model" {
  description = "Model deployment name for ensemble scoring"
  type        = string
  default     = "o3-mini"
}

variable "dalle3_model" {
  description = "Model deployment name for DALL-E 3 image generation"
  type        = string
  default     = "dall-e-3"
}

# Image Generation settings
variable "image_container_name" {
  description = "Blob container name for weekly summary images"
  type        = string
  default     = "weekly-summary-images"
}

variable "enable_image_generation" {
  description = "Enable DALL-E 3 image generation for weekly summaries"
  type        = bool
  default     = true
}

# Image Generation endpoint (separate from main OpenAI, e.g., swedencentral Foundry)
variable "image_generation_endpoint" {
  description = "Azure OpenAI/Foundry endpoint for DALL-E 3 image generation (if different from main endpoint)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "image_generation_api_key" {
  description = "API key for DALL-E 3 image generation endpoint"
  type        = string
  sensitive   = true
  default     = ""
}

# Container Apps Job settings
variable "collection_schedule" {
  description = "Cron expression for collection job schedule (UTC)"
  type        = string
  default     = "0 21 * * *" # 06:00 JST (21:00 UTC previous day)
}

variable "worker_image" {
  description = "Docker image for the worker container"
  type        = string
  default     = "" # Will be set after building the image
}

# App Service settings
variable "app_service_sku" {
  description = "App Service Plan SKU"
  type        = string
  default     = "B1"

  validation {
    condition     = contains(["F1", "B1", "B2", "B3", "S1", "S2", "S3"], var.app_service_sku)
    error_message = "App Service SKU must be a valid tier."
  }
}

# Container Registry settings
variable "acr_name" {
  description = "Azure Container Registry name (must be globally unique, alphanumeric only)"
  type        = string
  default     = "qinforankeracr"
}

# Optional: Custom domain
variable "custom_domain" {
  description = "Custom domain for the web app (optional)"
  type        = string
  default     = ""
}

# Authentication settings
variable "enable_authentication" {
  description = "Enable Azure AD authentication"
  type        = bool
  default     = true
}

variable "allowed_external_redirect_urls" {
  description = "Allowed external redirect URLs for authentication"
  type        = list(string)
  default     = []
}

# GitHub OIDC settings
variable "github_owner" {
  description = "GitHub repository owner (username or organization)"
  type        = string
  default     = "binnmti"
}

variable "github_repo" {
  description = "GitHub repository name"
  type        = string
  default     = "QInfoRanker"
}
