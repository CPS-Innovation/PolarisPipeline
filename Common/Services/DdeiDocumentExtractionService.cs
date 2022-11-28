using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Constants;
using Common.Domain.DocumentExtraction;
using Common.Domain.Responses;
using Common.Factories.Contracts;
using Common.Logging;
using Common.Mappers.Contracts;
using Common.Services.Contracts;
using Common.Wrappers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Common.Services;

public class DdeiDocumentExtractionService : BaseDocumentExtractionService, IDdeiDocumentExtractionService
{
    private readonly ILogger<DdeiDocumentExtractionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IJsonConvertWrapper _jsonConvertWrapper;
    private readonly ICaseDocumentMapper<DdeiCaseDocumentResponse> _caseDocumentMapper;

    public DdeiDocumentExtractionService(HttpClient httpClient, IHttpRequestFactory httpRequestFactory, ILogger<DdeiDocumentExtractionService> logger, 
        IConfiguration configuration, IJsonConvertWrapper jsonConvertWrapper, ICaseDocumentMapper<DdeiCaseDocumentResponse> caseDocumentMapper)
        : base(logger, httpRequestFactory, httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _jsonConvertWrapper = jsonConvertWrapper;
        _caseDocumentMapper = caseDocumentMapper;
    }

    public async Task<Stream> GetDocumentAsync(string caseUrn, string caseId, string documentCategory, string documentId, string upstreamToken, Guid correlationId)
    {
        _logger.LogMethodEntry(correlationId, nameof(GetDocumentAsync), $"CaseUrn: {caseUrn}, CaseId: {caseId}, DocumentId: {documentId}");
        
        var content = await GetHttpContentAsync(string.Format(_configuration[ConfigKeys.SharedKeys.GetDocumentUrl], caseUrn, caseId, documentCategory, documentId), upstreamToken, correlationId);
        var result = await content.ReadAsStreamAsync();
        
        _logger.LogMethodExit(correlationId, nameof(GetDocumentAsync), string.Empty);
        return result;
    }

    public async Task<CaseDocument[]> ListDocumentsAsync(string caseUrn, string caseId, string upstreamToken, Guid correlationId)
    {
        _logger.LogMethodEntry(correlationId, nameof(GetDocumentAsync), $"CaseUrn: {caseUrn}, CaseId: {caseId}");

        var response = await GetHttpContentAsync(string.Format(_configuration[ConfigKeys.SharedKeys.ListDocumentsUrl], caseUrn, caseId), upstreamToken, correlationId);
        var stringContent = await response.ReadAsStringAsync();
        var ddeiResults = _jsonConvertWrapper.DeserializeObject<List<DdeiCaseDocumentResponse>>(stringContent);

        _logger.LogMethodExit(correlationId, nameof(GetDocumentAsync), string.Empty);
        return ddeiResults.Take(1).Select(ddeiResult => _caseDocumentMapper.Map(ddeiResult)).Where(mappedResult => mappedResult != null).ToArray();
    }
}
