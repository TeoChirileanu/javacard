# Feature Specification: Smart Card & Reader Architecture Exploration and Data I/O

**Feature Branch**: `001-smartcard-reader-exploration`

**Created**: 2026-09-28

**Status**: Draft

**Input**: User description: "vreau sa exploram in detaliu acest cititor cu smartcard si sa documentam tot ce putem face pe el, ma intereseaza sa inteleg arhitectura si cum se poate scrie/citit de pe card"

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Full Architectural Discovery & Diagnostics (Priority: P1)

As a developer, I want to inspect, map, and document the end-to-end architecture connecting the host operating system, USB CCID driver, Identiv SPR332 PINpad hardware, and the NXP JCOP 4 P71 smart card microcontroller, so that I clearly understand every physical and logical layer of communication.

**Why this priority**: Without a precise understanding of the stack (PC/SC Part 10 IOCTLs, ISO 7816-3 T=1 transmission framing, GlobalPlatform 2.3 security domains, and Java Card 3.0.5 VM), read/write operations and applet deployment cannot be reliably executed or debugged.

**Independent Test**: Can be verified independently by running hardware diagnostic commands that probe PC/SC subsystem properties, verify ISO 7816-3 ATR parsing, decode CPLC chip production data, and report GlobalPlatform life-cycle states.

**Acceptance Scenarios**:
1. **Given** the Identiv SPR332 reader connected via USB, **When** queried via Windows PC/SC IOCTLs (`IOCTL_GET_FEATURE_REQUEST` and `FEATURE_IFD_PIN_PROPERTIES`), **Then** report reader hardware capabilities: Class 2 PINpad, acoustic buzzer feedback, and absence of LCD (`wLcdLayout = 0`).
2. **Given** the NXP JCOP 4 card inserted, **When** powering on the card interface, **Then** receive and decode the 15-byte ATR (`3B D5 18 FF 81 91 FE 1F C3 80 73 C8 21 10 0A`), establish T=1 protocol communication, and extract the CPLC identifier demonstrating the P71D321 silicon origin.
3. **Given** the Card Manager selected, **When** querying Card Data (tag `0x66`), **Then** parse and document all registered OIDs (GlobalPlatform 2.3, SCP02 Option 55, Java Card 3.0.5 Classic).

---

### User Story 2 - Card Data Reading & File Structure Inspection (Priority: P1)

As a developer, I want to execute standardized read operations on the smart card across both administrative security domains and application payloads, so that I can inspect card metadata, security status, and stored binary objects.

**Why this priority**: Reading data is fundamental to understanding what exists on the card (factory modules, card manager configuration, remaining memory) and verifying card contents prior to any write operations.

**Independent Test**: Can be tested independently by querying standard GlobalPlatform tags (`GET DATA 9F7F` for CPLC, `GET DATA 0066` for Card Data, `GET DATA 00E0` for Key Info) and reading binary data from selectable applets.

**Acceptance Scenarios**:
1. **Given** an unauthenticated card session, **When** sending `GET DATA` APDUs for public tags (`9F7F`, `0066`, `00E0`), **Then** retrieve the respective byte payloads with status word `9000` and parse them into structured JSON or formatted console output.
2. **Given** an authenticated GlobalPlatform SCP02 session, **When** sending `GET STATUS` APDUs for Issuer Security Domain, Executable Load Files, and Application Instances, **Then** list all installed AIDs, their life-cycle states, and privilege bitmaps.
3. **Given** a data-storage applet selected by its AID, **When** sending an ISO 7816-4 `READ BINARY` command with offset and length, **Then** receive the exact stored bytes and confirm data integrity.

---

### User Story 3 - Card Data Writing & Non-Volatile Persistence (Priority: P2)

As a developer, I want to execute write operations that persist arbitrary structured or binary data to the smart card's non-volatile flash memory (NVM), so that I understand how data is securely stored, updated, and protected on-card.

**Why this priority**: Enabling persistent data storage on the card unlocks practical smart card use-cases (storing user credentials, public keys, secure counters, or identity records).

**Independent Test**: Can be tested independently by writing a test payload to the card (via GlobalPlatform `STORE DATA` or an ISO 7816-4 `UPDATE BINARY` command to a data applet), disconnecting/resetting the card session, and reading back the exact bytes.

**Acceptance Scenarios**:
1. **Given** a selected data-storage applet, **When** sending an `UPDATE BINARY` or `WRITE DATA` APDU containing a payload up to 255 bytes, **Then** the card writes the data to persistent NVM using an atomic transaction and returns `SW: 9000`.
2. **Given** data previously written to the card, **When** the card is power-cycled (e.g. reader warm reset or disconnect) and read back, **Then** the retrieved data matches the written data byte-for-byte.
3. **Given** a write operation attempted with invalid parameters (e.g. exceeding available buffer length), **Then** the card rejects the command with an appropriate error status word (e.g. `6700` Wrong Length or `6A84` Not Enough Memory) without corrupting existing data.

---

### User Story 4 - Class 2 PINpad Hardware Verification & Acoustic Feedback (Priority: P3)

As a developer, I want to interact with the Identiv SPR332 integrated keypad via PC/SC Part 10 PIN verification, so that I understand how on-reader PIN verification protects user credentials from host-side keyloggers and how acoustic buzzer signals provide sensory feedback.

**Why this priority**: The Identiv reader is specifically a Class 2 device; demonstrating hardware-isolated PIN entry showcases the primary security benefit of this specific reader over standard Class 1 dongles.

**Independent Test**: Can be tested by invoking `FEATURE_VERIFY_PIN_DIRECT` (IOCTL `0x00313538`) from the host script, allowing the user to enter a PIN directly on the reader's numeric keypad and observing acoustic buzzer feedback.

