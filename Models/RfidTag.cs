namespace RfidBackend.Models
{
    public class RfidTag
    {
        public string TagId { get; set; } = string.Empty;
        public DateTime ReadTime { get; set; }
        public int Rssi { get; set; }
        public string AntennaPort { get; set; } = string.Empty;
    }
}