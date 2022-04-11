using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace coordinator.Domain.Tracker
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Tracker : ITracker
    {
        [JsonProperty("transactionId")]
        public string TransactionId { get; set; }

        [JsonProperty("documents")]
        public List<TrackerDocument> Documents { get; set; }

        [JsonProperty("logs")]
        public List<Log> Logs { get; set; }

        [JsonProperty("IsComplete")]
        public bool IsComplete { get; set; }

        public Task Initialise(string transactionId)
        {
            TransactionId = transactionId;
            Documents = new List<TrackerDocument>();
            Logs = new List<Log>();
            IsComplete = false;

            Log(LogType.Initialised);

            return Task.CompletedTask;
        }

        public Task RegisterDocumentIds(IEnumerable<int> documentIds)
        {
            Documents = documentIds
                .Select(documentId => new TrackerDocument { DocumentId = documentId })
                .ToList();

            Log(LogType.RegisteredDocumentIds);

            return Task.CompletedTask;
        }

        public Task RegisterPdfBlobName(RegisterPdfBlobNameArg arg)
        {
            var document = Documents.Find(document => document.DocumentId == arg.DocumentId);
            document.PdfBlobName = arg.BlobName;

            Log(LogType.RegisteredPdfBlobName, arg.DocumentId);

            return Task.CompletedTask;
        }

        public Task RegisterCompleted()
        {
            Log(LogType.Completed);
            IsComplete = true;

            return Task.CompletedTask;
        }

        public Task<List<TrackerDocument>> GetDocuments()
        {
            return Task.FromResult(Documents);
        }

        public Task<bool> IsAlreadyProcessed()
        {
            return Task.FromResult(IsComplete);
        }

        private void Log(LogType status, int? documentId = null)
        {
            Logs.Add(new Log
            {
                LogType = status.ToString(),
                TimeStamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz"),
                DocumentId = documentId
            });
        }

        [FunctionName("Tracker")]
        public Task Run([EntityTrigger] IDurableEntityContext context)
        { 
            return context.DispatchAsync<Tracker>();
        }

        [FunctionName("TrackerStatus")]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "cases/{caseId}/tracker")] HttpRequestMessage req,
            string caseId,
            [DurableClient] IDurableEntityClient client,
            ILogger log)
        {
            var entityId = new EntityId(nameof(Tracker), caseId);
            var stateResponse = await client.ReadEntityStateAsync<Tracker>(entityId);
            if (!stateResponse.EntityExists)
            {
                var baseMessage = $"No pipeline tracker found with id '{caseId}'";
                log.LogError(baseMessage);
                return new NotFoundObjectResult(baseMessage);
            }

            return new OkObjectResult(stateResponse.EntityState);
        }
    }
}