using Microsoft.AspNetCore.SignalR;
using RfidBackend.Hubs;
using RfidBackend.Models;
using RfidBackend.Native;

namespace RfidBackend.Services
{
    public class RfidService : IRfidService, IDisposable
    {
        private readonly IHubContext<RfidHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RfidService> _logger;
        
        private Timer? _simulationTimer;
        private readonly List<RfidTag> _readTags = new();
        private int _thresholdValue = 10;
        private bool _isReading = false;
        private int _readerHandle = -1;

        public event EventHandler<RfidTag>? TagRead;
        public event EventHandler<int>? ThresholdReached;

        public bool IsReading => _isReading;

        public RfidService(IHubContext<RfidHub> hubContext, IHttpClientFactory httpClientFactory, ILogger<RfidService> logger)
        {
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> StartReadingAsync()
        {
            if (_isReading) return true;

            try
            {
                // Gerçek RFID cihaz bağlantısı
                byte comAddr = 0xFF; // Broadcast address to find reader
                int result = RfidNativeWrapper.OpenComPort(
                    RfidNativeWrapper.COM1, 
                    ref comAddr, 
                    RfidNativeWrapper.BAUD_57600, 
                    ref _readerHandle);

                if (result != 0)
                {
                    _logger.LogWarning($"Failed to open COM port, using simulation mode. Error code: {result}");
                    // Simülasyon moduna geç
                    result = RfidNativeWrapper.Simulator.SimulateOpenComPort(
                        RfidNativeWrapper.COM1, 
                        ref comAddr, 
                        RfidNativeWrapper.BAUD_57600, 
                        ref _readerHandle);
                }

                if (result == 0)
                {
                    // Buffer'ı temizle
                    RfidNativeWrapper.ClearBuffer_G2(ref comAddr, _readerHandle);
                    
                    // Continuous reading için timer başlat
                    _simulationTimer = new Timer(ReadTagsFromDevice, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
                    
                    _isReading = true;
                    await _hubContext.Clients.All.SendAsync("ReadingStatusChanged", _isReading);
                    
                    _logger.LogInformation($"RFID reading started with handle: {_readerHandle}, address: 0x{comAddr:X2}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Failed to establish RFID connection. Error code: {result}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting RFID reading");
                return false;
            }
        }

        public async Task<bool> StopReadingAsync()
        {
            if (!_isReading) return true;

            try
            {
                _simulationTimer?.Dispose();
                _simulationTimer = null;

                // Gerçek RFID cihaz bağlantısını kapat
                if (_readerHandle != -1)
                {
                    try
                    {
                        RfidNativeWrapper.CloseComPort();
                        _logger.LogInformation("COM port closed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error closing COM port");
                    }
                    _readerHandle = -1;
                }

                _isReading = false;
                await _hubContext.Clients.All.SendAsync("ReadingStatusChanged", _isReading);
                
                _logger.LogInformation("RFID reading stopped");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping RFID reading");
                return false;
            }
        }

        public Task<TagCounterModel> GetTagCounterAsync()
        {
            var result = new TagCounterModel
            {
                CurrentCount = _readTags.Count,
                ThresholdValue = _thresholdValue,
                ThresholdReached = _readTags.Count >= _thresholdValue,
                Tags = _readTags.ToList()
            };
            return Task.FromResult(result);
        }

        public async Task SetThresholdAsync(int threshold)
        {
            _thresholdValue = threshold;
            await _hubContext.Clients.All.SendAsync("ThresholdChanged", threshold);
        }

        private async void ReadTagsFromDevice(object? state)
        {
            if (!_isReading || _readerHandle == -1) return;

            try
            {
                byte comAddr = 0x01; // Varsayılan okuyucu adresi
                int totalLength = 0;
                int cardNum = 0;
                byte[] buffer = new byte[4096]; // 4KB buffer

                // Buffer'dan veri oku
                int result = RfidNativeWrapper.ReadBuffer_G2(
                    ref comAddr, 
                    ref totalLength, 
                    ref cardNum, 
                    buffer, 
                    _readerHandle);

                if (result == 0 && cardNum > 0)
                {
                    // Buffer verilerini parse et
                    var parsedTags = RfidNativeWrapper.Helpers.ParseEpcBuffer(buffer, totalLength, cardNum);
                    
                    foreach (var parsedTag in parsedTags)
                    {
                        // Yeni tag olup olmadığını kontrol et (duplicate check)
                        if (!_readTags.Any(t => t.TagId == parsedTag.EpcString))
                        {
                            var newTag = new RfidTag
                            {
                                TagId = parsedTag.EpcString,
                                ReadTime = parsedTag.ReadTime,
                                Rssi = parsedTag.Rssi,
                                AntennaPort = parsedTag.Antenna.ToString()
                            };

                            _readTags.Add(newTag);

                            // SignalR üzerinden tüm istemcilere gönder
                            await _hubContext.Clients.All.SendAsync("TagRead", newTag);
                            await _hubContext.Clients.All.SendAsync("TagCountChanged", _readTags.Count);

                            // Event'i tetikle
                            TagRead?.Invoke(this, newTag);

                            _logger.LogInformation($"New tag read: {newTag.TagId}, RSSI: {newTag.Rssi}, Total: {_readTags.Count}");

                            // Eşik kontrolü
                            if (_readTags.Count == _thresholdValue)
                            {
                                await _hubContext.Clients.All.SendAsync("ThresholdReached", _readTags.Count);
                                ThresholdReached?.Invoke(this, _readTags.Count);
                                
                                // External API'ye istek gönder
                                await SendThresholdNotificationAsync();
                            }
                        }
                    }

                    // Buffer'ı temizle
                    if (cardNum > 0)
                    {
                        RfidNativeWrapper.ClearBuffer_G2(ref comAddr, _readerHandle);
                    }
                }
                else if (result != 0)
                {
                    // Hata durumunda simülasyon moduna geç
                    _logger.LogWarning($"ReadBuffer_G2 failed with code {result}, using simulation");
                    await SimulateTagReading();
                }

                // Periyodik inventory başlat
                if (_isReading)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(100); // Kısa delay
                        await StartInventoryCommand();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading tags from device, falling back to simulation");
                await SimulateTagReading();
            }
        }

        private Task StartInventoryCommand()
        {
            try
            {
                byte comAddr = 0x01;
                
                // Temporary variables for inventory result
                int tempTotalLen = 0;
                int tempCardNum = 0;
                
                // Inventory komutu gönder
                int result = RfidNativeWrapper.Inventory_G2(
                    ref comAddr,
                    4,                                           // Q Value (0-15)
                    0,                                           // Session (0-3)
                    RfidNativeWrapper.MASK_MEM_EPC,             // EPC memory
                    new byte[] { 0x00, 0x00 },                 // Mask address
                    0,                                           // Mask length (0 = no mask)
                    new byte[32],                                // Mask data
                    0x00,                                        // Mask disabled
                    0,                                           // TID address
                    0,                                           // TID length
                    0x00,                                        // Read EPC (not TID)
                    RfidNativeWrapper.TARGET_A,                  // Target A
                    0x80,                                        // Antenna (module specific)
                    20,                                          // Scan time (3-255)
                    0x01,                                        // Fast mode enabled
                    new byte[4096],                              // EPC buffer
                    new byte[512],                               // Antenna buffer
                    ref tempTotalLen,                            // Will be updated
                    ref tempCardNum,                             // Will be updated
                    _readerHandle);

                if (result != 0 && result != 0x02) // 0x02 = timeout, normal
                {
                    _logger.LogDebug($"Inventory command result: {result:X2}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing inventory command");
            }
            return Task.CompletedTask;
        }

        private async Task SimulateTagReading()
        {
            var newTag = new RfidTag
            {
                TagId = RfidNativeWrapper.Simulator.GenerateRandomTagId(),
                ReadTime = DateTime.Now,
                Rssi = RfidNativeWrapper.Simulator.GenerateRandomRssi(),
                AntennaPort = "1"
            };

            // Duplicate check
            if (!_readTags.Any(t => t.TagId == newTag.TagId))
            {
                _readTags.Add(newTag);

                // SignalR üzerinden tüm istemcilere gönder
                await _hubContext.Clients.All.SendAsync("TagRead", newTag);
                await _hubContext.Clients.All.SendAsync("TagCountChanged", _readTags.Count);

                // Event'i tetikle
                TagRead?.Invoke(this, newTag);

                // Eşik kontrolü
                if (_readTags.Count == _thresholdValue)
                {
                    await _hubContext.Clients.All.SendAsync("ThresholdReached", _readTags.Count);
                    ThresholdReached?.Invoke(this, _readTags.Count);
                    
                    // External API'ye istek gönder
                    await SendThresholdNotificationAsync();
                }

                _logger.LogInformation($"Simulated tag read: {newTag.TagId}, Total: {_readTags.Count}");
            }
        }

        private async Task SendThresholdNotificationAsync()
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync("http://81.213.79.71/barfas/rfid/sayac.php?=ok");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Threshold notification sent successfully");
                    await _hubContext.Clients.All.SendAsync("NotificationSent", "Yeterli Sayıda Etiket Okunmuştur");
                }
                else
                {
                    _logger.LogWarning($"Failed to send threshold notification. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending threshold notification");
            }
        }

        public void Dispose()
        {
            _simulationTimer?.Dispose();
            if (_readerHandle != -1)
            {
                // RfidNativeWrapper.CloseNetPort(_readerHandle);
            }
        }
    }
}
