#define EEPROM_SIZE 0x100

/*
All labeling for structure members taken from 1.0.5993.1 kernel
*/

#pragma pack(push, 1)
typedef struct {
	unsigned char Checksum[0x14];
	unsigned char Confounder[8];
	unsigned char XboxHDKey[0x10];
	unsigned int GameRegion;
} EncryptedSection; //0x30 bytes

typedef struct {
	unsigned int Checksum;
	char SerialNumber[0xC];
	unsigned char MACAddress[6];
	unsigned char Reserved1[2];
	unsigned char OnlineKey[0x10];
	unsigned int VideoRegion;
	unsigned char Reserved2[4];
} FactorySection; //0x30 bytes

typedef struct {
	unsigned char Month;
	unsigned char Day;
	unsigned char DayOfWeek;
	unsigned char Hour;
} TimeZoneDate; //0x4 bytes

//some data configurable by user, some not
typedef struct {
	unsigned int Checksum;
	int TimeZoneBias;
	char TimeZoneStdName[4];
	char TimeZoneDltName[4];
	unsigned char Reserved1[8];
	TimeZoneDate TimeZoneStdDate;
	TimeZoneDate TimeZoneDltDate;
	unsigned char Reserved2[8];
	int TimeZoneStdBias;
	int TimeZoneDltBias;
	unsigned int Language;
	unsigned int VideoFlags;
	unsigned int AudioFlags;
	unsigned int ParentalControlGames;
	unsigned int ParentalControlPassword;
	unsigned int ParentalControlMovies;
	unsigned char OnlineIpAddress[4]; //xbl only
	unsigned char OnlineDnsAddress[4]; //xbl only
	unsigned char OnlineDefaultGatewayAddress[4]; //xbl only
	unsigned char OnlineSubnetMask[4]; //xbl only
	unsigned int MiscFlags; //used for settings that dont get their own area. so far only found "Auto power down" and "DST enabled/disabled" use this - im sure theres more
	unsigned int DvdRegion;
} UserConfigSection; //0x60 bytes

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
typedef struct {
	unsigned char FbioDelay;
	unsigned char AddrDrv;
	unsigned char CTrim2;
	unsigned char EMRS;
	unsigned char ExtSlow[10];
	unsigned char Slow[10];
	unsigned char Typical[10];
	unsigned char Fast[10];
	unsigned char ExtFast[10];
} HardwareConfigSection; //0x36 bytes

//relevant to Microsoft when they were servicing consoles
//stores information about past error codes
typedef struct { 
	unsigned char LastCode; //errors from bootloaders arent stored
	unsigned char Reserved1;
	unsigned short History; //not sure what of but does get used
} UEMInformation; //0x4 bytes

typedef struct {
	EncryptedSection EncryptedSection;
	FactorySection FactorySection;
	UserConfigSection UserSection;
	HardwareConfigSection HardwareSection;

	//unsectioned/extended data (not hashed, no validation)
	unsigned short ThermalSensorCalibration;
	unsigned char Unused[2]; //literally labeled as 'unused' in kernel, does actually get used
	UEMInformation UEMInformation;
	unsigned char Reserved1[2];
} EEPROM; //0x100 bytes
#pragma pack(pop)

//Unpacks passcode value into 4 button values
//returns 4 byte array
void UnpackPasscode(unsigned int value, unsigned char* result)
{
	result[0] = (unsigned char)((value & 0x0000FFFF) >> 12);
	result[1] = (unsigned char)((value & 0x00000FFF) >> 8);
	result[2] = (unsigned char)((value & 0x000000FF) >> 4);
	result[3] = (unsigned char)(value & 0x0000000F);
}

//Packs 4 button values into passcode value
//returns uint32
unsigned int PackPasscode(unsigned char button1, unsigned char button2, unsigned char button3, unsigned char button4)
{
	unsigned int result = 0;
	result |= (button1 << 12);
	result |= (button2 << 8);
	result |= (button3 << 4);
	result |= button4;
	return result;
}

