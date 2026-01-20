# Azure Key Vault for secrets management

resource "azurerm_key_vault" "main" {
  name                       = "qinforanker-kv"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = false # Allow purge for dev/test
  rbac_authorization_enabled = false

  tags = local.common_tags
}

# Access policy for current user/service principal (Terraform & local dev)
resource "azurerm_key_vault_access_policy" "terraform" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  secret_permissions = [
    "Get",
    "List",
    "Set",
    "Delete",
    "Recover",
    "Backup",
    "Restore",
    "Purge"
  ]
}

# Secret names use "--" separator for .NET configuration mapping
# "AzureOpenAI--Endpoint" -> config["AzureOpenAI:Endpoint"]

# SQL Connection String secret
resource "azurerm_key_vault_secret" "sql_connection_string" {
  name         = "ConnectionStrings--DefaultConnection"
  value        = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Azure OpenAI Endpoint secret
resource "azurerm_key_vault_secret" "openai_endpoint" {
  name         = "AzureOpenAI--Endpoint"
  value        = azurerm_cognitive_account.openai.endpoint
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Azure OpenAI API Key secret
resource "azurerm_key_vault_secret" "openai_api_key" {
  name         = "AzureOpenAI--ApiKey"
  value        = azurerm_cognitive_account.openai.primary_access_key
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# UseSqlite flag (false for Azure)
resource "azurerm_key_vault_secret" "use_sqlite" {
  name         = "UseSqlite"
  value        = "false"
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Image generation secrets (created when enable_image_generation = true)

# Blob Storage Connection String secret
resource "azurerm_key_vault_secret" "blob_storage_connection_string" {
  count = var.enable_image_generation ? 1 : 0

  name         = "BlobStorage--ConnectionString"
  value        = azurerm_storage_account.images[0].primary_connection_string
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Blob Storage Container Name secret
resource "azurerm_key_vault_secret" "blob_storage_container_name" {
  count = var.enable_image_generation ? 1 : 0

  name         = "BlobStorage--ContainerName"
  value        = azurerm_storage_container.weekly_summary_images[0].name
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Image Generation Endpoint (separate Azure Foundry endpoint, e.g., swedencentral)
resource "azurerm_key_vault_secret" "image_generation_endpoint" {
  count = var.enable_image_generation && var.image_generation_endpoint != "" ? 1 : 0

  name         = "ImageGeneration--Endpoint"
  value        = var.image_generation_endpoint
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Image Generation API Key
resource "azurerm_key_vault_secret" "image_generation_api_key" {
  count = var.enable_image_generation && var.image_generation_api_key != "" ? 1 : 0

  name         = "ImageGeneration--ApiKey"
  value        = var.image_generation_api_key
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Image Generation Deployment Name
resource "azurerm_key_vault_secret" "image_generation_deployment_name" {
  count = var.enable_image_generation ? 1 : 0

  name         = "ImageGeneration--DeploymentName"
  value        = var.dalle3_model
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Image Generation Enabled flag
resource "azurerm_key_vault_secret" "image_generation_enabled" {
  count = var.enable_image_generation ? 1 : 0

  name         = "ImageGeneration--Enabled"
  value        = tostring(var.enable_image_generation)
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.terraform]
}

# Output
output "key_vault_name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.main.vault_uri
}
