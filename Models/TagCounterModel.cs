namespace RfidBackend.Models
{
    public class TagCounterModel
    {
        public int CurrentCount { get; set; }
        public int ThresholdValue { get; set; }
        public bool ThresholdReached { get; set; }
        public List<RfidTag> Tags { get; set; } = new List<RfidTag>();
    }
}
