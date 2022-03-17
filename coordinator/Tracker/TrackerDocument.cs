using System.Collections.Generic;

namespace coordinator.Tracker
{
    public class TrackerDocument
    {
        public int DocumentId { get; set; }

        public string PdfUrl { get; set; }

        public List<TrackerPageDetails> PageDetails { get; set; }

    }
}