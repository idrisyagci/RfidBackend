using RfidBackend.Models;

namespace RfidBackend.Services
{
    public interface IRfidService
    {
        Task<bool> StartReadingAsync();
        Task<bool> StopReadingAsync();
        event EventHandler<RfidTag>? TagRead;
        bool IsReading { get; }
    }
}
