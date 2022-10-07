﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace coordinator.Domain.Tracker
{
    public interface ITracker
    {
        Task Initialise(string transactionId);
        Task RegisterDocumentIds(IEnumerable<string> documentIds);
        Task RegisterPdfBlobName(RegisterPdfBlobNameArg arg);
        Task RegisterDocumentNotFoundInCde(string documentId);
        Task RegisterUnableToConvertDocumentToPdf(string documentId);
        Task RegisterUnexpectedPdfDocumentFailure(string documentId);
        Task RegisterNoDocumentsFoundInCde();
        Task RegisterIndexed(string documentId);
        Task RegisterOcrAndIndexFailure(string documentId);
        Task RegisterCompleted();
        Task RegisterFailed();
        Task<List<TrackerDocument>> GetDocuments();
        Task<bool> AllDocumentsFailed();
        Task<bool> IsAlreadyProcessed();
    }
}