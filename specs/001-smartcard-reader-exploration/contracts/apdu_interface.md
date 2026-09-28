# Contract: ISO 7816-4 APDU Command & Response Interface

**Feature Branch**: `001-smartcard-reader-exploration`  
**Date**: 2026-09-28  
**Target Applet AID**: `F0 73 70 65 63 01 01` (`F0 speck0101`)  

---

## 1. APDU Summary Table

| Command | CLA | INS | P1 | P2 | Lc | Data In | Le | SW1/SW2 |
|---|---|---|---|---|---|---|---|---|
| **SELECT** | `00` | `A4` | `04` | `00` | AID Len | Application AID | `00` | `9000`, `6A82` |
| **VERIFY PIN** | `00` | `20` | `00` | `80` | PIN Len | Plaintext PIN (direct via PINpad) | Absent | `9000`, `63CX`, `6983` |
| **GET DATA** | `00` | `CA` | Tag MSB | Tag LSB | Path Len / 0 | Tag Path (Optional) | `00` | `9000`, `6A88`, `6982` |
| **PUT DATA** | `00` | `DA` | Tag MSB | Tag LSB | Payload Len | BER-TLV Payload | Absent | `9000`, `6700`, `6A80`, `6A84`, `6982` |
| **DELETE DATA**| `80` | `E4` | Tag MSB | Tag LSB | 0 | None | Absent | `9000`, `6A88`, `6982` |
| **RESET PIN** | `80` | `24` | `00` | `80` | New PIN Len | New PIN bytes (Protected) | Absent | `9000`, `6982` |

---

## 2. Command Details

### 2.1 SELECT Applet
- **CLA**: `0x00`
- **INS**: `0xA4`
- **P1**: `0x04` (Select by direct AID)
- **P2**: `0x00` (First or only occurrence)
- **Data Field**: `F0 73 70 65 63 01 01` (7 bytes)
- **Response**:
  - `90 00`: Applet selected successfully. Storage version and available memory returned in payload:
    `54 4C [Ver: 01] [Free NVM: 2 bytes] [PinState: 1 byte]`
  - `6A 82`: File or application not found.

---

### 2.2 VERIFY PIN (Direct via Identiv SPR332 PINpad)
- **CLA**: `0x00`
- **INS**: `0x20`
- **P1**: `0x00`
- **P2**: `0x80` (PIN reference)
- **Data Field**: 4-8 numeric ASCII digits (`0x30`..`0x39`). Intercepted and injected by reader CCID firmware via `FEATURE_VERIFY_PIN_DIRECT`.
- **Response**:
  - `90 00`: Verification successful. PIN state unlocked for session. Reader emits confirmation melody.
  - `63 CX`: Verification failed, `X` attempts remaining. Reader emits error buzz.
  - `69 83`: Authentication method blocked (0 tries left).
  - `69 84`: Referenced data invalidated.

---

### 2.3 GET DATA (Read Node / Branch)
- **CLA**: `0x00`
- **INS**: `0xCA`
- **P1 / P2**: Target BER-TLV Tag (e.g. `P1=0xDF, P2=0x01` for public; `P1=0xEF, P2=0x01` for protected).
  - If `P1=0x00, P2=0x00`, queries root container (exports full hierarchical tree).
- **Data Field**: Empty, or sequence of parent tags defining descendant path.
- **Le**: `0x00` (Maximum 256 bytes returned).
- **Response**:
  - `90 00` + `[Full BER-TLV bytes of requested tag/branch]`.
  - `6A 88`: Referenced data object not found.
  - `69 82`: Security status not satisfied (PIN verification required).

---

### 2.4 PUT DATA (Write / Update Node or Branch)
- **CLA**: `0x00`
- **INS**: `0xDA`
- **P1 / P2**: Target BER-TLV Tag.
- **Data Field**: Encoded BER-TLV object (`Tag + Length + Value`).
- **Response**:
  - `90 00`: Node inserted/updated atomically in persistent NVM.
  - `67 00`: Wrong length in Lc or malformed length field.
  - `6A 80`: Incorrect parameters in data field (exceeds max nesting depth 8 or malformed TLV).
  - `6A 84`: Not enough memory space in NVM buffer.
  - `69 82`: Security status not satisfied (attempted write to protected tag without prior PIN verification).