/*
Keys used to generate a RC4 key to encrypt/decrypt the 'EncryptedSection'
*/

//1.0
#define EEPROM_KEY_1 { 0x2A, 0x3B, 0xAD, 0x2C, 0xB1, 0x94, 0x4F, 0x93, 0xAA, 0xCD, 0xCD, 0x7E, 0x0A, 0xC2, 0xEE, 0x5A }
//1.1-1.4(1.5?)
#define EEPROM_KEY_2 { 0x1D, 0xF3, 0x5C, 0x83, 0x8E, 0xC9, 0xB6, 0xFC, 0xBD, 0xF6, 0x61, 0xAB, 0x4F, 0x06, 0x33, 0xE4 }
//1.6(b)
#define EEPROM_KEY_3 { 0x2B, 0x84, 0x57, 0xBE, 0x9B, 0x1E, 0x65, 0xC6, 0xCD, 0x9D, 0x2B, 0xCE, 0xC1, 0xA2, 0x09, 0x61 }

/*
Confounders are seemly static with each EEPROM key version (in unmodified EEPROM images)
There is no check in the kernel for the confounder and it can be changed without consenquence
(guess they didn't want to run a RNG function when generating EEPROM images or figured it just didn't matter?)

This can be useful to check if an EEPROM image is decrypted/what key to use to encrypt, assuming the confounder hasn't been changed
*/

//1.0
#define CONFOUNDER_1 { 0x00, 0x00, 0x00, 0x00, 0x10, 0xA0, 0x1C, 0x00 }
//1.1-1.4(1.5?)
#define CONFOUNDER_2 { 0x0F, 0x2A, 0x20, 0xD3, 0x49, 0x17, 0xC8, 0x6D }
//1.6(b)
#define CONFOUNDER_3 { 0x4C, 0x70, 0x33, 0xCB, 0x5B, 0xB5, 0x97, 0xD2 }


/*
Decrypts the 'EncryptedSection' in an EEPROM image
returns the key version used to successfully to decrypt or 0 on failure
1.0 = keyVersion: 1
1.1-1.4(1.5?) = keyVersion: 2
1.6(b) = keyVersion: 3
*/
int DecryptEncryptedSection(EncryptedSection* section)
{
	RC4_CTX context;
	unsigned char key[0x14];
	unsigned char hash[0x14];
	unsigned char buffer[sizeof(EncryptedSection)];
	unsigned char XboxEEPROMKey[0x10];
	
	/*
	No way of checking which model an EEPROM image is from to know which EEPROM key to use
	Try each EEPROM key until decryption is successful
	Success is determined by comparing the 'checksum' data to a hmac-sha1 hash computed over the decrypted section data ('checksum' is not encrypted)
	*/

	//1.0
	memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_1, 0x10); //copy working key
	memcpy(buffer, section, sizeof(EncryptedSection)); //copy working data

	//create rc4 key from hmac-sha1 of 'checksum' using EEPROM key
	XCryptHMAC(XboxEEPROMKey, 0x10, buffer, 0x14, 0, 0, key); 
	//rc4 key setup
	XCryptRC4Key(&context, 0x14, key);
	//decrypt section (minus the 'checksum' data)
	XCryptRC4Crypt(&context, (sizeof(EncryptedSection) - 0x14), &buffer[0x14]);
	//hmac-sha1 of decrypted section
	XCryptHMAC(XboxEEPROMKey, 0x10, &buffer[0x14], (sizeof(EncryptedSection) - 0x14), 0, 0, hash); 

	//check resulting hash of section matches 'checksum' data
	if (memcmp(buffer, hash, 0x14) == 0) 
	{
		//copy working data back into struct
		memcpy(section, buffer, sizeof(EncryptedSection));

		//return key version
		return 1;
	}

	//1.1-1.4(1.5?)
	memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_2, 0x10);
	memcpy(buffer, section, sizeof(EncryptedSection));

	XCryptHMAC(XboxEEPROMKey, 0x10, buffer, 0x14, 0, 0, key);
	XCryptRC4Key(&context, 0x14, key);
	XCryptRC4Crypt(&context, (sizeof(EncryptedSection) - 0x14), &buffer[0x14]);
	XCryptHMAC(XboxEEPROMKey, 0x10, &buffer[0x14], (sizeof(EncryptedSection) - 0x14), 0, 0, hash);

	if (memcmp(buffer, hash, 0x14) == 0)
	{
		memcpy(section, buffer, sizeof(EncryptedSection));
		return 2;
	}

	//1.6(b)
	memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_3, 0x10);
	memcpy(buffer, section, sizeof(EncryptedSection));

	XCryptHMAC(XboxEEPROMKey, 0x10, buffer, 0x14, 0, 0, key);
	XCryptRC4Key(&context, 0x14, key);
	XCryptRC4Crypt(&context, (sizeof(EncryptedSection) - 0x14), &buffer[0x14]);
	XCryptHMAC(XboxEEPROMKey, 0x10, &buffer[0x14], (sizeof(EncryptedSection) - 0x14), 0, 0, hash);

	if (memcmp(buffer, hash, 0x14) == 0)
	{
		memcpy(section, buffer, sizeof(EncryptedSection));
		return 3;
	}

	return 0;
}

