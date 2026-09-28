# Technical Research: Smart Card & Reader Architecture Exploration and Data I/O

**Feature Branch**: `001-smartcard-reader-exploration`  
**Date**: 2026-09-28  
**Status**: Completed  

---

## 1. Technical Decisions & Tradeoffs

### Decision 1: Hierarchical Data Architecture (ISO/IEC 7816-4 BER-TLV vs CBOR vs Flat Records)

- **Decision**: Implement ISO/IEC 7816-4 ASN.1 Basic Encoding Rules Tag-Length-Value (BER-TLV) with recursive sub-tag traversal bounded to 8 nesting levels.
- **Rationale**:
  - BER-TLV is the universal native standard in smart card environments (EMV, eID, PIV, GlobalPlatform).
  - Bit 6 of the tag byte natively distinguishes Primitive (scalar value) from Constructed (nested dictionary/object) nodes (`0` = primitive, `1` = constructed).
  - Can be mapped 1:1 to JSON/YAML dictionaries (`constructed TLV`) and scalar values (numbers, strings, byte buffers).
  - Enables zero-allocation parsing in Java Card: traversal is performed by inspecting byte offsets directly in the APDU buffer without instantiating parser objects.
- **Alternatives Considered**:
  - *CBOR (RFC 8949)*: Highly expressive, but standard smart card tooling and ISO 7816-4 APDUs natively operate on BER-TLV tag addressing (`GET DATA` / `PUT DATA`). CBOR would require substantial overhead for custom framing.
  - *Flat Linear Records (ISO 7816-4 EF standard)*: Simple byte arrays with record numbers, but fails the user's explicit requirement for arbitrary nesting depth (hierarchical key-value tree like JSON/YAML).

---

### Decision 2: PC/SC Part 10 Class 2 PINpad Verification (`FEATURE_VERIFY_PIN_DIRECT`)

- **Decision**: Communicate with the Identiv SPR332 PINpad using native `SCardControl` with IOCTL `0x00313538` (`FEATURE_VERIFY_PIN_DIRECT`).
- **Rationale**:
  - The Identiv SPR332 is a Class 2 reader featuring an integrated numeric keypad and buzzer, but no LCD display (`wLcdLayout = 0`).
  - `FEATURE_VERIFY_PIN_DIRECT` instructs the reader firmware to intercept keypad entry directly into the CCID buffer, construct an ISO 7816-4 `VERIFY` APDU (`00 20 00 80 [Lc] [PIN]`), and transmit it directly to the card over the contact pins.
  - Plaintext PIN digits never traverse the USB bus into Windows kernel memory or user-space host applications, eliminating host-side keystroke logging and memory dump attacks.
  - Reader buzzer provides acoustic user feedback for prompt (double beep), keystrokes (click/tone), confirmation (melody), and error/timeout (low buzzer tone).
- **Alternatives Considered**:
  - *Class 1 Host-side PIN prompt (stdin)*: Plaintext PIN entered on PC keyboard and sent via standard `VERIFY` APDU. Rejected because it bypasses the physical hardware security of the Identiv Class 2 PINpad and violates Constitution Principle V.
  - *`FEATURE_VERIFY_PIN_START` / `FEATURE_VERIFY_PIN_FINISH` (asynchronous)*: More complex state machine with no tangible benefit for single-threaded CLI tools over direct synchronous control.

---

### Decision 3: Zero-Allocation Storage Engine & Memory Boundary (< 3.2 KB RAM)

- **Decision**: Pre-allocate a contiguous persistent NVM byte array (`byte[] storageBuffer`) during applet `install()`, managed via a compact compacting-tree index or linear TLV offset table, and use transient RAM scratchpads (`JCSystem.makeTransientByteArray(256, JCSystem.CLEAR_ON_DESELECT)`) for APDU parsing and path evaluation.
- **Rationale**:
  - The NXP SmartMX3 P71D321 chip budget verified in assessment is **3,264 bytes transient RAM** and **~140 KB persistent NVM**.
  - Java Card garbage collection on smart cards is either absent or introduces non-deterministic wear/latency. Calling `new` in APDU `process()` is forbidden by Constitution Principle II.
  - Traversal is iterative or bounded recursive (maximum stack frame depth 8) to avoid Java Card VM call stack overflow.
  - Atomic transactions (`JCSystem.beginTransaction()` / `commitTransaction()`) wrap persistent updates to guarantee rollback safety against card tears.
- **Alternatives Considered**:
  - *Object-oriented linked nodes (`new TlvNode()`)*: Each child node an object reference. Rejected because dynamic allocation in `process()` causes rapid memory fragmentation and leaks in NVM.

---

### Decision 4: GlobalPlatform AID Allocation & Factory Module Safety

- **Decision**: Use proprietary AID RID `F0 73 70 65 63` (`F0 speck`) with Application AID `F0 73 70 65 63 01 01`.
- **Rationale**:
  - Prevents collision with the factory NXP MIFARE Plus EV1 4k module (`AID: D276000085304A434F900101`, package `D276000085304A434F9001`).
  - Respects Constitution Principle IV (Hardware Guardrails & Factory Asset Protection).
- **Alternatives Considered**:
  - *Standard NIST PIV or OpenPGP AIDs*: Could collide with existing packages or cause unintended OS-level driver claiming (e.g. Windows Smart Card Minidriver).

---

### Decision 5: Host-Side Tooling via Native Windows WinSCard API

- **Decision**: Implement all host interaction in Python using `ctypes` calling `winscard.dll` natively.
- **Rationale**:
  - No external driver dependencies or compiled C-extensions required (e.g., `pyscard` wheel issues on Windows).
  - Direct access to `SCardControl`, `SCardTransmit`, `SCardConnect`, `SCardStatus`.
  - Windows inbox CCID driver supports Identiv SPR332 natively with full PC/SC Part 10 feature reporting.
- **Alternatives Considered**:
  - *Java PC/SC (`javax.smartcardio`)*: Requires JDK runtime on the host machine. Python provides faster scripting, easier JSON/YAML integration, and zero external runtime dependencies beyond standard Python.

---

## 2. Best Practices & Design Patterns

1. **APDU Command Structure**:
   - `SELECT`: `00 A4 04 00 [AID_LEN] [AID] 00`
   - `GET DATA (by tag/path)`: `00 CA [Tag MSB] [Tag LSB] [Path_Len/Data] 00`
   - `PUT DATA (insert/update)`: `00 DA [Tag MSB] [Tag LSB] [Lc] [TLV Data]`
   - `VERIFY PIN`: `00 20 00 80 [Lc] [PIN]`
2. **Access Control List (ACL) Representation**:
   - Tag namespace partition:
     - Tags in range `0xDF00 - 0xDFFF` (Private / Public Unauthenticated).
     - Tags in range `0xEF00 - 0xEFFF` (Constructed Protected - requires PIN validation).
     - Alternatively, bit 7 of tag descriptor or explicit node attribute byte.
3. **Transaction Safety**:
   - Updates must check `storageBuffer` available capacity before modifying any bytes.
   - Updates must execute inside `JCSystem.beginTransaction()` and terminate with `commitTransaction()`. If any parsing or memory limit error occurs, `abortTransaction()` is called.
