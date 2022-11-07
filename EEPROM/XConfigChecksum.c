/*
 
Original XConfigChecksum function (i686/x86 assembly)

EAX: Checksum value
EBX: Number of carries (holds the number of times the carry flag (CF) was set during addition)
ECX: Pointer to data buffer
EDX: Number of 32bit values to itterate through (originally passed in as number of bytes in data buffer to process, gets divided by 4)

NOTES:
- Any data not in 32bit alignment is ignored
- If count is not divisible by 4 then the return value will be 0
- Bitwise NOT must be applied to the resulting sum if dealing with EEPROM data sections
- Hard disk "config" sectors use this algorithm and the resulting sum is used as-is


mov ecx, [ebp + data]  //store 'data' pointer value, ecx = data pointer
mov edx, [ebp + count] //store 'count' value, edx = count

xor eax, eax   //zero eax, eax = checksum value
xor ebx, ebx   //zero ebx, ebx = number of carries

shr edx, 2     //shift count value right by 2 (equivalent of 'count = count / 4')
test edx, edx  //check count > 0
jz FinalizeChecksum    //return if count == 0 (must be at least 32bits of data to work on)

Loop:
add eax, [ecx] //sum = sum + *(unsigned int*)data
adc ebx, 0     //increment number of carries if CF is set
add ecx, 4     //increment pointer (data = data + 4)
dec edx//decrement count (count = count - 1)
jnz Loop       //continue loop if count > 0

FinalizeChecksum :
add eax, ebx   //add number of carries to the final checksum value
adc eax, 0     //add a final carry if (checksum + carries) resulted in CF being set

retn

*/

typedef unsigned char byte;
typedef unsigned int u32;
typedef unsigned long long u64;

u32 XConfigChecksum(byte* data, u32 count) {

    u64 value = 0;
    u32 carries = 0;
    u32 checksum = 0;

    const u64 lower_mask = 0xFFFFFFFF00000000;
    const u64 upper_mask = 0x00000000FFFFFFFF;

    if (data && count > 0) {

        for (u32 i = 0; i < (count / 4); i++) {

            value = checksum;
            value += *(u32*)(data + (i * 4));

            if ((value & lower_mask) > 0)
                carries++;

            checksum = (value & upper_mask);
        }

        if (carries > 0) {
            value = checksum;
            value += carries;
            checksum = (value & upper_mask);

            if ((value & lower_mask) > 0)
                checksum++;
        }
    }

    return checksum;
}
