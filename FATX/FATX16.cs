//@NOTE: WORK IN PROGRESS !!INCOMPLETE!!

internal static class Structures
{
    //padded with 0xFF to sector bounds or memory page size (0x1000), whichever is greater
    //(always 0x1000 from what i've seen)
    //(which makes sense since most devices wont have a sector size greater than 512 bytes)
    //(there exists devices that may use the 'advanced' format, which is 4096 byte sectors (0x1000))
    //(so i dont know if there's a way for this to not be 0x1000. 0x200/0x1000 are the only hard disk sector sizes ive seen)
    internal struct Header
    {
        internal uint Magic;
        internal uint SerialNumber;
        internal uint SectorsPerCluster;
        internal uint RootDirectoryClusterStartIndex;
        internal ushort[] VolumeName; //unicode, 0x40 bytes (32 w_char null-term string)

        //0x800 bytes but only 0x6C bytes used in FATX16 for the 1 allowed account
        //and for FATX32 each account gets allocated its own sector near the beginning of the drive
        //so i don't know why this is allocated as 0x800 bytes (old code? scrapped idea?)
        //@TODO: needs more research for FATX32 volumes
        //@TODO: xbl accounts and machine account are stored at the beginning of the drive -not- partitions, not sure what this area is actually reserved for (mem cards)
        internal byte[] OnlineData;
    }

    //bit packed struct
    internal struct Timestamp //32 bits, standard FAT timestamp with different epoch
    {
        //little endian
        internal ushort Seconds;       //5 bits (0-29) ("DoubleSeconds", 2000ms resolution, 0-29 * 2 = 0-58)
        internal ushort Minute;        //6 bits (0-59)
        internal ushort Hour;          //5 bits (0-23)
        internal ushort Day;           //5 bits (1-31)
        internal ushort Month;         //4 bits (1-12)
        internal ushort Year;          //7 bits (0-127) (2000 + value = year) (2127 is doomsday for FATX)
                                       //epoch is Jan. 1, 2000 00:00:00
    }

    internal struct DirectoryEntry //entry structure is the same between FATX16/FATX32
    {
        internal byte NameLength; //also used as an allocation flag when zero or above max filename size
        internal byte Attributes;
        internal char[] Name; //allocates max filename size regardless of NameLength value, fills unused chars with 0xFF
        internal uint ClusterStartIndex; //stored as u32 value but still masked to u16 for FATX16
                                         //timestamps
        internal uint CreationTime;
        internal uint LastWriteTime;
        internal uint LastAccessTime;
    }
}

internal static class Constants
{
    internal const uint MAGIC = 0x58544146; //FATX (little endian)

    //max clusters FATX16 can support, including 1 reserved cluster (1GB - 16KB = max FATX16 volume)
    //if above this limit it is considered a FATX32 volume
    internal const int FATX16MAXCLUSTERS = 0xFFF0;
    //internal const int FATX32MAXCLUSTERS = 0xFFFFFFF0;
    internal const int MAX_FILENAME = 42;
    internal const int MAX_FILEPATH = 250;

    //Directory entry flags (used when filename size not set)
    internal const byte FREE = 0x00;
    internal const byte FREE2 = 0xFF; //y tho
    internal const byte DELETED = 0xE5;

    //Directory entry attribute flags (standard WinNT file system flags (trimmed to 8 bits))
    //unsure if FATX actually uses any flags other than directory but the below flags are
    //what gets checked so may as well support them (typical flags: file=0x00, folder=0x10)
    internal const byte FILE_ATTRIBUTE_READONLY = 0x01;
    internal const byte FILE_ATTRIBUTE_HIDDEN = 0x02;
    internal const byte FILE_ATTRIBUTE_SYSTEM = 0x04;
    internal const byte FILE_ATTRIBUTE_DIRECTORY = 0x10;
    internal const byte FILE_ATTRIBUTE_ARCHIVE = 0x20;

    //FAT entry flags
    //FATX16
    internal const ushort CLUSTER_FREE = 0x0000;
    internal const ushort CLUSTER_RESERVED = 0xFFF0;
    internal const ushort CLUSTER_BAD = 0xFFF7;
    internal const ushort CLUSTER_MEDIA = 0xFFF8; //only used for FAT cluster afaik
    internal const ushort CLUSTER_LAST = 0xFFFF;
    //FATX32
    /*
    internal const uint CLUSTER_FREE     = 0x00000000;
    internal const uint CLUSTER_RESERVED = 0xFFFFFFF0;
    internal const uint CLUSTER_BAD      = 0xFFFFFFF7;
    internal const uint CLUSTER_MEDIA    = 0xFFFFFFF8;
    internal const uint CLUSTER_LAST     = 0xFFFFFFFF;
    */
}

internal static class Functions
{
    private const int FILENAME_MAX = 42;
    private static readonly char[] VALID_CHARACTERS =
        {
                ' ', '!', '#', '$', '%', '&', '\'', '(', ')', '-', '.',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '@',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K',
                'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
                'W', 'X', 'Y', 'Z', '[', ']', '^', '_', '`', 'a', 'b',
                'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
                'y', 'z', '{', '}', '~'
            };

    internal static bool IsValidFileName(string name)
    {
        //check length
        if (string.IsNullOrEmpty(name) || name.Length > FILENAME_MAX) return false;

        //check starting characters (can't start with . or ..)
        if (name[0] == '.') return false;
        if (name.Length > 1 && (name[0] == '.' && name[1] == '.')) return false;

        //check characters are in valid range
        for (int i = 0; i < name.Length; i++)
        {
            if (!VALID_CHARACTERS.Contains(name[i])) return false;
        }

        return true;
    }

}

internal static class Kernel
{

    internal static int RoundToPages(int bytes)
    {
        int PageSize = 0x1000; //xbox memory page size
        return ((bytes + (PageSize - 1)) & ~(PageSize - 1)); //1:1 with kernel func
    }

    internal static byte RtlFindFirstSetRightMember(uint value)
    {
        //this was tough to port, even though it seems simple
        //got help understanding this from the ReactOS source
        //this is also referred to as the De Bruijn sequence

        byte[] RtlpBitsClearLow = {
                8, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                6, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                7, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                6, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                5, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0,
                4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0
            };

        if ((value & 0xFFFF) > 0)
        {
            if ((value & 0xFF) > 0)
                return RtlpBitsClearLow[value & 0xFF];
            else
                return (byte)(RtlpBitsClearLow[(value >> 8) & 0xFF] + 8);
        }
        else
        {
            if (((value >> 16) & 0xFF) > 0)
                return (byte)(RtlpBitsClearLow[(value >> 16) & 0xFF] + 16);
            else
                return (byte)(RtlpBitsClearLow[(value >> 24) & 0xFF] + 24);
        }
    }

}