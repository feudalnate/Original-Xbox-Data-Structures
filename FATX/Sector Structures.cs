//@NOTE: Only some structures for sectors stored at the beginning of a Xbox hard disk are written out here !!INCOMPLETE!!

struct RefurbInfo
{
    public uint Signature;
    public uint PowerCycleCount;
    public long FirstSetTime;

    public static bool Parse(byte[] sector, out RefurbInfo result)
    {
        result = null;
        if (sector == null || sector.Length != 0x200) return false;
        if (BitConverter.ToUInt32(sector, 0) != 0x52465242) return false;
        result = new RefurbInfo()
        {
            Signature = BitConverter.ToUInt32(sector, 0),
            PowerCycleCount = BitConverter.ToUInt32(sector, 4),
            FirstSetTime = BitConverter.ToInt64(sector, 8)
        };
        return true;
    }
}

//(also labeled as XBOX_CACHE_DB_SECTOR in the debug manager, ive labeled it as the kernel does)
struct class XboxConfigSector
{
    public uint SectorBeginSignature;
    public uint Version;
    public uint SectorCount;
    public byte[] Data; //0x1EC bytes
    public uint Checksum;
    public uint SectorEndSignature;

    //public static bool Parse(byte[] sector, out XboxConfigSector result)
    //{

    //}

}