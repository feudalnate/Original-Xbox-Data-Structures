# The Xbox EEPROM

### Table of Contents
- [What is an EEPROM?](#what-is-an-eeprom)
- [The Purpose of the EEPROM](#the-purpose-of-the-eeprom)
- [Physical Location and Addresses](#physical-location-and-addresses)
- [EEPROM Devices](#eeprom-devices)
- [EEPROM Data Structure](#eeprom-data-structure)
  - [EEPROM Sections Overview](#eeprom-sections-overview)
  - [EEPROM Layout](#eeprom-layout)
  - [Note: Hardware section](#note-hardware-section)
  - [Time Zone Date structure](#time-zone-date-structure)
  - [UEM Information structure](#uem-information-structure)
  - [Game Region Flags](#game-region-flags)
  - [Video Standard Flags](#video-standard-flags)
  - [Language Flags](#language-flags)
  - [Video Flags](#video-flags)
  - [Audio Flags](#audio-flags)
  - [Parental Control (Games) Flags](#parental-control-games-flags)
  - [Parental Control Password (Button) Flags](#parental-control-password-button-flags)
  - [Parental Control (Movies) Flags](#parental-control-movies-flags)
  - [Misc. Flags](#misc-flags)
  - [DVD Region Flags](#dvd-region-flags)
- [Encryption, Hashing, and Checksums](#encryption-hashing-and-checksums)
  - [XConfigChecksum](#xconfigchecksum)
- [Resetting EEPROM Data for the Xbox OOBE](#resetting-eeprom-data-for-the-xbox-oobe)
  - [How to reset EEPROM data](#how-to-reset-eeprom-data)
  - [Xbox Out-of-Box Experience](#xbox-out-of-box-experience)
- [References and Notes](#references-and-notes)
  - [References](#references)
  - [Notes](#notes)

---

### What is an EEPROM?
"EEPROM (also E²PROM) stands for electrically erasable programmable read-only memory and is a type of non-volatile memory used in computers, integrated in microcontrollers for smart cards and remote keyless systems, and other electronic devices to store relatively small amounts of data by allowing individual bytes to be erased and reprogrammed." [_-Wikipedia_](https://en.wikipedia.org/wiki/EEPROM)

---

### The Purpose of the EEPROM
In short, the EEPROM on the original Xbox stores **256 bytes** worth of **global system settings and system configuration data**. The EEPROM holds settings and configuration for the [system kernel](https://en.wikipedia.org/wiki/Kernel_(operating_system)), [drivers](https://en.wikipedia.org/wiki/Device_driver), [hardware](https://en.wikipedia.org/wiki/Computer_hardware), and the [Xbox Dashboard](https://en.wikipedia.org/wiki/Xbox_system_software#Xbox_(first_generation)_software) that are required to persist through a power cycle. These settings can range from the date and time to [crytographic keys](https://en.wikipedia.org/wiki/Key_(cryptography)) to [region code locking](https://en.wikipedia.org/wiki/Regional_lockout) and even memory ([RAM](https://en.wikipedia.org/wiki/Random-access_memory)) initialization data.<br /><br />The EEPROM serves a critical role in both the software and hardware chains, and as such, the Xbox will not function without it. 

---

### Physical Location and Addresses
[![N|Solid](https://i.imgur.com/x13Y69v.png)](https://i.imgur.com/905wc9l.png)[![N|Solid](https://i.imgur.com/55RSasY.png)](https://i.imgur.com/sfbO0Jn.png)

Physically, the EEPROM is connected to the [System Management Bus (SMBus)](https://en.wikipedia.org/wiki/System_Management_Bus)/[I²C](https://en.wikipedia.org/wiki/I%C2%B2C) interface which is hosted by the nVidia [MCPX](https://web.archive.org/web/20210312203818/https://xboxdevwiki.net/MCPX) Southbridge (Media Communication Processor for Xbox).

- [Addresses reference](https://web.archive.org/web/20210313003229/https://xboxdevwiki.net/SMBus)
  - When accessed by another I²C slave hardware device, the EEPROM has a _hardware address_ of **0x54**
  - When accessed via software through the SMBus, the EEPROM has a _software address_ of **0xA8**

```C
#define EEPROM_HARDWARE_ADDRESS 0x54
#define EEPROM_SOFTWARE_ADDRESS (EEPROM_HARDWARE_ADDRESS << 1)
```
 
### EEPROM Devices
Many different models and manufacturers were used as a source for EEPROM devices on the Xbox, all of which were more-or-less the same. All EEPROM devices on the Xbox have a storage capability of 2048 bits (256 bytes) with 8 byte paging alignment. **_It is unknown to the author if the [pinout](https://en.wikipedia.org/wiki/Pinout) of the various EEPROM devices on the Xbox is shared between models of EEPROM and Xbox motherboard revisions but it is assumed._**

[Catalyst 24WC02](https://archive.org/details/cat24wc02) is one of many various models of EEPROM devices that can be found on a Xbox motherboard, which has the following pinout:
![](https://i.imgur.com/n8nBxqP.png)
> NOTE: Different EEPROM devices may have different pinouts and voltage tolerances. Image is for example only.

---

### EEPROM Data Structure

Much of the data contained in the Xbox EEPROM is organized into and accessed as [sections](https://en.wikipedia.org/wiki/Data_structure). Commonly referenced fields (such as Game Region) can be specifically accessed as well. Sections contain [static data](https://web.archive.org/web/20210312215601/https://www.techopedia.com/definition/31590/static-data) set during manufacturing, kernel-only modifiable data, and user configurable data. There is kernel-only data that lives outside these defined sections as well.

The system kernel exports 2 public function calls for manipulating data stored in the EEPROM. Although these functions have public access in the kernel, developers were not permitted to call them. Code submitted to certification with these function calls would be denied certification.

```C
//kernel functions for accessing and modifying EEPROM data

uint32 ExQueryNonVolatileSetting(uint32 ValueIndex, uint32* Type, void* Value, uint32 ValueLength, uint32* ResultLength);
uint32 ExSaveNonVolatileSetting(uint32 ValueIndex, uint32 Type, void* Value, uint32 ValueLength);
```

#### EEPROM Sections Overview
| Section | Offset | Length |
|-|-|-|
| Factory Encrypted | 0x0 | 0x30 |
| Factory | 0x30 | 0x30 |
| User | 0x60 | 0x60 |
| Hardware | 0xC0 | 0x36 |
| Unsectioned | 0xF6 | 0xA |

#### EEPROM Layout
| Name | Offset | Length | Type | Section | Comment | User Configurable |
|-|-|-|-|-|-|-|
| Checksum | 0x00 | 0x14 | byte[] | **Factory Encrypted** | HMAC SHA-1 hash of the **Factory Encrypted** section. *See [Encryption, Hashing, and Checksums](#encryption-hashing-and-checksums)* | No |
| Confounder | 0x14 | 0x8 | byte[] | **Factory Encrypted**| Used for further obfuscation in RC4 encryption of the **Factory Encrypted** section. Static value based on motherboard revision | No |
| XboxHDKey | 0x1C | 0x10 | byte[] | **Factory Encrypted** | Key used for many purposes throughout the system such as generating signing keys for various types of content and most notably, generating the hard drive locking password | No |
| Game Region | 0x2C | 0x4 | BitFlags | **Factory Encrypted** | Game region lock setting. *See [Game Region Flags](#game-region-flags)* | No |
| Checksum | 0x30 | 0x4 | uint32 | **Factory** | [XConfigChecksum](#xconfigchecksum) checksum of **Factory** section | No |
| Serial Number | 0x34 | 0xC | ASCII | **Factory** | Manufacuring set serial number of the console. Fixed size | No |
| MAC Address | 0x40 | 0x6 | byte[] | **Factory** | Manufacuring set MAC address for ethernet adapter. User set MAC address is stored in ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) of hard drive | No |
| Reserved | 0x46 | 0x2 | byte[] | **Factory** | Unused | No |
| Online Key | 0x48 | 0x10 | byte[] | **Factory** | Manufacturing set key for generating Xbox LIVE session keys for matchmaking and peer-to-peer connections | No |
| Video Standard | 0x58 | 0x4 | BitFlags | **Factory** | Manufacturing set video standard (ex: PAL-I video mode + 50hz refresh rate). Video Standard can be changed without the need for changing the Game Region. *See [Video Standard Flags](#video-standard-flags)* | No |
| Reserved | 0x5C | 0x4 | byte[] | **Factory** | Unused | No |
| Checksum | 0x60 | 0x4 | uint32 | **User** | [XConfigChecksum](#xconfigchecksum) checksum of **User** section | No |
| Time Zone Bias | 0x64 | 0x4 | int32 | **User** | Time zone bias currently in use (in minutes). Calculated when a user sets a time zone, based on whether daylight savings is enabled or not | No |
| Time Zone Standard Name | 0x68 | 0x4 | ASCII | **User** | User set time zone (e.g.: PST, CST, EST, etc.). Fixed size, null-trimmed | Yes |
| Time Zone Daylight Name | 0x6C | 0x4 | ASCII | **User** | User set daylight savings time zone (e.g.: PDT, CDT, EDT, etc.). Fixed size, null-trimmed. Calculated when a user sets a timezone and used when daylight savings is enabled | No |
| Reserved | 0x70 | 0x8 | byte[] | **User** | Unused | No |
| Time Zone Standard Date | 0x78 | 0x4 | struct | **User** | User set date. *See [Time Zone Date structure](#time-zone-date-structure)* | Yes |
| Time Zone Daylight Date | 0x7C | 0x4 | struct | **User** | User set daylight savings date. Calculated when a user sets the date and used when daylight savings is enabled. *See [Time Zone Date structure](#time-zone-date-structure)* | No |
| Reserved | 0x80 | 0x8 | byte[] | **User** | Unused | No |
| Time Zone Standard Bias | 0x88 | 0x4 | int32 | **User** | Time zone standard bias calculated from user selected "Time Zone Standard Name" value. Value copied to "Time Zone Bias" when daylight savings is disabled | No |
| Time Zone Daylight Bias | 0x8C | 0x4 | int32 | **User** | Time zone daylight savings bias calculated from "Time Zone Daylight Name" value. Value copied to "Time Zone Bias" when daylight savings is enabled | No |
| Language | 0x90 | 0x4 | BitFlags | **User** | User set system language. *See [Language Flags](#language-flags)* | Yes |
| Video Flags | 0x94 | 0x4 | BitFlags | **User** | User set video settings (720p, 1080i, widescreen, letterbox, etc.). *See [Video Flags](#video-flags)* | Yes |
| Audio Flags | 0x98 | 0x4 | BitFlags | **User** | User set audio settings (stereo, surround, Dolby Digital, etc.). *See [Audio Flags](#audio-flags)* | Yes |
| Game Rating Parental Control | 0x9C | 0x4 | BitFlags | **User** | Parental control setting for game rating limit. *See [Parental Control (Games) Flags](#parental-control-games-flags)* | Yes |
| Parental Control Password | 0xA0 | 0x4 | uint32 | **User** | Bitpacked button values. Only uint16 worth of data is stored. *See [Parental Control Password (Button) Flags](#parental-control-password-button-flags)* | Yes |
| Movie Rating Parental Control | 0xA4 | 0x4 | BitFlags | **User** | Parental control setting for movie (DVD) rating limit. *See [Parental Control (Movies) Flags](#parental-control-movies-flags)* | Yes |
| Online IP Address | 0xA8 | 0x4 | byte[] | **User** | *Deprecated* - User set static IPV4 address, 1 octet stored per byte.  Moved to ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) of hard drive before retail release of the Xbox | No |
| Online DNS Address | 0xAC | 0x4 | byte[] | **User** | *Deprecated* - User set static DNS address, 1 octet stored per byte.  Moved to ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) of hard drive before retail release of the Xbox | No |
| Online Default Gateway Address | 0xB0 | 0x4 | byte[] | **User** | *Deprecated* - User set static default gateway address, 1 octet stored per byte.  Moved to ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) of hard drive before retail release of the Xbox | No |
| Online Subnet Mask | 0xB4 | 0x4 | byte[] | **User** | *Deprecated* - User set static subnet mask, 1 octet stored per byte.  Moved to ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) of hard drive before retail release of the Xbox | No |
| Misc. Flags | 0xB8 | 0x4 | BitFlags | **User** | Stores various settings that don't get their own area (daylight savings enabled, auto-shutdown enabled, etc.). *See [Misc. Flags](#misc-flags)* | Yes |
| DVD Region | 0xBC | 0x4 | BitFlags | **User** | DVD region lock setting. It is unknown how this value is initially set, either during manufacting or through software such as the DVD playback software stored on the [Xbox DVD Playback Kit](https://web.archive.org/web/20210314022429/https://xboxdevwiki.net/Xbox_DVD_Movie_Playback_Kit) dongle. *See [DVD Region Flags](#dvd-region-flags)* | No |
| FBIO Delay | 0xC0 | 0x1 | byte | **Hardware** | Unknown | No |
| Address Drive | 0xC1 | 0x1 | byte | **Hardware** | Unknown | No |
| Clock Trim 2 | 0xC2 | 0x1 | byte | **Hardware** | Unknown | No |
| EMRS | 0xC3 | 0x1 | byte | **Hardware** | Unknown | No |
| Extended Slow | 0xC4 | 0xA | byte[] | **Hardware** | Unknown | No |
| Slow | 0xCE | 0xA | byte[] | **Hardware** | Unknown | No |
| Typical | 0xD8 | 0xA | byte[] | **Hardware** | Unknown | No |
| Fast | 0xE2 | 0xA | byte[] | **Hardware** | Unknown | No |
| Extended Fast | 0xEC | 0xA | byte[] | **Hardware** | Unknown | No |
| Thermal Sensor Calibration | 0xF6 | 0x2 | int16 | **Unsectioned** | Unknown | No |
| Unused | 0xF8 | 0x2 | byte[] | **Unsectioned** | Unknown. Despite the name, does get used | No |
| UEM Information | 0xFA | 0x4 | struct | **Unsectioned** | [Fatal error code](https://web.archive.org/web/20210313220229/https://xboxdevwiki.net/Fatal_Error) history. Only stores errors triggered by the system kernel. ["Service required"](https://i.imgur.com/VFtudiJ.jpg) error codes (5-21). *See [UEM Information structure](#uem-information-structure)* | No |
| Reserved | 0xFE | 0x2 | byte[] | **Unsectioned** | Unused | No |

> #### NOTE: Hardware section<br/><br/>
> Hardware section contains a (partial?) [drive strength](https://www.google.com/search?q=drive+strength+in+electronics)/[slew rate](https://en.wikipedia.org/wiki/Slew_rate) calibration datatable. This has to do with the system [SDRAM](https://en.wikipedia.org/wiki/Synchronous_dynamic_random-access_memory) voltage and timing calibration.<br/><br/>
> **This section only exists on 1.6 (Samsung SDRAM) and 1.6B (Hynix SDRAM) revision motherboards.** Prior revision motherboards have this section of the EEPROM reserved.<br/><br/>
> This section is read by the second-stage bootloader (2BL) and passed to the nVidia [NV2A](https://web.archive.org/web/20210313210231/https://xboxdevwiki.net/NV2A) GPU/Northbridge BIOS.<br/><br/>
> The data contained in the hardware section is static and **revision specific**. All 1.6 revisions share one datatable and all 1.6B revisions share another.<br/><br/>
> Prior revision motherboards have this datatable hard-coded into the system BIOS image. It is unknown to the author why this is not the case for 1.6/1.6B revisions as well. Perhaps the change in the GPU model to "XGPU-B" on 1.6/1.6B revisions or uncertainty of SDRAM manufacturer supply made this necessary. The rationality of this change from prior revisions of motherboards can only be speculated.


#### Time Zone Date structure
| Name | Offset | Length | Type | Comment |
|-|-|-|-|-|
| Month | 0x0 | 0x1 | byte | Integer value representing month (1-12) |
| Day | 0x1 | 0x1 | byte | Integer value representing day (1-31) |
| Day of Week | 0x2 | 0x1 | byte | Enum value representing day of the week (Sunday, Monday, etc.). (0-6) |
| Hour | 0x3 | 0x1 | byte | Integer value representing hour (24 hour based) (0-23) |

#### UEM Information structure
| Name | Offset | Length | Type | Comment |
|-|-|-|-|-|
| Last Code | 0x0 | 0x1 | byte | Literal value of last error code. Only stores errors triggered by the system kernel. (5-21) |
| Reserved | 0x1 | 0x1 | byte | Unused |
| History | 0x2 | 0x2 | uint16 | Bitpacked value storing history of past error codes. When "Last Code" is updated, this value is updated as well. |
> History = (History | (1 << (Last Error - 5))

#### Game Region Flags
```C
#define GAME_REGION_NTSC_M 0x00000001 //XC_GAME_REGION_NA
#define GAME_REGION_NTSC_J 0x00000002 //XC_GAME_REGION_JAPAN
#define GAME_REGION_PAL    0x00000004 //XC_GAME_REGION_RESTOFWORLD
#define GAME_REGION_TEST   0x40000000 //XC_GAME_REGION_INTERNAL_TEST
#define GAME_REGION_MFG    0x80000000 //XC_GAME_REGION_MANUFACTURING
```

#### Video Standard Flags
```C
//NOTE: Video Standard is a pre-bitmasked value set during manufacturing and is a combination of 2 types of flags bitwise OR'd together

#define AV_REGION_NTSC_M 0x00000100
#define AV_REGION_NTSC_J 0x00000200
#define AV_REGION_PAL_I  0x00000300
#define AV_REGION_PAL_M  0x00000400

#define REFRESH_RATE_60HZ 0x00400000
#define REFRESH_RATE_50HZ 0x00800000

//Pre-bitmasked values found in the Video Standard field
#define VIDEO_STANDARD_NTSC_M 0x00400100 // (AV_REGION_NTSC_M | REFRESH_RATE_60HZ)
#define VIDEO_STANDARD_NTSC_J 0x00400200 // (AV_REGION_NTSC_J | REFRESH_RATE_60HZ)
#define VIDEO_STANDARD_PAL_I  0x00800300 // (AV_REGION_PAL_I  | REFRESH_RATE_50HZ)
#define VIDEO_STANDARD_PAL_M  0x00400400 // (AV_REGION_PAL_M  | REFRESH_RATE_60HZ)
```

#### Language Flags
```C
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
```

#### Video Flags
```C
//NOTE: Video Flags field contains a bitmasked value of various flags bitwise OR'd together

#define VIDEO_FLAG_480I       0x00000000 //default
#define VIDEO_FLAG_480P       0x00080000
#define VIDEO_FLAG_720P       0x00020000
#define VIDEO_FLAG_1080I      0x00040000
#define VIDEO_FLAG_WIDESCREEN 0x00010000
#define VIDEO_FLAG_LETTERBOX  0x00100000
#define VIDEO_FLAG_60HZ       0x00400000
#define VIDEO_FLAG_50HZ       0x00800000
```

#### Audio Flags
```C
//NOTE: Audio Flags field contains a bitmasked value of various flags bitwise OR'd together

#define AUDIO_FLAG_STEREO     0x00000000 //default
#define AUDIO_FLAG_MONO       0x00000001
#define AUDIO_FLAG_SURROUND   0x00000002
#define AUDIO_FLAG_ENABLE_AC3 0x00010000
#define AUDIO_FLAG_ENABLE_DTS 0x00020000
```

#### Parental Control (Games) Flags
```C
#define PARENTAL_CONTROL_GAMES_RP 0x00000000 //Rating Pending (disabled)
#define PARENTAL_CONTROL_GAMES_AO 0x00000001 //Adults Only
#define PARENTAL_CONTROL_GAMES_M  0x00000002 //Mature
#define PARENTAL_CONTROL_GAMES_T  0x00000003 //Teen
#define PARENTAL_CONTROL_GAMES_E  0x00000004 //Everyone
#define PARENTAL_CONTROL_GAMES_KA 0x00000005 //Kids to Adults
#define PARENTAL_CONTROL_GAMES_EC 0x00000006 //Early Childhood
```

#### Parental Control Password (Button) Flags
```C
//NOTE: Parental control password is a 32bit field but only stores a 16bit value. Passcode button values are bitpacked to uint16 as 'nibbles' (4 bits)

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

//Unpacks password value into 4 passcode button values
//returns 4 byte array
void UnpackPasscode(uint32 value, byte* result)
{
    result[0] = (byte)((value & 0x0000FFFF) >> 12);
    result[1] = (byte)((value & 0x00000FFF) >> 8);
    result[2] = (byte)((value & 0x000000FF) >> 4);
    result[3] = (byte)(value & 0x0000000F);
}

//Packs 4 passcode button values into password value
//returns uint32
uint32 PackPasscode(byte button1, byte button2, byte button3, byte button4)
{
    uint32 result = 0;
    
    result |= (button1 << 12);
    result |= (button2 << 8);
    result |= (button3 << 4);
    result |= button4;
    
    return result;
}
```

#### Parental Control (Movies) Flags
```C
#define PARENTAL_CONTROL_MOVIES_NONE 0x00000000
#define PARENTAL_CONTROL_MOVIES_NC17 0x00000001
#define PARENTAL_CONTROL_MOVIES_R    0x00000002
#define PARENTAL_CONTROL_MOVIES_PG13 0x00000004
#define PARENTAL_CONTROL_MOVIES_PG   0x00000005
#define PARENTAL_CONTROL_MOVIES_G    0x00000007
```

#### Misc. Flags
```C
//NOTE: Misc. Flags field contains a bitmasked value of various flags bitwise OR'd together. Defined below are known flags, more may exist

#define MISC_FLAG_AUTOPOWERDOWN 0x0001 //if set, "Auto-shutdown after 6 hours idle" functionality is enabled
#define MISC_FLAG_DISABLE_DST   0x0002 //if set, daylight savings is disabled
```

#### DVD Region Flags
```C
#define DVD_REGION_0 0x00000000 //Region free (never set by manufacturing or other retail software but is supported in the kernel)
#define DVD_REGION_1 0x00000001 //USA, Canada
#define DVD_REGION_2 0x00000002 //Japan, Europe, South Africa, Middle East, Greenland
#define DVD_REGION_3 0x00000003 //South Korea, Taiwan, Hong Kong, Parts of South East Asia
#define DVD_REGION_4 0x00000004 //Australia, New Zealand, Latin America (including Mexico)
#define DVD_REGION_5 0x00000005 //Eastern Europe, Russia, India, Africa
#define DVD_REGION_6 0x00000006 //China
```

---

### Encryption, Hashing, and Checksums

EEPROM data is secured and validated by 2 cryptographic algorithms and 1 non-cryptographic checksum algorithm

- [RC4 encryption algorithm](https://en.wikipedia.org/wiki/RC4)
- [HMAC](https://en.wikipedia.org/wiki/HMAC) [SHA-1 hash algorithm](https://en.wikipedia.org/wiki/SHA-1)
- [XConfigChecksum](#xconfigchecksum) checksum algorithm

The **Encrypted Factory** section is hashed using HMAC SHA-1 and the resulting hash value is stored in the **Encrypted Factory** section "Checksum" field. The ["XboxEEPROMKey"](https://web.archive.org/web/20210314032018/https://xboxdevwiki.net/Kernel/XboxEEPROMKey) is used as the key for HMAC SHA-1 hashing of the **Encrypted Factory** section.
The XboxEEPROMKey is stored in the second-stage bootloader (2BL), passed to the kernel on kernel initialization, and stored in memory until the Xbox is powered off. This key is 0x10 bytes in length and is publicly exported by the kernel.

The XboxEEPROMKey is revision dependent: 1.0, 1.1-1.4, and 1.6-1.6B are the 3 unique versions of this key. These key versions are not interchangeable.

```C
//NOTE: Confounder values are listed here as static values but there is no consequence to changing them

//1.0 revision
#define XboxEEPROMKey { 0x2A, 0x3B, 0xAD, 0x2C, 0xB1, 0x94, 0x4F, 0x93, 0xAA, 0xCD, 0xCD, 0x7E, 0x0A, 0xC2, 0xEE, 0x5A }
#define Confounder    { 0x00, 0x00, 0x00, 0x00, 0x10, 0xA0, 0x1C, 0x00 }

//1.1-1.4 revisions
#define XboxEEPROMKey { 0x1D, 0xF3, 0x5C, 0x83, 0x8E, 0xC9, 0xB6, 0xFC, 0xBD, 0xF6, 0x61, 0xAB, 0x4F, 0x06, 0x33, 0xE4 }
#define Confounder    { 0x0F, 0x2A, 0x20, 0xD3, 0x49, 0x17, 0xC8, 0x6D }

//1.6-1.6B revisions
#define XboxEEPROMKey { 0x2B, 0x84, 0x57, 0xBE, 0x9B, 0x1E, 0x65, 0xC6, 0xCD, 0x9D, 0x2B, 0xCE, 0xC1, 0xA2, 0x09, 0x61 }
#define Confounder    { 0x4C, 0x70, 0x33, 0xCB, 0x5B, 0xB5, 0x97, 0xD2 }
```

The **Encrypted Factory** section is encrypted/decrypted using RC4 encryption.
The resulting HMAC SHA-1 hash of the **Encrypted Factory** section is once again hashed using HMAC SHA-1 using the XboxEEPROMKey (hash of a hash), this hash is then used as the key for encrypting the **Encrypted Factory** section.

> Encryption/decryption begins from the starting offset of the "Confounder" field (0x14).
> Encryption/decryption ends at the end of the **Factory Encrypted** section (0x30).
> RC4 is used to encrypt/decrypt **a total of 0x1C bytes** of data in the **Factory Encrypted** section.
>
> The "Checksum" field is **not** encrypted.

```C
//HMAC SHA-1 of Encrypted Factory section data to create "Checksum" value
XCryptHMAC(XboxEEPROMKey, 0x10, &EncryptedFactorySection[0x14], 0x1C, 0, 0, Checksum);

//create RC4 key from HMAC SHA-1 of "Checksum" value (hash of a hash)
XCryptHMAC(XboxEEPROMKey, 0x10, Checksum, 0x14, 0, 0, Key);

//initialize RC4 context
XCryptRC4Key(&RC4Context, 0x14, Key);

//encrypt the Encrypted Factory section data with RC4
XCryptRC4Crypt(&RC4Context, 0x1C, &EncryptedFactorySection[0x14]);

//store "Checksum" value in **Encrypted Factory** section ...
```

When decrypting the **Encrypted Factory** section, the "Checksum" field value is hashed using HMAC SHA-1, once again using the XboxEEPROMKey as the key. The resulting hash is used as the key for RC4 decryption.
Once decrypted, the **Encrypted Factory** section is hashed using HMAC SHA-1 (again, using the XboxEEPROMKey as the key) and the resulting hash is compared to the stored "Checksum" value. If the "Checksum" value matches the resulting hash then decryption was successful and the section is validated.

```C
//create RC4 key from HMAC SHA-1 of "Checksum" value (hash of a hash)
XCryptHMAC(XboxEEPROMKey, 0x10, Checksum, 0x14, 0, 0, Key);
  
//initialize RC4 context
XCryptRC4Key(&RC4Context, 0x14, Key);
  
//decrypt the Encrypted Factory section data with RC4
XCryptRC4Crypt(&RC4Context, 0x1C, &EncryptedFactorySection[0x14]);

//HMAC SHA-1 of Encrypted Factory section data to create a temporary hash value
XCryptHMAC(XboxEEPROMKey, 0x10, &EncryptedFactorySection[0x14], 0x1C, 0, 0, TemporaryHash);

//compare temporary hash to stored "Checksum" value
if (memcmp(TemporaryHash, Checksum) == 0) {
  //decryption was successful and Encryption Factory section is validated ...
}
else {
  //decryption failed or Encryption Factory section is invalid ...
}
```


#### XConfigChecksum

The XConfigChecksum is a non-cryptographic checksum algorithm unique to the Xbox and is a private non-exported function of the kernel.
The XConfigChecksum is used to validate the **Factory** and **User** sections of the EEPROM data. It is also used to validate the ["config" sector](https://web.archive.org/web/20210313200534/https://xboxdevwiki.net/Config_Sector) stored on the hard drive.

The algorithm is very simple:

- Iterate over the input buffer, 4 bytes at a time
  - Add the uint32 value to the sum
    - After each addition, the [Carry Flag](https://en.wikipedia.org/wiki/Carry_flag) in the CPU's [EFLAGS register](https://en.wikipedia.org/wiki/FLAGS_register) is checked
      - If the Carry Flag is set then a [wrap around (overflow)](https://en.wikipedia.org/wiki/Integer_overflow) has occurred and the sum is incremented by 1 (despite the naming, no value is carried during calculation)
- After the sum is calculated a bitwise NOT is applied to the result

```C
uint32 XConfigChecksum(byte* buffer, int32 count)
{
    //check if there is at least 32bits of data to operate on (any data not in 32bit alignment is not included in the resulting sum)
    if ((count / 4) == 0)
        return 0xFFFFFFFF; // ~0 = 0xFFFFFFFF

    //64bit buffer value, any overflow that occurs during addition will be stored in the upper 32bits of this buffer
    uint64 temp;

    //32bit checksum value
    uint32 checksum = 0;

    //loop through all 32bit values
    for (int32 i = 0; i < (count / 4); i++)
    {
        //copy current 32bit checksum value into 64bit temp. buffer 
        temp = checksum;

        //add 32bit value from input buffer
        temp += *(uint32*)buffer[(i * 4)];

        //cast 64bit temp. buffer back into 32bit checksum value, trimming the upper 32bits of our overflow area
        checksum = (uint32)temp; 

        //check if a 32bit overflow occurred during addition, somewhat equivalent to the Carry Flag check
        if (temp > 0xFFFFFFFF)
            checksum++; //if an overflow occurred, increment the checksum value
    }

    //return the checksum value with a bitwise NOT applied
    return ~checksum;
}
```

---

### Resetting EEPROM Data for the Xbox OOBE

EEPROM data can be "reset" to effectively force the system kernel to believe it is in a "fresh from factory" state. Tricking the kernel into believing it is in a factory state will cause the system to prompt the user for initial system setup - or the ["Out-of-Box Experience" (OOBE)](https://en.wikipedia.org/wiki/Out-of-box_experience) as manufacturers call it.


#### How to reset EEPROM data

Resetting EEPROM data for the OOBE is very simple:

- Set each field in the **User** section of the EEPROM to **0** (hexadecimal zero, _not text_)
- Set the "Checksum" field of the **User** section to **0xFFFFFFFF**

> **WARNING!**
>
> Resetting to the OOBE requires the official Xbox Dashboard executable (xboxdash.xbe) to be present on the C:\ partition of the hard drive.
> Softmodded Xbox consoles have this executable overwritten with an exploitable executable.
>
> _Resetting to the OOBE on a softmodded Xbox may have unforeseen consequences._


#### Xbox Out-of-Box Experience

After "resetting" EEPROM data and power cycling the console, the user will be prompted for the following settings

###### Language
![](https://i.imgur.com/hG3NI7c.png)

###### Time Zone
![](https://i.imgur.com/2wewIvv.png)

###### Daylight Savings
![](https://i.imgur.com/xV5m3xN.png)

### References and Notes

#### References
This documents purpose is to serve as educational information and a means of technological preservation

Many sites were used for research references into the Xbox EEPROM data, ranging from homebrew development wiki's to old random posts on now offline and archived forums.

Research was done over many months and not all references can be remembered or listed. Below are the most notable

- [XboxDevWiki](https://xboxdevwiki.net) - a wiki dedicated to research of the original Microsoft Xbox
- [Xbox Linux](https://web.archive.org/web/20050320032122/http://www.xbox-linux.org) - the first homebrew wiki that was dedicated to researching homebrew development and Linux on the original Xbox

#### Notes
Much of the data structuring and code based research was acquired from (software) reverse engineering the original Xbox 1.0.5933.1 kernel (last publicly released kernel)

##### Author self-notes
- More research is required into the Misc. Flags bitflags, it is unknown if there are more. These flags are seemly based on the Xbox Dashboard offered features and there are many revisions of the Xbox Dashboard.

- More research into the **Hardware** section on 1.6-1.6B revisions of the motherboard is required. The fields in this structure are much more related to hardware engineering knowledge and not programming knowledge.

- More research into the "Thermal Sensor Calibration" and "Unused" fields stored in the **Unsectioned** section is required.

- The structure of the "config" sector should be provided in this document as well. Although this structure is not part of the EEPROM, the data is still very relevant to the configuration type purposes of the system much like the EEPROM.

- Separating the sections in the EEPROM data table would be ideal in making it more readable. At the time of writing GitHub does not offer a way of doing a sort of horizontal rule through a table.

- Rewording of some things, further linking to explanations of technical terminology, and further improvements on the formatting of this document is likely needed.

- Further spell-checking, I'm sure there's spelling mistakes in here somewhere that I missed.
