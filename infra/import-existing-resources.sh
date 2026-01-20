#!/bin/bash
# Import existing Azure resources into Terraform state
# Run this script from the infra directory

SUBSCRIPTION_ID="6ffdd37c-9b89-4f4c-889e-56017146b636"
RESOURCE_GROUP="QInfoRanker"

echo "Importing existing Azure resources into Terraform state..."

# Resource Group
terraform import azurerm_resource_group.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP

# App Service Plan
terraform import azurerm_service_plan.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/serverFarms/qinforanker-plan

# Container Registry
terraform import azurerm_container_registry.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerRegistry/registries/qinforankeracr

# Log Analytics Workspace
terraform import azurerm_log_analytics_workspace.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.OperationalInsights/workspaces/qinforanker-log

# Key Vault
terraform import azurerm_key_vault.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/qinforanker-kv

# Azure OpenAI (Cognitive Account)
terraform import azurerm_cognitive_account.openai /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/QInfoRankerAI

# SQL Server
terraform import azurerm_mssql_server.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Sql/servers/qinforanker-sql

# SQL Database (assuming it exists)
terraform import azurerm_mssql_database.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Sql/servers/qinforanker-sql/databases/QInfoRankerDb

# Web App (assuming it exists)
terraform import azurerm_linux_web_app.main /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/qinforanker-web

echo "Import complete. Run 'terraform plan' to verify."
