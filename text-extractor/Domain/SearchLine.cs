﻿using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Newtonsoft.Json;

namespace text_extractor.Domain
{
    public class SearchLine : Line
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("caseId")]
        public int CaseId { get; set; }

        [JsonProperty("documentId")]
        public string DocumentId { get; set; }

        [JsonProperty("pageIndex")]
        public int PageIndex { get; set; }

        [JsonProperty("lineIndex")]
        public int LineIndex { get; set; }
    }
}