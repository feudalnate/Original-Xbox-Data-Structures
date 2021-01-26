/*
Iterations of my reversing attempts of the XConfigChecksum function

The XConfigChecksum function is mainly used for EEPROM sections but is used elsewhere on the system as well

@NOTE: For EEPROM sections, the returned checksum value of a section MUST have a bitwise NOT applied to the result
*/

//ORIGINAL KERNEL FUNCTION
//(neatened up and labeled a bit)
unsigned int __stdcall XConfigChecksum(void *data, unsigned int count)
{
	__asm
	{
        push ebp               //store last base pointer for returning to caller
        mov ebp, esp           //store current stack pointer as our base pointer
        push ebx               //store return value address
        mov ecx, [ebp+data]    //store 'data' pointer value
        mov edx, [ebp+count]   //store count value
        xor eax, eax           //zero eax 
        xor ebx, ebx           //zero ebx
        shr edx, 2             //shift count value right by 2 (equivalent of 'count = count / 4')
        test edx, edx          //check count > 0
        jz L2                  //goto L2 if count == 0
        
        L1:                    //main loop, += uint32's (+1 on 32bit overflow)
        add eax, [ecx]         //sum = sum + (unsigned int)*data
        adc ebx, 0             //+1 (if overflow)
        add ecx, 4             //increment pointer (*data = *data + 4)
        dec edx                //decrement count (count = count - 1)
        jnz L1                 //goto L1 if count > 0
        
        L2:
        add eax, ebx
        adc eax, 0
        pop ebx
        pop ebp
        retn
	}
}

//FIRST ATTEMPT
//works but only for C/C++
unsigned int XConfigChecksum(unsigned char* data, int count)
{
#define PACKUINT64(high, low) (((unsigned long long)high) << 32 | low)
#define CARRYFLAG(x, y) (x > (x + y))

	unsigned int low = 0;
	unsigned int high = 0;
	unsigned long long value = 0;
	for (int i = count >> 2; i > 0; i--)
	{
		value = *(unsigned int*)data + PACKUINT64(high, low);
		high = (unsigned int)(value >> 32);
		low = (unsigned int)value;
		data = (unsigned char*)data + 4;
	}
	return (CARRYFLAG(high, low) + (high + low));
}

//FINAL FUNCTION
//this works in C/C++ and C# (with a couple tweaks)
unsigned long XConfigChecksum(unsigned char* data, int count)
{
    unsigned long result = 0;
    unsigned long overflows = 0;
    unsigned long long value;
    for (int i = 0; i < (count / 4); i++)
    {
        value = ((unsigned long long)result + *(unsigned long*)(data + (i * 4)));
        if (value > 0xFFFFFFFF) overflows++;
        result = (unsigned long)value;
    }
    value = ((unsigned long long)result + overflows);
    overflows = 0;
    if (value > 0xFFFFFFFF) overflows++;
    result = (unsigned long)value;
    return (result + overflows);
}

//FINAL FINAL function
//no casting, tried to keep within bounds of 8086 registers
//easily portable to other languages
unsigned int XConfigChecksum(unsigned char* data, unsigned int count)
{
    unsigned char* ecx; //data
    unsigned int edx;   //count
    unsigned int eax;   //checksum
    unsigned int ebx;   //num. of carries (overflows)

    ecx = data;
    edx = (count >> 2);
    eax = 0;
    ebx = 0;

    if (edx == 0)
        goto L2;

L1:
    if (eax > (*(int*)ecx + eax))
        ebx++;
    eax = (*(int*)ecx + eax);
    ecx += 4;
    edx--;
    if (edx > 0)
        goto L1;

L2:
    if (eax > (eax + ebx))
        eax = (eax + ebx) + 1;
    else
        eax = (eax + ebx);

    return eax;
}