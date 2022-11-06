﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Constants;
using Common.Domain.Exceptions;
using Common.Domain.Requests;
using Common.Handlers;
using Common.Logging;
using Common.Wrappers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using text_extractor.Handlers;
using text_extractor.Services.SearchIndexService;

namespace text_extractor.Functions
{
    public class UpdateSearchIndex
    {
        private readonly IAuthorizationValidator _authorizationValidator;
        private readonly IJsonConvertWrapper _jsonConvertWrapper;
        private readonly IValidatorWrapper<UpdateSearchIndexRequest> _validatorWrapper;
        private readonly ISearchIndexService _searchIndexService;
        private readonly IExceptionHandler _exceptionHandler;
        private readonly ILogger<UpdateSearchIndex> _log;

        public UpdateSearchIndex(IAuthorizationValidator authorizationValidator, IJsonConvertWrapper jsonConvertWrapper,
             IValidatorWrapper<UpdateSearchIndexRequest> validatorWrapper, ISearchIndexService searchIndexService, IExceptionHandler exceptionHandler, 
             ILogger<UpdateSearchIndex> logger)
        {
            _authorizationValidator = authorizationValidator;
            _jsonConvertWrapper = jsonConvertWrapper;
            _validatorWrapper = validatorWrapper;
            _searchIndexService = searchIndexService;
            _exceptionHandler = exceptionHandler;
            _log = logger;
        }

        [FunctionName("UpdateSearchIndex")]
        public async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "updateSearchIndex")] HttpRequestMessage request)
        {
            Guid currentCorrelationId = default;
            const string loggingName = "UpdateSearchIndex - Run";

            try
            {
                request.Headers.TryGetValues(HttpHeaderKeys.CorrelationId, out var correlationIdValues);
                if (correlationIdValues == null)
                    throw new BadRequestException("Invalid correlationId. A valid GUID is required.", nameof(request));

                var correlationId = correlationIdValues.First();
                if (!Guid.TryParse(correlationId, out currentCorrelationId) || currentCorrelationId == Guid.Empty)
                    throw new BadRequestException("Invalid correlationId. A valid GUID is required.", correlationId);

                _log.LogMethodEntry(currentCorrelationId, loggingName, string.Empty);

                var authValidation =
                    await _authorizationValidator.ValidateTokenAsync(request.Headers.Authorization, currentCorrelationId, PipelineScopes.UpdateSearchIndex, PipelineRoles.UpdateSearchIndex);
                if (!authValidation.Item1)
                    throw new UnauthorizedException("Token validation failed");

                if (request.Content == null)
                    throw new BadRequestException("Request body has no content", nameof(request));

                var content = await request.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new BadRequestException("Request body cannot be null.", nameof(request));
                }

                var updateSearchIndexRequest = _jsonConvertWrapper.DeserializeObject<UpdateSearchIndexRequest>(content);

                var results = _validatorWrapper.Validate(updateSearchIndexRequest);
                if (results.Any())
                {
                    throw new BadRequestException(string.Join(Environment.NewLine, results), nameof(request));
                }

                _log.LogMethodFlow(currentCorrelationId, loggingName, $"Beginning search index update for caseId: {updateSearchIndexRequest.CaseId}, documentId: {updateSearchIndexRequest.DocumentId}");
                await _searchIndexService.RemoveResultsForDocumentAsync(int.Parse(updateSearchIndexRequest.CaseId), updateSearchIndexRequest.DocumentId, currentCorrelationId);
                
                _log.LogMethodFlow(currentCorrelationId, loggingName, $"Search index update completed for caseId: {updateSearchIndexRequest.CaseId}, documentId: {updateSearchIndexRequest.DocumentId}");

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception exception)
            {
                return _exceptionHandler.HandleException(exception, currentCorrelationId, loggingName, _log);
            }
            finally
            {
                _log.LogMethodExit(currentCorrelationId, loggingName, string.Empty);
            }
        }
    }
}