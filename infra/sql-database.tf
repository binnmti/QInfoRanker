# Azure SQL Database (Free Tier) with Full-Text Search

# SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = "qinforanker-sql"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password

  # Enable Azure AD authentication
  azuread_administrator {
    login_username = "AzureAD Admin"
    object_id      = data.azurerm_client_config.current.object_id
  }

  # Minimum TLS version for security
  minimum_tls_version = "1.2"

  tags = local.common_tags
}

# SQL Database - General Purpose Serverless
# Serverless: 使用量に応じた課金、auto_pause無効化でコールドスタートを防止
resource "azurerm_mssql_database" "main" {
  name                        = "qinforanker-db"
  server_id                   = azurerm_mssql_server.main.id
  collation                   = "Japanese_CI_AS" # Japanese collation for proper sorting
  max_size_gb                 = 32               # Maximum storage
  sku_name                    = "GP_S_Gen5_1"    # General Purpose Serverless Gen5 1 vCore
  min_capacity                = 0.5              # Minimum vCores when active
  auto_pause_delay_in_minutes = -1               # 自動停止を無効化してコールドスタートを防止
  zone_redundant              = false            # Not available for serverless tier

  # Short-term backup retention
  short_term_retention_policy {
    retention_days = 7
  }

  tags = local.common_tags
}

# Firewall rule: Allow Azure services
resource "azurerm_mssql_firewall_rule" "allow_azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Firewall rule: Allow current client IP (for development)
# Note: In production, consider using Private Endpoints instead
resource "azurerm_mssql_firewall_rule" "allow_client_ip" {
  count            = var.environment == "dev" ? 1 : 0
  name             = "AllowClientIP"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0" # Replace with actual IP in production
  end_ip_address   = "255.255.255.255"
}

# Output connection strings
output "sql_server_fqdn" {
  description = "SQL Server fully qualified domain name"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "SQL Database name"
  value       = azurerm_mssql_database.main.name
}

output "sql_connection_string" {
  description = "SQL Database connection string (without password)"
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  sensitive   = true
}
