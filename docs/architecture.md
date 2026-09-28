# End-to-End System Architecture: Identiv SPR332 & NXP JCOP 4 P71

**Platform**: Windows 10/11 x64 PC/SC Subsystem  
**Reader**: Identiv SPR332 (SPRx32) Class 2 USB PINpad  
**Card**: NXP SmartMX3 P71D321 running JCOP 4 OS (Java Card 3.0.5 Classic)  

---

## 1. Five-Layer Architecture Overview

```
+---------------------------------------------------------------------------------------+
| LAYER 5: APPLICATION LAYER (Host Tools & Card Applets)                                |
|   - Host: tools/card_tool.py, tools/tlv_tool.py, tools/pinpad_tool.py                 |
|   - Card: org.speckit.javacard.berstorage.BerStorageApplet                            |
+---------------------------------------------------------------------------------------+
                                           |
                                           v
+---------------------------------------------------------------------------------------+
| LAYER 4: APDU & SECURITY PROTOCOL LAYER                                               |
|   - ISO/IEC 7816-4 APDU Framing (CLA, INS, P1, P2, Lc, Data, Le, SW1/SW2)             |
|   - GlobalPlatform 2.3 Secure Channel Protocol (SCP02 Option 55, 3DES Session Keys)   |
|   - PC/SC Part 10 Control Structures (PIN_VERIFY_STRUCTURE)                           |
+---------------------------------------------------------------------------------------+
                                           |
                                           v
+---------------------------------------------------------------------------------------+
| LAYER 3: PC/SC SUBSYSTEM & CCID DRIVER LAYER                                          |
|   - Windows Native WinSCard (winscard.dll -> Smart Card Service SCardSvr)             |
|   - Microsoft Inbox USB CCID Class Driver (usbccid.sys)                               |
+---------------------------------------------------------------------------------------+
                                           |
                                           v
+---------------------------------------------------------------------------------------+
| LAYER 2: READER HARDWARE LAYER (Identiv SPR332)                                       |
|   - Microcontroller & USB 2.0 CCID Controller                                         |
|   - Class 2 PINpad Keypad Matrix (16 keys) & Acoustic Buzzer                          |
|   - ISO/IEC 7816-3 Smart Card Interface (VCC, GND, CLK, RST, I/O)                     |
+---------------------------------------------------------------------------------------+
                                           |
                                           v
+---------------------------------------------------------------------------------------+
| LAYER 1: SMART CARD SILICON & OS LAYER (NXP P71D321)                                  |
|   - Secure Microcontroller: NXP SmartMX3 P71D321                                      |
|   - Cryptographic Accelerators: 3DES, AES, RSA, ECC, TRNG                             |
|   - Storage Budget: ~140 KB Flash NVM, 3,264 bytes Transient RAM                      |
|   - Card OS: NXP JCOP 4 (Java Card 3.0.5 Classic VM & GlobalPlatform 2.3 ISD)         |
+---------------------------------------------------------------------------------------+
```

---

## 2. Sequence Diagrams

### 2.1 Hardware Discovery & SCP02 Authentication Flow

```mermaid
sequenceDiagram
    participant Host as Python (tools/card_tool.py)
    participant WinSCard as Windows PC/SC (winscard.dll)
    participant Reader as Identiv SPR332 PINpad
    participant Card as NXP JCOP 4 P71

    Host->>WinSCard: SCardEstablishContext()
    Host->>WinSCard: SCardConnect("Identiv SPRx32", T=1)
    WinSCard->>Reader: Power On / Reset
    Reader->>Card: RST High
    Card-->>Reader: ATR (3B D5 18 FF 81 91 FE 1F C3 80 73 C8 21 10 0A)
    Reader-->>Host: ATR returned (T=1 Protocol Active)

    Host->>WinSCard: SCardControl(IOCTL_GET_FEATURE_REQUEST)
    WinSCard-->>Host: Features (VERIFY_PIN_DIRECT: 0x00313538, IFD_PIN_PROPERTIES)

    Host->>Card: SELECT Card Manager (00 A4 04 00 08 A0 00 00 01 51 00 00 00)
    Card-->>Host: SW: 9000

    Host->>Card: INITIALIZE UPDATE (80 50 00 00 08 [Host Challenge] 00)
    Card-->>Host: Key Info, Card Challenge, Card Cryptogram (SW: 9000)

    Note over Host: Derives 3DES S-ENC, S-MAC Session Keys (SCP02 Opt 55)<br/>Verifies Card Cryptogram<br/>Computes Host Cryptogram & C-MAC

    Host->>Card: EXTERNAL AUTHENTICATE (84 82 00 00 10 [Host Cryptogram] [C-MAC])
    Card-->>Host: SW: 9000 (Authenticated)
```

---

### 2.2 Class 2 PINpad Verification Flow

```mermaid
sequenceDiagram
    participant User as Developer / End User
    participant Host as Python (tools/pinpad_tool.py)
    participant Reader as Identiv SPR332 PINpad
    participant Card as Java Card Applet (OwnerPIN)

    Host->>Reader: SCardControl(FEATURE_VERIFY_PIN_DIRECT, PIN_VERIFY_STRUCTURE)
    Reader->>User: Buzzer Prompt (Two short beeps)
    
    User->>Reader: Types digits "1 2 3 4" on keypad
    Note over Reader: Emits audio click per keystroke.<br/>No plaintext sent to Host PC!
    
    User->>Reader: Presses Green "Enter"
    Reader->>Card: Transmits APDU directly: 00 20 00 80 04 31 32 33 34
    Card->>Card: OwnerPIN.check()
    Card-->>Reader: SW: 9000
    
    Reader->>User: Melodic Confirmation Chime
    Reader-->>Host: SCardControl completes (SW: 9000)
    
    Note over Host: Protected BER-TLV branches are now unlocked!
```
