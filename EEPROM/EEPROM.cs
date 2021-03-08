public class EEPROM
{
    public byte[] XboxEEPROMKey { private set; get; } //0x10 bytes (not stored in the EEPROM data, kept in this class for ease-of-use)
    public EncryptedSection EncryptedSettings { private set; get; }
    public FactorySection FactorySettings { private set; get; }
    public UserConfigSection UserSettings { private set; get; }
    public HardwareConfigSection HardwareConfiguration { private set; get; } //hardware config section is only used on 1.6(b) revision, else zeroed
                                                                             //you would think these ↓ should be in the hardware config section but they are not according to the kernel
                                                                             //and thats why when any of these have values that the checksum of the hardware config is still zero
    public ushort ThermalSensorCalibration { private set; get; }
    public byte[] Unused { private set; get; } //0x2 bytes
    public UEMInformation UEMInfo { private set; get; }
    public byte[] Reserved1 { private set; get; } //0x2 bytes @NOTE: seems 2 bytes MUST be left reserved at the end of the EEPROM data (bug in the chip maybe?)

    //quality of life
    public byte[] EncryptedEEPROM { private set; get; }
    public byte[] DecryptedEEPROM { private set; get; }

    public struct EncryptedSection //0x30 bytes
    {
        public byte[] Checksum; //0x14 bytes
        public byte[] Confounder; //0x8 bytes
        public byte[] XboxHDKey; //0x10 bytes
        public uint GameRegion;
    }

    public struct FactorySection //0x30 bytes
    {
        public uint Checksum;
        public char[] SerialNumber; //0xC chars
        public byte[] MACAddress; //0x6 bytes
        public byte[] Reserved1; //0x2 bytes
        public byte[] OnlineKey; //0x10 bytes
        public uint VideoRegion;
        public byte[] Reserved2; //0x4 bytes
    }

    public struct TimeZoneDate
    {
        public byte Month;
        public byte Day;
        public byte DayOfWeek;
        public byte Hour;
    }

    public struct UserConfigSection //0x60 bytes
    {
        public uint Checksum;
        public int TimeZoneBias;
        public char[] TimeZoneStdName; //0x4 chars
        public char[] TimeZoneDltName; //0x4 chars
        public byte[] Reserved1; //0x8 bytes
        public TimeZoneDate TimeZoneStdDate; //0x4 bytes
        public TimeZoneDate TimeZoneDltDate; //0x4 bytes
        public byte[] Reserved2; //0x8 bytes
        public int TimeZoneStdBias;
        public int TimeZoneDltBias;
        public uint Language;
        public uint VideoFlags;
        public uint AudioFlags;
        public uint ParentalControlGames;
        public uint ParentalControlPassword;
        public uint ParentalControlMovies;
        public byte[] OnlineIpAddress; //0x4 bytes
        public byte[] OnlineDnsAddress; //0x4 bytes
        public byte[] OnlineDefaultGatewayAddress; //0x4 bytes
        public byte[] OnlineSubnetMask; //0x4 bytes
        public uint MiscFlags; //used for settings that dont get their own area. so far only found "Auto power down" and "DST enabled/disabled" use this - im sure theres more
        public uint DvdRegion;
    }

    /*
    this section is used only on 1.6 and 1.6b models
    1.6 and 1.6b each have different data, all 1.6 models share the same section data

    for example my 1.6 EEPROM will have the same hw section data as your 1.6 console
    but neither of our hw sections will be the same as someone with a 1.6b model and vise-versa

    1.6/1.6b hw section data is unique for each model

    there is no checksum on this data!

    this data has something to do with the disc drive judging from the naming of types

    labeled "XBOX_HW_EE_SETTINGS" in kernel, this section only seems to declared in kernels that support 1.6 models else labeled reserved
    */
    public struct HardwareConfigSection //0x36 bytes
    {
        public byte FbioDelay;
        public byte AddrDrv;
        public byte CTrim2;
        public byte EMRS;
        public byte[] ExtSlow; //0xA bytes
        public byte[] Slow;    //0xA bytes
        public byte[] Typical; //0xA bytes
        public byte[] Fast;    //0xA bytes
        public byte[] ExtFast; //0xA bytes
    }

    public struct UEMInformation
    {
        public byte LastCode; //errors from bootloaders arent stored
        public byte Reserved1;
        public ushort History;
    }

    public static EEPROM Parse(byte[] EEPROMData, byte[] XboxEEPROMKey)
    {
        if (EEPROMData == null || EEPROMData.Length != 256)
            throw new Exception("Invalid EEPROM data");

        byte[] EncryptedData = new byte[256];
        byte[] DecryptedData = new byte[256];

        Array.Copy(EEPROMData, 0, EncryptedData, 0, 256); //make a copy of the encrypted (original) EEPROM data

        //create RC4 key from current XboxEEPROMKey and EncryptedSettings checksum
        byte[] RC4Key;
        XcHMAC(XboxEEPROMKey, EEPROMData, 0, 0x14, null, 0, 0, out RC4Key);

        //decrypt EncryptedSection
        RC4(EEPROMData, 0x14, 0x1C, RC4Key);

        //create hash of the EncryptedSettings section that was decrypted
        byte[] EncryptedSectionChecksum;
        XcHMAC(XboxEEPROMKey, EEPROMData, 0x14, 0x1C, null, 0, 0, out EncryptedSectionChecksum);

        //compare hash to the EncryptedSettings checksum
        if (!Compare(EEPROMData, 0, EncryptedSectionChecksum, 0, 0x14))
            throw new Exception("Failed to decrypt EEPROM");

        Array.Copy(EEPROMData, 0, DecryptedData, 0, 256); //make a copy of the decrypted EEPROM data

        try
        {
            //start pulling data out
            EEPROM EEPROM = new EEPROM();

            using (BinaryReader stream = new BinaryReader(new MemoryStream(EEPROMData)))
            {
                //EncryptedSettings
                EEPROM.EncryptedSettings = new EncryptedSection
                {
                    Checksum = stream.ReadBytes(0x14),
                    Confounder = stream.ReadBytes(8),
                    XboxHDKey = stream.ReadBytes(0x10),
                    GameRegion = stream.ReadUInt32()
                };

                //FactorySettings
                EEPROM.FactorySettings = new FactorySection
                {
                    Checksum = stream.ReadUInt32(),
                    SerialNumber = stream.ReadChars(0xC),
                    MACAddress = stream.ReadBytes(6),
                    Reserved1 = stream.ReadBytes(2),
                    OnlineKey = stream.ReadBytes(0x10),
                    VideoRegion = stream.ReadUInt32(),
                    Reserved2 = stream.ReadBytes(4)
                };

                //UserSettings
                EEPROM.UserSettings = new UserConfigSection
                {
                    Checksum = stream.ReadUInt32(),
                    TimeZoneBias = stream.ReadInt32(),
                    TimeZoneStdName = stream.ReadChars(4),
                    TimeZoneDltName = stream.ReadChars(4),
                    Reserved1 = stream.ReadBytes(8),
                    TimeZoneStdDate = new TimeZoneDate()
                    {
                        Month = stream.ReadByte(),
                        Day = stream.ReadByte(),
                        DayOfWeek = stream.ReadByte(),
                        Hour = stream.ReadByte()
                    },
                    TimeZoneDltDate = new TimeZoneDate()
                    {
                        Month = stream.ReadByte(),
                        Day = stream.ReadByte(),
                        DayOfWeek = stream.ReadByte(),
                        Hour = stream.ReadByte()
                    },
                    Reserved2 = stream.ReadBytes(8),
                    TimeZoneStdBias = stream.ReadInt32(),
                    TimeZoneDltBias = stream.ReadInt32(),
                    Language = stream.ReadUInt32(),
                    VideoFlags = stream.ReadUInt32(),
                    AudioFlags = stream.ReadUInt32(),
                    ParentalControlGames = stream.ReadUInt32(),
                    ParentalControlPassword = stream.ReadUInt32(),
                    ParentalControlMovies = stream.ReadUInt32(),
                    OnlineIpAddress = stream.ReadBytes(4),
                    OnlineDnsAddress = stream.ReadBytes(4),
                    OnlineDefaultGatewayAddress = stream.ReadBytes(4),
                    OnlineSubnetMask = stream.ReadBytes(4),
                    MiscFlags = stream.ReadUInt32(),
                    DvdRegion = stream.ReadUInt32()
                };

                //HardwareConfiguration
                EEPROM.HardwareConfiguration = new HardwareConfigSection
                {
                    FbioDelay = stream.ReadByte(),
                    AddrDrv = stream.ReadByte(),
                    CTrim2 = stream.ReadByte(),
                    EMRS = stream.ReadByte(),
                    ExtSlow = stream.ReadBytes(10),
                    Slow = stream.ReadBytes(10),
                    Typical = stream.ReadBytes(10),
                    Fast = stream.ReadBytes(10),
                    ExtFast = stream.ReadBytes(10)
                };

                //unsectioned/extended data
                EEPROM.ThermalSensorCalibration = stream.ReadUInt16();
                EEPROM.Unused = stream.ReadBytes(2);
                EEPROM.UEMInfo = new UEMInformation()
                {
                    LastCode = stream.ReadByte(),
                    Reserved1 = stream.ReadByte(),
                    History = stream.ReadUInt16()
                };
                EEPROM.Reserved1 = stream.ReadBytes(2);
            }

            //store encryption key
            EEPROM.XboxEEPROMKey = XboxEEPROMKey;

            //store copies of encrypted/decrypted EEPROM data
            EEPROM.EncryptedEEPROM = EncryptedData;
            EEPROM.DecryptedEEPROM = DecryptedData;

            return EEPROM;
        }
        catch
        {
            throw new Exception("Failed to parse EEPROM data");
        }
    }

    //encrypts/decrypts in-place
    private static void RC4(byte[] buffer, int index, int count, byte[] key)
    {
        int[] a = new int[256];
        int[] b = new int[256];
        int x = 0;
        int y = 0;
        int z = 0;
        int xor = 0;
        //key setup
        for (int i = 0; i < 256; i++)
        {
            a[i] = key[i % key.Length];
            b[i] = i;
        }
        for (int i = 0; i < 256; i++)
        {
            x = (((x + b[i]) + a[i]) % 256);
            y = b[i];
            b[i] = b[x];
            b[x] = y;
        }
        //transform
        x = 0;
        for (int i = index; i < (index + count); i++)
        {
            z++;
            z %= 256;
            x += b[z];
            x %= 256;
            y = b[z];
            b[z] = b[x];
            b[x] = y;
            xor = b[((b[z] + b[x]) % 256)];
            buffer[i] = (byte)(buffer[i] ^ xor);
        }
    }

    private static bool Compare(byte[] buffer, int index, byte[] buffer2, int index2, int count)
    {
        if (buffer == null || buffer2 == null) return false; //null check
        if (buffer.Length < (index + count) || buffer2.Length < (index2 + count)) return false; //oob check
        for (int i = 0; i < count; i++)
        {
            if (buffer[index + i] != buffer2[index2 + i]) return false;
        }
        return true;
    }

    private static void XcHMAC(byte[] Key, byte[] buffer, int index, int count, byte[] buffer2, int index2, int count2, out byte[] Hash)
    {
        Hash = null;
        if (Key == null || buffer == null || count == 0) return;

        using (HMACSHA1 HMAC = new HMACSHA1(Key))
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (CryptoStream stream = new CryptoStream(memory, HMAC, CryptoStreamMode.Write))
                {
                    stream.Write(buffer, index, count);
                    if (buffer2 != null || count2 > 0)
                    {
                        stream.Flush();
                        stream.Write(buffer2, index2, count2);
                    }
                    stream.FlushFinalBlock();
                }
            }
            Hash = HMAC.Hash;
        }
    }

}