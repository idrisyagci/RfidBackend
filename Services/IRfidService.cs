using RfidBackend.Models;

namespace RfidBackend.Services
{
    public interface IRfidService
    {
        Task<bool> StartReadingAsync();
        Task<bool> StopReadingAsync();
        Task<TagCounterModel> GetTagCounterAsync();
        Task SetThresholdAsync(int threshold);
        event EventHandler<RfidTag>? TagRead;
        event EventHandler<int>? ThresholdReached;
        bool IsReading { get; }
    }
}