//Encrypts the 'EncryptedSection' in an EEPROM image
//1.0 = keyVersion: 1
//1.1-1.4(1.5?) = keyVersion: 2
//1.6(b) = keyVersion: 3
void EncryptEncryptedSection(EncryptedSection* section, int keyVersion)
{
	if (keyVersion < 1 || keyVersion > 3) return;

	RC4_CTX context;
	unsigned char key[0x14];
	unsigned char hash[0x14];
	unsigned char buffer[sizeof(EncryptedSection)];
	unsigned char XboxEEPROMKey[0x10];

	//copy working key
	if (keyVersion == 1) {
		memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_1, 0x10);
	} else if (keyVersion == 2) {
		memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_2, 0x10);
	} else {
		memcpy(XboxEEPROMKey, (unsigned char[])EEPROM_KEY_3, 0x10);
	}
	
	//copy working data
	memcpy(buffer, section, sizeof(EncryptedSection)); 

	//hmac-sha1 of unencrypted section data to create 'checksum'
	XCryptHMAC(XboxEEPROMKey, 0x10, &buffer[0x14], (sizeof(EncryptedSection) - 0x14), 0, 0, hash);

	//create rc4 key from hmac-sha1 of 'checksum' using EEPROM key (hash of a hash)
	XCryptHMAC(XboxEEPROMKey, 0x10, hash, 0x14, 0, 0, key);

	//rc4 key setup
	XCryptRC4Key(&context, 0x14, key);

	//encrypt unencrypted section data
	XCryptRC4Crypt(&context, (sizeof(EncryptedSection) - 0x14), &buffer[0x14]);

	//copy 'checksum'
	memcpy(buffer, hash, 0x14);

	//copy working data back into struct
	memcpy(section, buffer, sizeof(EncryptedSection));
}

/*
//original checksum code
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
*/

/*
OLD

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
*/

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
    if (eax > (*(unsigned int*)ecx + eax))
        ebx++;
    eax = (*(unsigned int*)ecx + eax);
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

/* 
Enums/flags taken from xboxdevwiki.net/EEPROM

Thanks to all the researchers and programmers
Thanks to xboxdevwiki maintainers for pulling this information together into one place
*/

#define GAME_REGION_NONE   0x00000000
#define GAME_REGION_NTSC_M 0x00000001
#define GAME_REGION_NTSC_J 0x00000002
#define GAME_REGION_PAL    0x00000004
#define GAME_REGION_TEST   0x40000000 //only present in later kernels, never seen this used
#define GAME_REGION_MFG    0x80000000

