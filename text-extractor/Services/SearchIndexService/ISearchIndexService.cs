﻿using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace text_extractor.Services.SearchIndexService
{
	public interface ISearchIndexService
	{
		Task StoreResultsAsync(AnalyzeResults analyzeresults, int caseId, string documentId);
	}
}

