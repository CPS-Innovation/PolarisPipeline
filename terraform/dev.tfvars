env = "dev"
stub_blob_storage_connection_string="DefaultEndpointsProtocol=https;AccountName=sadevcmsdocumentservices;AccountKey=06beksVS54Cw5YqSLpvKrJStK8yYMsSui1cPO3MT4+pnHys6sCBFqBq17ix5ZGXuL5cHxnBIslXzZsL24ZRa7g==;EndpointSuffix=core.windows.net"
app_service_plan_sku = {
    size = "B3"
    tier = "Basic"
}
ddei_config = {
    base_url = "https://fa-rumpole-tde-temp.azurewebsites.net/api/"
    list_documents_function_key = "smz7KW0-fIcR6oT-EaX63P3hduRAXoQvtn9YqGIKOzlXAzFuRdA73g=="
    get_document_function_key = "Kdk8RI0pQlJ8nm1AANKohWRA69llzAIkHIpbjqlMj9FhAzFucFAWQA=="
}
queue_config = {
    evaluate_existing_documents_queue_name = "evaluate-existing-documents"
    update_search_index_by_version_queue_name = "update-search-index-by-version"
    update_search_index_by_blob_name_queue_name = "update-search-index-by-blob-name"
}
queue_config = {
    evaluate_existing_documents_queue_name = "evaluate-existing-documents"
    update_search_index_by_version_queue_name = "update-search-index-by-version"
    update_search_index_by_blob_name_queue_name = "update-search-index-by-blob-name"
}