# Azure Storage Account for Weekly Summary Images
# Stores DALL-E 3 generated images for weekly summaries

resource "azurerm_storage_account" "images" {
  count = var.enable_image_generation ? 1 : 0

  name                     = "${replace(var.project_name, "-", "")}images"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"

  # Enable blob public access for serving images
  allow_nested_items_to_be_public = true

  # Security settings
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  shared_access_key_enabled       = true
  public_network_access_enabled   = true

  blob_properties {
    cors_rule {
      allowed_headers    = ["*"]
      allowed_methods    = ["GET", "HEAD"]
      allowed_origins    = ["*"]
      exposed_headers    = ["*"]
      max_age_in_seconds = 3600
    }
  }

  tags = local.common_tags
}

# Blob Container for weekly summary images
resource "azurerm_storage_container" "weekly_summary_images" {
  count = var.enable_image_generation ? 1 : 0

  name                  = var.image_container_name
  storage_account_id    = azurerm_storage_account.images[0].id
  container_access_type = "blob" # Public read access for blobs
}

# Outputs
output "storage_account_name" {
  description = "Storage Account name for images"
  value       = var.enable_image_generation ? azurerm_storage_account.images[0].name : null
}

output "storage_account_primary_connection_string" {
  description = "Storage Account primary connection string"
  value       = var.enable_image_generation ? azurerm_storage_account.images[0].primary_connection_string : null
  sensitive   = true
}

output "storage_account_primary_blob_endpoint" {
  description = "Storage Account primary blob endpoint"
  value       = var.enable_image_generation ? azurerm_storage_account.images[0].primary_blob_endpoint : null
}

output "image_container_name" {
  description = "Blob container name for weekly summary images"
  value       = var.enable_image_generation ? azurerm_storage_container.weekly_summary_images[0].name : null
}
