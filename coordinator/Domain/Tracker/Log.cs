using Newtonsoft.Json;

namespace coordinator.Domain.Tracker
{
    public class Log
    {
        public string LogType { get; set; }

        public string TimeStamp { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? DocumentId { get; set; }
    }
}