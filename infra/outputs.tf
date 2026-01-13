# Outputs for QInfoRanker Azure Infrastructure

output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "resource_group_location" {
  description = "The location of the resource group"
  value       = azurerm_resource_group.main.location
}

# Deployment summary
output "deployment_summary" {
  description = "Summary of deployed resources"
  value = {
    web_app_url       = "https://${azurerm_linux_web_app.main.default_hostname}"
    sql_server_fqdn   = azurerm_mssql_server.main.fully_qualified_domain_name
    sql_database      = azurerm_mssql_database.main.name
    key_vault_uri     = azurerm_key_vault.main.vault_uri
    acr_server        = azurerm_container_registry.main.login_server
    openai_endpoint   = azurerm_cognitive_account.openai.endpoint
    filtering_model   = azurerm_cognitive_deployment.filtering.name
    ensemble_model    = azurerm_cognitive_deployment.ensemble.name
    job_schedule      = var.collection_schedule
  }
}

# Docker push commands
output "docker_push_commands" {
  description = "Commands to build and push the worker image"
  value       = <<-EOT
    # Login to Azure Container Registry
    az acr login --name ${azurerm_container_registry.main.name}

    # Build and push the worker image
    docker build -t ${azurerm_container_registry.main.login_server}/qinforanker-worker:latest -f src/QInfoRanker.Worker/Dockerfile .
    docker push ${azurerm_container_registry.main.login_server}/qinforanker-worker:latest
  EOT
}
