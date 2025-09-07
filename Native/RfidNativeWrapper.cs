using System.Runtime.InteropServices;

namespace RfidBackend.Native
{
    public static class RfidNativeWrapper
    {
        private const string DLL_NAME = "UHFReader86.dll";

        //com ports
        public const int COM1 = 1;
        public const int COM2 = 2;
        public const int COM3 = 3;

        //baud rates
        public const byte BAUD_9600 = 0;
        public const byte BAUD_19200 = 1;
        public const byte BAUD_38400 = 2;
        public const byte BAUD_57600 = 5;
        public const byte BAUD_115200 = 6;

        //memory banks
        public const byte MASK_MEM_EPC = 0x01;
        public const byte MASK_MEM_TID = 0x02;
        public const byte MASK_MEM_USER = 0x03;

        //targets
        public const byte TARGET_A = 0x00;
        public const byte TARGET_B = 0x01;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int OpenComPort(int Port, ref byte ComAdr, byte Baud, ref int FrmHandle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CloseComPort();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Inventory_G2(
            ref byte ComAdr,
            byte QValue,
            byte Session,
            byte MaskMem,
            byte[] MaskAdr,
            byte MaskLen,
            byte[] MaskData,
            byte MaskFlag,
            byte AdrTID,
            byte LenTID,
            byte TIDFlag,
            byte Target,
            byte InAnt,
            byte Scantime,
            byte Fastflag,
            byte[] EPClenandEPC,
            byte[] Ant,
            ref int Totallen,
            ref int CardNum,
            int FrmHandle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ReadBuffer_G2(
            ref byte ComAdr,
            ref int Totallen,
            ref int CardNum,
            byte[] pEPCList,
            int FrmHandle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ClearBuffer_G2(ref byte ComAdr, int FrmHandle);

        public static class Helpers
        {
            public static List<ParsedEpcData> ParseEpcBuffer(byte[] buffer, int totalLength, int cardNum)
            {
                var result = new List<ParsedEpcData>();
                int offset = 0;

                for (int i = 0; i < cardNum && offset < totalLength; i++)
                {
                    if (offset + 5 > totalLength) break; // Minimum header size check

                    var epcData = new ParsedEpcData
                    {
                        Antenna = buffer[offset],
                        Length = buffer[offset + 1]
                    };

                    offset += 2;

                    if (offset + epcData.Length + 2 > totalLength) break;

                    // EPC data
                    epcData.EpcBytes = new byte[epcData.Length];
                    Array.Copy(buffer, offset, epcData.EpcBytes, 0, epcData.Length);
                    offset += epcData.Length;

                    // RSSI and Count
                    epcData.Rssi = (sbyte)buffer[offset];
                    epcData.Count = buffer[offset + 1];
                    offset += 2;

                    // Convert EPC bytes to hex string
                    epcData.EpcString = BitConverter.ToString(epcData.EpcBytes).Replace("-", "");

                    result.Add(epcData);
                }

                return result;
            }

            public static string ByteArrayToHex(byte[] bytes)
            {
                return BitConverter.ToString(bytes).Replace("-", "");
            }

            public static byte[] HexToByteArray(string hex)
            {
                return Enumerable.Range(0, hex.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                         .ToArray();
            }
        }

        public class ParsedEpcData
        {
            public byte Antenna { get; set; }
            public byte Length { get; set; }
            public byte[] EpcBytes { get; set; } = Array.Empty<byte>();
            public string EpcString { get; set; } = string.Empty;
            public sbyte Rssi { get; set; }
            public byte Count { get; set; }
            public DateTime ReadTime { get; set; } = DateTime.Now;
        }

        // Simülasyon için örnek veri üretici
        public static class Simulator
        {
            private static readonly Random _random = new Random();
            private static readonly string[] _sampleTags = {
                "E200001A75012345",
                "E200001A75012346", 
                "E200001A75012347",
                "E200001A75012348",
                "E200001A75012349",
                "E200001B85023456",
                "E200001C95034567",
                "E200001D05045678"
            };

            public static string GenerateRandomTagId()
            {
                return _sampleTags[_random.Next(_sampleTags.Length)] + _random.Next(1000, 9999);
            }

            public static int GenerateRandomRssi()
            {
                return _random.Next(-80, -30);
            }

            public static byte[] GenerateSimulatedEpcBuffer(int tagCount = 1)
            {
                var buffer = new List<byte>();

                for (int i = 0; i < tagCount; i++)
                {
                    // Antenna (1 byte)
                    buffer.Add(0x01);

                    // EPC Length (1 byte) - typically 12 bytes for EPC
                    byte epcLength = 12;
                    buffer.Add(epcLength);

                    // EPC Data (epcLength bytes)
                    var epcHex = GenerateRandomTagId();
                    var epcBytes = Helpers.HexToByteArray(epcHex.Substring(0, Math.Min(epcHex.Length, epcLength * 2)));
                    
                    // Pad if necessary
                    while (epcBytes.Length < epcLength)
                    {
                        Array.Resize(ref epcBytes, epcBytes.Length + 1);
                        epcBytes[epcBytes.Length - 1] = 0;
                    }

                    buffer.AddRange(epcBytes.Take(epcLength));

                    // RSSI (1 byte)
                    buffer.Add((byte)GenerateRandomRssi());

                    // Count (1 byte)
                    buffer.Add((byte)_random.Next(1, 100));
                }

                return buffer.ToArray();
            }

            public static int SimulateOpenComPort(int port, ref byte comAdr, byte baud, ref int frmHandle)
            {
                // Simülasyon için rastgele handle üret
                frmHandle = _random.Next(1000, 9999);
                
                // ComAdr broadcast ise (0xFF), rastgele adres dön
                if (comAdr == 0xFF)
                {
                    comAdr = (byte)_random.Next(0x01, 0xFE);
                }

                // Başarılı bağlantı simüle et
                return 0;
            }
        }
    }
}
