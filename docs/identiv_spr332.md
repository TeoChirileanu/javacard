# Identiv SPR332 (SPRx32) USB Class 2 PINpad Reader Technical Reference

**Device**: SCM Microsystems Inc. / Identiv SPR332 (SPRx32) USB Smart Card Reader  
**Classification**: Class 2 Secure PINpad Peripheral (ISO 7816 / PC/SC Part 10)  
**Host Interface**: USB 2.0 Full-Speed CCID  

---

## 1. Hardware Architecture & Capabilities

The Identiv SPR332 is a secure desktop smart card reader engineered for credential protection. Unlike standard Class 1 smart card dongles (which require PIN entry on the host computer keyboard), the SPR332 provides an on-reader numeric keypad.

```
+--------------------------------------------------------------------+
|                   Identiv SPR332 Physical Layout                   |
|                                                                    |
|   +------------------------------------------------------------+   |
|   |                 Smart Card Insertion Slot                  |   |
|   |       (ISO 7816-3 Contact Pins: VCC, RST, CLK, GND, I/O)   |   |
|   +------------------------------------------------------------+   |
|                                                                    |
|      [ 1 ]       [ 2 ]       [ 3 ]       [ Correction/Yellow ]     |
|      [ 4 ]       [ 5 ]       [ 6 ]       [ Cancel / Red ]          |
|      [ 7 ]       [ 8 ]       [ 9 ]                                 |
|                  [ 0 ]                   [ Enter / Green ]         |
|                                                                    |
|             Integrated Piezoelectric Acoustic Buzzer               |
+--------------------------------------------------------------------+
```

### Key Hardware Characteristics:
- **Display**: **None** (`wLcdLayout = 0x0000`).
- **Acoustic Feedback**: Integrated piezoelectric buzzer provides real-time auditory status cues to compensate for the absence of an LCD.
- **Keypad**: 16 keys (0–9 digits, Green `Enter`, Red `Cancel`, Yellow `Correction/Clear`).
- **Smart Card Protocols**: ISO 7816-3 T=0 and T=1 asynchronous protocols with contact clock frequencies up to 5 MHz.

---

## 2. PC/SC Part 10 IOCTL Feature Table

When queried using `IOCTL_GET_FEATURE_REQUEST` (`0x00313520`), the Identiv SPR332 driver enumerates its supported control codes:

| Feature Tag | Feature Name | IOCTL Code (Hex) | Purpose |
|---|---|---|---|
| `0x01` | `FEATURE_VERIFY_PIN_START` | `0x0031353C` | Asynchronous PIN entry initiation |
| `0x02` | `FEATURE_VERIFY_PIN_FINISH` | `0x00313540` | Asynchronous PIN entry finalization |
| `0x05` | `FEATURE_GET_KEY_PRESSED` | `0x00313544` | Keypad status interrogation |
| `0x06` | `FEATURE_VERIFY_PIN_DIRECT` | `0x00313538` | **Synchronous direct PIN verification** |
| `0x07` | `FEATURE_MODIFY_PIN_DIRECT` | `0x0031354C` | Direct PIN change / unblock |
| `0x0A` | `FEATURE_IFD_PIN_PROPERTIES` | `0x00313548` | Interrogate reader PINpad capabilities |

### Interrogating PIN Properties (`FEATURE_IFD_PIN_PROPERTIES`)
Calling `SCardControl` with code `0x00313548` returns:
- `wLcdLayout = 0x0000`: Explicitly confirms that the device has 0 display rows and 0 display columns.
- `bEntryValidationCondition = 0x07`: Enter key validation enabled.
- `bTimeOut2 = 0x1E`: 30-second default user input timeout.

---

## 3. Acoustic UX Feedback Matrix (Identiv Buzzer Signals)

Because there is no visual screen, the buzzer is the primary user interface during PIN verification:

| Event | Acoustic Signal | User Action Required |
|---|---|---|
| **PIN Prompt Start** | Two short high-pitched beeps | User enters PIN digits on numeric keypad |
| **Keystroke Pressed** | Single short click / tone per key | Confirms digit registration without visual display |
| **PIN Confirmed (`Enter`)** | Short ascending two-tone beep | Reader sends `VERIFY` APDU to card |
| **Verification Success (`9000`)** | High melodic confirmation chime | Access granted; proceed on host |
| **Verification Error (`63CX`)** | Low descending error tone | User retry allowed (tries remaining displayed in host CLI) |
| **Cancel Pressed (`Cancel`)** | Long low buzz | Verification aborted immediately |
| **Timeout Expired (30s)** | Double low buzz | Session resets to unauthenticated state |

---

## 4. Security Isolation: Class 2 vs Class 1

```
CLASS 1 (Insecure Host-Side PIN Entry):
Keyboard -> OS Kernel -> Windows Input Stack -> Host Application Process -> USB CCID -> Card
                ^                                        ^
                |--- Vulnerable to Keyloggers            |--- Vulnerable to Memory Dumps

CLASS 2 (Identiv SPR332 Hardware Isolation):
Host Application -> Sends FEATURE_VERIFY_PIN_DIRECT (APDU template: 00 20 00 80 00)
                              |
                              v
Keypad Entry ---> Identiv Hardware Buffer ---> ISO 7816-3 Contact Pins ---> Smart Card Chip
(Isolated)                                                                 (OwnerPIN)
```

No PIN bytes ever enter the host PC's operating system, kernel USB driver, or application memory space.
