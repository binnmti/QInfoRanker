# Resource Group for QInfoRanker

resource "azurerm_resource_group" "main" {
  name     = "QInfoRanker"
  location = var.location

  tags = local.common_tags
}
