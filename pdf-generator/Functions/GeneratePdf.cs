﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using common.Domain.Exceptions;
using common.Handlers;
using common.Wrappers;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using pdf_generator.Domain;
using pdf_generator.Domain.Requests;
using pdf_generator.Domain.Responses;
using pdf_generator.Handlers;
using pdf_generator.Services.BlobStorageService;
using pdf_generator.Services.DocumentExtractionService;
using pdf_generator.Services.PdfService;

namespace pdf_generator.Functions
{
    public class GeneratePdf
    {
        private readonly IAuthorizationValidator _authorizationValidator;
        private readonly IJsonConvertWrapper _jsonConvertWrapper;
        private readonly IValidatorWrapper<GeneratePdfRequest> _validatorWrapper;
        private readonly IDocumentExtractionService _documentExtractionService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IPdfOrchestratorService _pdfOrchestratorService;
        private readonly IExceptionHandler _exceptionHandler;

        public GeneratePdf(
             IAuthorizationValidator authorizationValidator, IJsonConvertWrapper jsonConvertWrapper,
             IValidatorWrapper<GeneratePdfRequest> validatorWrapper, IDocumentExtractionService documentExtractionService,
             IBlobStorageService blobStorageService, IPdfOrchestratorService pdfOrchestratorService, IExceptionHandler exceptionHandler)
        {
            _authorizationValidator = authorizationValidator;
            _jsonConvertWrapper = jsonConvertWrapper;
            _validatorWrapper = validatorWrapper;
            _documentExtractionService = documentExtractionService;
            _blobStorageService = blobStorageService;
            _pdfOrchestratorService = pdfOrchestratorService;
            _exceptionHandler = exceptionHandler;
        }

        [FunctionName("generate-pdf")]
        public async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "generate")] HttpRequestMessage request, ClaimsPrincipal claimsPrincipal)
        {
            try
            {
                var authValidation = await _authorizationValidator.ValidateTokenAsync(request.Headers.Authorization);
                if (!authValidation.Item1)
                    throw new UnauthorizedException("Token validation failed");

                if (request.Content == null)
                    throw new BadRequestException("Request body has no content", nameof(request));

                var content = await request.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new BadRequestException("Request body cannot be null.", nameof(request));
                }

                var pdfRequest = _jsonConvertWrapper.DeserializeObject<GeneratePdfRequest>(content);

                var results = _validatorWrapper.Validate(pdfRequest);
                if (results.Any())
                {
                    throw new BadRequestException(string.Join(Environment.NewLine, results), nameof(request));
                }

                //TODO exchange access token via on behalf of?
                //var accessToken = values.First().Replace("Bearer ", "");
                var documentStream = await _documentExtractionService.GetDocumentAsync(pdfRequest.DocumentId, pdfRequest.FileName, "onBehalfOfAccessToken");

                var blobName = $"{pdfRequest.CaseId}/pdfs/{pdfRequest.DocumentId}.pdf";
                var fileType = pdfRequest.FileName.Split('.').Last().ToFileType();
                if (fileType == FileType.PDF)
                {
                    await _blobStorageService.UploadDocumentAsync(documentStream, blobName);
                }
                else
                {
                    var pdfStream = _pdfOrchestratorService.ReadToPdfStream(documentStream, fileType, pdfRequest.DocumentId);
                    await _blobStorageService.UploadDocumentAsync(pdfStream, blobName);
                }

                return OkResponse(Serialize(new GeneratePdfResponse { BlobName = blobName }));
            }
            catch(Exception exception)
            {
                return _exceptionHandler.HandleException(exception);
            }
        }

        private string Serialize(object objectToSerialize)
        {
            return _jsonConvertWrapper.SerializeObject(objectToSerialize);
        }

        private HttpResponseMessage OkResponse(string content)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, MediaTypeNames.Application.Json)
            };
        }
    }
}