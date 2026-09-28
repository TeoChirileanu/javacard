# Contract: PC/SC Part 10 PINpad Direct Control Interface

**Feature Branch**: `001-smartcard-reader-exploration`  
**Date**: 2026-09-28  
**Target Hardware**: Identiv SPR332 / SPRx32 USB Class 2 PINpad Reader  
**Driver**: Native Windows inbox CCID (`winscard.dll`)  

---

## 1. IOCTL Code Discovery Protocol

Before issuing PIN verification, the host software MUST discover the reader's Part 10 feature table using `SCardControl`:

- **Discovery IOCTL**: `IOCTL_GET_FEATURE_REQUEST` (`0x00313520` or `SCARD_CTL_CODE(3400)`).
- **Returned Feature TLVs**:
  - Tag `0x06` (`FEATURE_VERIFY_PIN_DIRECT`): `0x00313538`
  - Tag `0x07` (`FEATURE_VERIFY_PIN_START`): `0x0031353C`
  - Tag `0x08` (`FEATURE_VERIFY_PIN_FINISH`): `0x00313540`
  - Tag `0x0A` (`FEATURE_IFD_PIN_PROPERTIES`): `0x00313548`

---

## 2. Hardware Capabilities (`FEATURE_IFD_PIN_PROPERTIES`)

When querying IOCTL `0x00313548`, the Identiv SPR332 returns:
- `wLcdLayout`: `0x0000` (No LCD display, 0 lines x 0 chars).
- `bEntryValidationCondition`: `0x02` (Validation triggered by pressing the Green `Enter` key).
- `bTimeOut2`: Timeout in seconds for user keypad response (default: 30 seconds).

---

## 3. Direct PIN Verification Structure (`PIN_VERIFY_STRUCTURE`)

To initiate PIN verification on the reader keypad via `FEATURE_VERIFY_PIN_DIRECT` (`0x00313538`), the host constructs a binary C structure passed to `SCardControl`:

```c
struct PIN_VERIFY_STRUCTURE {
    BYTE  bTimerOut;                 // Timeout in seconds (e.g. 0x1E = 30s)
    BYTE  bTimerOut2;                // Key-to-key timeout in seconds (e.g. 0x0A = 10s)
    BYTE  bmFormatString;            // 0x82 (System units = bytes, Position = right justified)
    BYTE  bmPINBlockString;          // 0x08 (PIN block size = 8 bytes)
    BYTE  bmPINLengthFormat;         // 0x00 (No length byte embedded in PIN block)
    WORD  wPINMaxExtraDigit;         // 0x0804 (Max digits: 8, Min digits: 4)
    BYTE  bEntryValidationCondition; // 0x02 (Enter key pressed)
    BYTE  bNumberMessage;            // 0x00 (No LCD message)
    WORD  wLangId;                   // 0x0409 (English US)
    BYTE  bMsgIndex;                 // 0x00
    BYTE  bTeoPrologue[3];           // Reserved / T=1 prologue (0x00, 0x00, 0x00)
    DWORD ulDataLength;              // Length of APDU template in bytes (e.g. 0x00000005)
    BYTE  abData[5];                 // APDU template: 00 20 00 80 00 (VERIFY header)
};
```

---

## 4. Acoustic UX Feedback Matrix (Identiv Buzzer Signals)

Because the Identiv SPR332 has no LCD display, all user interaction is communicated acoustically:

| Event | Acoustic Signal | User Action Required |
|---|---|---|
| **PIN Prompt Start** | Two short high-pitched beeps | User looks at reader and enters 4-8 digits on keypad |
| **Keystroke Pressed** | Single short click / tone per key | Verifies key was registered (no visual echo) |
| **PIN Confirmed (`Enter`)** | Short ascending two-tone beep | Reader sends `VERIFY` APDU to card |
| **Verification Success (`9000`)** | High melodic confirmation chime | Protected data unlocked; proceed on host |
| **Verification Error (`63CX`)** | Low descending error tone | User retry allowed (tries remaining displayed in host CLI) |
| **Cancel Pressed (`Cancel`)** | Long low buzz | Verification aborted immediately |
| **Timeout Expired (30s)** | Double low buzz | Session resets to unauthenticated state |
