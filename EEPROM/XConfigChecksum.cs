uint XConfigChecksum(byte[] buffer, int index, uint length) {

    ulong value = 0;
    uint carries = 0;
    uint checksum = 0;

    const ulong lower_mask = 0xFFFFFFFF00000000;
    const ulong upper_mask = 0x00000000FFFFFFFF;

    if (buffer != null && length > 0 && (index + length) <= buffer.Length) {

        for (int i = 0; i < (length / 4); i++) {

            value = checksum;
            //value += BitConverter.ToUInt32(buffer, (index + (i * 4))); //manually unpacking would be faster and avoid signed casts
            value += (buffer[(index + (i * 4))] |
                (uint)(buffer[(index + (i * 4)) + 1] << 8) |
                (uint)(buffer[(index + (i * 4)) + 2] << 16) |
                (uint)(buffer[(index + (i * 4)) + 3] << 24));

            if ((value & lower_mask) > 0)
                carries++;

            checksum = (uint)(value & upper_mask);
        }

        if (carries > 0) {
            value = checksum;
            value += carries;
            checksum = (uint)(value & upper_mask);

            if ((value & lower_mask) > 0)
                checksum++;
        }
    }

    return checksum;
}
