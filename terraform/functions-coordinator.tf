#################### Functions ####################

resource "azurerm_function_app" "fa_coordinator" {
  name                       = "fa-${local.resource_name}-coordinator"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  app_service_plan_id        = azurerm_app_service_plan.asp.id 
  storage_account_name       = azurerm_storage_account.sa.name
  storage_account_access_key = azurerm_storage_account.sa.primary_access_key
  os_type                    = "linux"
  version                    = "~3"
  app_settings = {
    "AzureWebJobsStorage"                     = azurerm_storage_account.sa.primary_connection_string
    "FUNCTIONS_WORKER_RUNTIME"                = "dotnet"
    "StorageConnectionAppSetting"             = azurerm_storage_account.sa.primary_connection_string 
    "APPINSIGHTS_INSTRUMENTATIONKEY"          = azurerm_application_insights.ai.instrumentation_key
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE"     = ""
    "WEBSITE_ENABLE_SYNC_UPDATE_SITE"         = ""
    # "functionEndpoints:GeneratePdf"           = "https://fa-${local.resource_name}-pdf-generator.azurewebsites.net/api/generate?code=${azurerm_function_app_host_keys.ak_pdf_generator.default_function_key}"
    "CoreDataApiUrl"                          = var.core_data_api_url
    "CoreDataApiScope"                        = "api://5f1f433a-41b3-45d3-895e-927f50232a47/case.confirm"
    "OnBehalfOfTokenTenantId"                 = data.azurerm_client_config.current.tenant_id
    "OnBehalfOfTokenClientId"                 = azuread_application.fa_coordinator.application_id
    "OnBehalfOfTokenClientSecret"             = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.kvs_fa_coordinator_client_secret.id})"
  }
  site_config {
    always_on      = true
    ip_restriction = []

    cors {
      allowed_origins = [
        "https://${local.app_name}.azurewebsites.net"
      ]
    }
  }

  identity {
    type = "SystemAssigned"
  }

  auth_settings {
    enabled                       = true
    issuer                        = "https://sts.windows.net/${data.azurerm_client_config.current.tenant_id}/"
    unauthenticated_client_action = "RedirectToLoginPage"
    default_provider              = "AzureActiveDirectory"
    active_directory {
      client_id         = azuread_application.fa_coordinator.application_id
      client_secret     = azuread_application_password.faap_fa_coordinator_app_service.value
      allowed_audiences = ["api://fa-${local.resource_name}-coordinator"]
    }
  }
  
  lifecycle {
    ignore_changes = [
      app_settings["WEBSITES_ENABLE_APP_SERVICE_STORAGE"],
      app_settings["WEBSITE_ENABLE_SYNC_UPDATE_SITE"],
    ]
  }

  # depends_on = [
  #   data.azurerm_function_app_host_keys.ak_pdf_generator,
  #   data.azurerm_function_app_host_keys.ak_text_extractor,
  #   data.azurerm_function_app_host_keys.ak_indexer
  # ]
}

data "azurerm_function_app_host_keys" "ak_coordinator" {
  name                = "fa-${local.resource_name}-coordinator"
  resource_group_name = azurerm_resource_group.rg.name
  depends_on = [azurerm_function_app.fa_coordinator]
}

resource "azuread_application" "fa_coordinator" {
  display_name               = "fa-${local.resource_name}-coordinator"
  identifier_uris            = ["api://fa-${local.resource_name}-coordinator"]
}

resource "azuread_application_password" "faap_fa_coordinator_app_service" {
  application_object_id = azuread_application.fa_coordinator.id
  end_date_relative     = "17520h"
  # value                 = "xxx"

  depends_on = [
    azuread_application.fa_coordinator
  ]
}