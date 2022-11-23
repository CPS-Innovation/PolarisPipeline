﻿using System;

namespace coordinator.Domain
{
    public class CreateTextExtractorHttpRequestActivityPayload : BasePipelinePayload
    {
        public CreateTextExtractorHttpRequestActivityPayload(string caseUrn, long caseId, string documentId, long versionId, string blobName, Guid correlationId)
            : base(caseUrn, caseId, correlationId)
        {
            DocumentId = documentId;
            VersionId = versionId;
            BlobName = blobName;
        }
        
        public string DocumentId { get; set; }

        public long VersionId { get; set; }

        public string BlobName { get; set; }
    }
}