**Acceptance Scenarios**:
1. **Given** an applet requiring PIN authentication selected on the card, **When** the host application sends the `FEATURE_VERIFY_PIN_DIRECT` control code to the reader, **Then** the reader emits an audible prompt beep, captures user keystrokes with keypress audio feedback, and directly transmits the `VERIFY` APDU to the card without sending keystrokes to the host PC.
2. **Given** an active PINpad entry prompt, **When** the user presses the Green `Enter` key, **Then** the reader emits a confirmation beep, sends the APDU, and returns the card's verification result (`9000` on success, `63CX` on wrong PIN).
3. **Given** the user presses the Red `Cancel` key or the timeout elapses, **Then** the reader aborts the prompt with an error tone and returns an abort status to the host.

---

### Edge Cases

- **Card Tear / Abrupt Disconnection**: What happens if the card is unplugged mid-write? All multi-byte writes inside Java Card applets must be protected by `JCSystem.beginTransaction()` and `JCSystem.commitTransaction()` to prevent half-written state.
- **RAM Overflow**: What happens if an APDU allocates temporary objects? Memory exhaustion occurs because total RAM is capped at 3,264 bytes. In accordance with the project Constitution, zero allocations are permitted in `process()`.
- **Factory Emulation Interference**: What happens if an applet uses an AID conflicting with `D276000085304A434F900101`? The card manager will reject instantiation with `6A80`. Custom applets must use unique private AIDs (e.g. standard proprietary RID `F0 00 ...`).
- **GlobalPlatform Authentication Lockout**: What happens if invalid SCP02 keys are sent in `EXTERNAL AUTHENTICATE`? The card increments the failed authentication counter. Default keys (`40..4F`) must be maintained until an explicit key-rotation procedure is specified.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an interactive host-side CLI tool (`tools/card_tool.py`) capable of enumerating PC/SC readers, connecting via ISO 7816-3 T=1, and extracting raw ATRs.
- **FR-002**: System MUST decode and document the NXP JCOP 4 P71 CPLC data, identifying the silicon fabricator, chip model (`P71D321`), and OS version level.
- **FR-003**: System MUST execute GlobalPlatform 2.3 SCP02 (Option 55) mutual authentication against the Card Manager (`A000000151000000`) using 3DES session keys derived from default master keys.
- **FR-004**: System MUST query and display the life-cycle status and privileges of all installed Executable Load Files, Packages, and Application Instances via `GET STATUS`.
- **FR-005**: System MUST document the end-to-end architectural layers (Host Application → WinSCard API → USB CCID → Identiv SPR332 PINpad Hardware → Smart Card Silicon → Java Card VM → Applet).
- **FR-006**: System MUST demonstrate reading data from the card via standardized ISO 7816-4 APDU commands (`SELECT`, `GET DATA`, `READ BINARY`).
- **FR-007**: System MUST specify and demonstrate writing persistent data to the card (via `UPDATE BINARY` / `STORE DATA`) with atomic commit rollback guarantees.
- **FR-008**: System MUST demonstrate PC/SC Part 10 PIN verification (`FEATURE_VERIFY_PIN_DIRECT`) utilizing the Identiv SPR332 numeric keypad without exposing PIN bytes to the host operating system.
- **FR-009**: System MUST document all Identiv SPR332 buzzer sound patterns (Keypress, Insertion, Prompt, Confirmation, Error) to provide acoustic UX guidance in the absence of an LCD.
- **FR-010**: System MUST provide a reference Java Card Data Storage Applet source code compatible with Java Card 3.0.5 Classic Edition that can be compiled to `.cap` and deployed to the card.

---

### Key Entities

- **Smart Card Microcontroller (NXP P71D321)**: High-security secure element featuring ~140 KB NVM, 3.2 KB RAM, cryptographic coprocessors, running JCOP 4 OS.
- **Smart Card Reader (Identiv SPR332)**: USB CCID Class 2 secure PINpad peripheral with integrated numeric keypad and audio buzzer.
- **APDU (Application Protocol Data Unit)**: The standardized message unit exchanged between host and card (`CLA`, `INS`, `P1`, `P2`, `Lc`, `Data`, `Le`, `SW1/SW2`).
- **Data Storage Record**: Persistent byte array held in card flash memory, addressable by file identifier or record index, protected against unauthenticated overwrite.
- **PIN Credential**: 4-to-8 digit secret verified on-card via `OwnerPIN` API, entered securely through the reader PINpad.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of architectural layers (Hardware, Driver, Protocol, Card OS, Applet) are fully documented with corresponding APDU traces and message sequence diagrams.
- **SC-002**: Read and write operations to the card complete with round-trip latency under 100ms per APDU for standard payloads up to 255 bytes.
- **SC-003**: Written data survives complete card power removal and verification reads match original payloads with zero corruption across 10 consecutive test cycles.
- **SC-004**: The Identiv SPR332 PINpad successfully captures and verifies PINs on-card without any PIN plaintext appearing in host process memory or USB sniffer logs.

---

## Assumptions

- The Identiv SPR332 reader remains plugged into a USB port on the developer workstation and has the Windows inbox CCID driver active.
- The smart card in the reader is the verified NXP JCOP 4 P71 card with default GlobalPlatform master keys (`404142434445464748494A4B4C4D4E4F`).
- Python 3.11+ is available on the host with `winscard.dll` (standard on Windows 10/11) and `cryptography` installed.
- Compilation of `.cap` files requires a Java Card 3.0.5 compatible converter/SDK (bundled API specifications are provided in `kit.zip`).
