    /* Raw data structures */

    public struct Header {

        public byte[] Signature;       //0x0  - HMAC-SHA-1 hash
        public uint Magic;             //0x14 - 'XCSF' chars
        public uint Size;              //0x18 - header size
        public uint _unknown;          //0x1C - usually set to a value of 1
        public uint Flags;             //0x20 - content bit flags
        public uint TitleId;           //0x24 - content title id
        public ulong OfferingId;       //0x28 - offer id + offer title id
        public uint PublisherFlags;    //0x30 - publisher/developer set bit flags

        //section headers follow (part of header)
    }

    public struct SectionHeader {

        public uint Offset;    //0x0 - offset of section data (in file)
        public uint Size;      //0x4 - size of section data
        public byte[] Hash;    //0x8 - SHA-1 hash of section data
    }

    public struct FileTableHeader {

        public uint FileEntryCount;         //0x0 - number of file entries / hashes
        public uint _unknown;               //0x4 - usually set to a value of 0
        public uint FileEntriesStartOffset; //0x8 - offset of entry structs (from start of section data)
        public byte[] FileEntryHashTable;   //0xC - blob of SHA-1 hashes of external files (size = entry count * 0x14)
    }

    public struct FileTableEntry {

        public ushort LeftChildNode;    //0x0  - binary tree index pointing to "left node" (serves as an offset to another file table entry struct), value of 0 indicates end of branch (leaf)
        public ushort RightChildNode;   //0x2  - binary tree index pointing to "right node" (serves as an offset to another file table entry struct), value of 0 indicates end of branch (leaf)
        public ushort _unknown;         //0x4  - usually set to a value of 1
        public ushort FileNameLength;   //0x6  - number of chars in external file name (relative path)
        public uint FileSize;           //0x8  - size in bytes of external file
        public uint HashStartOffset;    //0xC  - offset in external file where SHA-1 hash computation begins
        public uint HashComputeLength;  //0x10 - number of bytes in external file to compute SHA-1 hash over
        public ushort HashTableIndex;   //0x14 - index into file entry hash table where the external files SHA-1 hash is stored (hash offset = index * 0x14)
        public string FileName;         //0x16 - relative file path to external file (relative to the ContentMeta.xbx file)
    }

    public struct UpdateBlob {

        public uint _unknown;           //0x0 - usually set to a value of 1
        public uint BaseTitleVersion;   //0x4 - title version stored in the certificate of the base executable shipped on disc
        public byte[] _reserved;        //0x8 - reserved/unknown bytes (usually zeroed)
    }

    /* Friendly structs */

    public struct Localization {
        public string Default;
        public string English;
        public string Japanese;
        public string German;
        public string French;
        public string Spanish;
        public string Italian;
        public string Korean;
        public string TraditionalChinese;
        public string Brazilian;
    }