#define VIDEO_REGION_NONE   0x00000000
#define VIDEO_REGION_NTSC_M 0x00400100
#define VIDEO_REGION_NTSC_J 0x00400200
#define VIDEO_REGION_PAL_I  0x00800300
#define VIDEO_REGION_PAL_M  0x00400400

#define LANGUAGE_NONE       0x00000000
#define LANGUAGE_ENGLISH    0x00000001
#define LANGUAGE_JAPANESE   0x00000002
#define LANGUAGE_GERMAN     0x00000003
#define LANGUAGE_FRENCH     0x00000004
#define LANGUAGE_SPANISH    0x00000005
#define LANGUAGE_ITALIAN    0x00000006
#define LANGUAGE_KOREAN     0x00000007
#define LANGUAGE_CHINESE    0x00000008
#define LANGUAGE_PORTUGUESE 0x00000009

#define VIDEO_FLAG_480I       0x00000000 //default
#define VIDEO_FLAG_480P       0x00080000
#define VIDEO_FLAG_720P       0x00020000
#define VIDEO_FLAG_1080I      0x00040000
#define VIDEO_FLAG_WIDESCREEN 0x00010000
#define VIDEO_FLAG_LETTERBOX  0x00100000
#define VIDEO_FLAG_60HZ       0x00400000
#define VIDEO_FLAG_50HZ       0x00800000

#define AUDIO_FLAG_STEREO     0x00000000 //default
#define AUDIO_FLAG_MONO       0x00000001
#define AUDIO_FLAG_SURROUND   0x00000002
#define AUDIO_FLAG_ENABLE_AC3 0x00010000
#define AUDIO_FLAG_ENABLE_DTS 0x00020000

#define MISC_FLAG_AUTOPOWERDOWN 0x0001
#define MISC_FLAG_DISABLE_DST   0x0002

//Rating Pending (disabled)
#define PARENTAL_CONTROL_GAMES_RP 0x00000000
//Adults Only
#define PARENTAL_CONTROL_GAMES_AO 0x00000001
//Mature
#define PARENTAL_CONTROL_GAMES_M  0x00000002
//Teen
#define PARENTAL_CONTROL_GAMES_T  0x00000003
//Everyone
#define PARENTAL_CONTROL_GAMES_E  0x00000004
//Kids to Adults
#define PARENTAL_CONTROL_GAMES_KA 0x00000005
//Early Childhood
#define PARENTAL_CONTROL_GAMES_EC 0x00000006

#define PARENTAL_CONTROL_MOVIES_NONE 0x00000000
#define PARENTAL_CONTROL_MOVIES_NC17 0x00000001
#define PARENTAL_CONTROL_MOVIES_R    0x00000002
#define PARENTAL_CONTROL_MOVIES_PG13 0x00000004
#define PARENTAL_CONTROL_MOVIES_PG   0x00000005
#define PARENTAL_CONTROL_MOVIES_G    0x00000007

//Region free/region all
#define DVD_REGION_0 0x00000000
//USA, Canada
#define DVD_REGION_1 0x00000001
//Japan, Europe, South Africa, Middle East, Greenland
#define DVD_REGION_2 0x00000002
//South Korea, Taiwan, Hong Kong, Parts of South East Asia
#define DVD_REGION_3 0x00000003
//Australia, New Zealand, Latin America (including Mexico)
#define DVD_REGION_4 0x00000004
//Eastern Europe, Russia, India, Africa
#define DVD_REGION_5 0x00000005
//China
#define DVD_REGION_6 0x00000006

#define PASSCODE_UP    0x1
#define PASSCODE_DOWN  0x2
#define PASSCODE_LEFT  0x3
#define PASSCODE_RIGHT 0x4
#define PASSCODE_A     0x5
#define PASSCODE_B     0x6
#define PASSCODE_X     0x7
#define PASSCODE_Y     0x8
#define PASSCODE_LT    0xB
#define PASSCODE_RT    0xC
