terraform {
  required_version = ">= 1.0"

  backend "azurerm" {
    resource_group_name  = "ats-v11-prod-rg"
    storage_account_name = "atsv11prodst"
    container_name       = "tfstate"
    key                  = "against-the-spread-v1.1.tfstate"
  }

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# Variables
variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "against-the-spread"
}

variable "environment" {
  description = "Environment (dev, staging, prod)"
  type        = string
  default     = "prod"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "google_client_id" {
  description = "Public Google Identity Services OAuth client ID"
  type        = string
  default     = "520517828773-09fud86es46rrj48bosc2g5de1ubk46i.apps.googleusercontent.com"
}

variable "admin_emails" {
  description = "Comma-separated Google accounts allowed to use admin APIs"
  type        = string
  default     = "bengrossm@gmail.com"
}

# Locals
locals {
  resource_prefix = "${var.project_name}-${var.environment}"
  tags = {
    Project     = var.project_name
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = "${local.resource_prefix}-rg"
  location = var.location
  tags     = local.tags
}

# Storage Account for game files and function storage
resource "azurerm_storage_account" "main" {
  name                            = replace("${local.resource_prefix}st", "-", "")
  resource_group_name             = azurerm_resource_group.main.name
  location                        = azurerm_resource_group.main.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  allow_nested_items_to_be_public = false

  tags = local.tags
}

# Container for game files (lines)
resource "azurerm_storage_container" "gamefiles" {
  name                  = "gamefiles"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

# Container for durable Terraform state
resource "azurerm_storage_container" "tfstate" {
  name                  = "tfstate"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private"
}

# Application Insights for monitoring
resource "azurerm_application_insights" "main" {
  name                = "${local.resource_prefix}-ai"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"
  tags                = local.tags

  # Azure may attach a managed Log Analytics workspace automatically.
  lifecycle {
    ignore_changes = [workspace_id]
  }
}

# Static Web App for Blazor and its managed Azure Functions API
resource "azurerm_static_web_app" "main" {
  name                = "${local.resource_prefix}-web"
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  sku_tier            = "Free"
  sku_size            = "Free"

  app_settings = {
    AZURE_STORAGE_CONNECTION_STRING       = azurerm_storage_account.main.primary_connection_string
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
    GOOGLE_CLIENT_ID                      = var.google_client_id
    ADMIN_EMAILS                          = var.admin_emails
  }

  tags = local.tags
}

# Outputs
output "resource_group_name" {
  value       = azurerm_resource_group.main.name
  description = "Resource group name"
}

output "storage_account_name" {
  value       = azurerm_storage_account.main.name
  description = "Storage account name for admin uploads"
}

output "storage_connection_string" {
  value       = azurerm_storage_account.main.primary_connection_string
  description = "Storage connection string (sensitive)"
  sensitive   = true
}

output "static_web_app_url" {
  value       = "https://${azurerm_static_web_app.main.default_host_name}"
  description = "Static Web App URL"
}

output "application_insights_connection_string" {
  value       = azurerm_application_insights.main.connection_string
  description = "Application Insights connection string"
  sensitive   = true
}

output "static_web_app_deployment_token" {
  value       = azurerm_static_web_app.main.api_key
  description = "Deployment token for Static Web App"
  sensitive   = true
}
