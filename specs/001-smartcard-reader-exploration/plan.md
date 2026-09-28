# Implementation Plan: Smart Card & Reader Architecture Exploration and Data I/O

**Branch**: `001-smartcard-reader-exploration` | **Date**: 2026-09-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-smartcard-reader-exploration/spec.md`

---

## Summary

This feature delivers an end-to-end exploration and technical implementation for the Identiv SPR332 Class 2 PINpad reader and the NXP JCOP 4 P71 smart card. It encompasses:
1. Complete architectural documentation and live hardware diagnostics across all 5 physical and logical layers (Host OS → WinSCard API → USB CCID → Identiv PINpad → NXP P71D321 Silicon / Java Card VM).
2. A reference Java Card 3.0.5 Classic Edition applet (`BerStorageApplet`) implementing a zero-allocation, arbitrary-depth hierarchical key-value store using ISO/IEC 7816-4 BER-TLV framing in persistent NVM, protected by hybrid tag-level access control.
3. Host-side Python CLI tooling (`tools/card_tool.py`, `tools/pinpad_tool.py`, `tools/tlv_tool.py`) interfacing via native `winscard.dll` to execute GlobalPlatform SCP02 authentication, bidirectional JSON/YAML <-> BER-TLV serialization, and PC/SC Part 10 direct hardware PIN verification (`FEATURE_VERIFY_PIN_DIRECT`).

---

## Technical Context

**Language/Version**: Java Card 3.0.5 Classic Edition (on-card), Python 3.11+ (host)

**Primary Dependencies**:
- On-card: `javacard.framework.*`, `javacard.security.*` (Java Card 3.0.5 API specifications).
- Host: Windows `winscard.dll` (via standard `ctypes`), Python `cryptography` (for GP SCP02 3DES session key derivation), `pyyaml` (for YAML serialization).

**Storage**:
- Smart card Flash NVM (~140 KB capacity available; applet pre-allocates a contiguous storage buffer up to 32 KB).
- Zero-allocation heap policy in runtime `process()`.
- Smart card transient RAM strictly bounded within the chip's 3,264 bytes total budget (scratchpad buffer 256 bytes `CLEAR_ON_DESELECT`).

**Testing**:
- Host-side Python `unittest` suite (`tests/test_tlv_serialization.py`) for offline codec round-trip validation.
- Live hardware integration tests (`tests/test_hardware_*.py`) running live APDU commands against the physical card and reader.

**Target Platform**:
- Smart card: NXP SmartMX3 P71D321 secure microcontroller running JCOP 4 OS.
- Reader: Identiv SPR332 / SPRx32 USB Class 2 PINpad reader (No LCD, integrated numeric keypad, acoustic buzzer).
- Host: Windows 10/11 x64 PC/SC CCID subsystem.

**Project Type**: Embedded secure element applet (CAP package) + Host PC/SC CLI / SDK tool suite.

**Performance Goals**:
- APDU round-trip latency < 100ms for standard payloads up to 255 bytes.
- Direct PIN verification cycle < 2s (including acoustic buzzer feedback and card response).

**Constraints**:
- RAM usage <= 3,264 bytes; zero `new` allocations inside `process()`.
- Maximum BER-TLV nesting depth bounded to 8 levels to protect the Java Card VM call stack.
- Factory NXP MIFARE Plus EV1 4k module (`AID: D276000085304A434F900101`) must never be modified or uninstalled.
- PIN entry must never expose plaintext digits to host PC memory; strictly routed via `FEATURE_VERIFY_PIN_DIRECT` (IOCTL `0x00313538`).

**Scale/Scope**:
- 1 Java Card reference applet package (`org.speckit.javacard.berstorage`).
- 3 Host Python CLI tools (`card_tool.py`, `pinpad_tool.py`, `tlv_tool.py`).
- 4 Integration test suites covering Discovery, Read, Write, and PINpad flows.

---

## Constitution Check

*GATE: Evaluated against `.specify/memory/constitution.md` (v1.0.0).*

| Principle | Requirement | Plan Status |
|---|---|---|
| **I. Real Hardware First** | Physical SPR332 + NXP P71D321 card verified via native `winscard.dll` | **PASS** - Diagnostics confirm live communication over T=1 protocol. |
| **II. Zero-Allocation (NON-NEGOTIABLE)** | No dynamic allocations in `process()`; RAM <= 3,264 bytes | **PASS** - Buffers pre-allocated in `install()`; 256-byte transient scratchpad; iterative traversal. |
| **III. Standards Compliance** | ISO 7816-3/4, GlobalPlatform 2.3, Java Card 3.0.5, PC/SC Part 10 | **PASS** - Standard BER-TLV encoding, standard APDUs, SCP02 Opt 55, Part 10 IOCTL `0x00313538`. |
| **IV. Factory Asset Protection** | Protect MIFARE AID `D276...`; custom RID `F0...` | **PASS** - Dedicated proprietary AID `F0 73 70 65 63 01 01`; factory module strictly preserved. |
| **V. Secure PINpad Isolation** | Direct reader PIN entry; acoustic feedback UX mapped | **PASS** - PINpad IOCTL `0x00313538` bypasses host memory; acoustic signals documented. |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-smartcard-reader-exploration/
├── plan.md              # Implementation plan (this document)
├── research.md          # Technical research & architectural decisions
├── data-model.md        # BER-TLV storage layout & state machine
├── quickstart.md        # Step-by-step validation guide
├── contracts/
│   ├── apdu_interface.md # ISO 7816-4 APDU command/response definitions
│   └── pinpad_ioctl.md   # PC/SC Part 10 PINpad direct control definitions
└── checklists/
    └── requirements.md  # Specification quality checklist
```

### Source Code (repository root)

```text
applet/
└── src/
    └── org/
        └── speckit/
            └── javacard/
                └── berstorage/
                    ├── BerStorageApplet.java   # Applet lifecycle, process() dispatcher, OwnerPIN
                    ├── BerTlvTree.java         # Zero-allocation BER-TLV search, insert, and update
                    └── AccessController.java   # Tag-level ACL evaluator (public vs PIN-gated)

tools/
├── card_tool.py             # PC/SC connection, ATR decoding, CPLC & GP Card Manager diagnostics
├── pinpad_tool.py           # PC/SC Part 10 PINpad direct verification runner & buzzer UX monitor
└── tlv_tool.py              # BER-TLV <-> JSON/YAML serializer, APDU builder, and card client

tests/
├── test_tlv_serialization.py # Unit tests for JSON/YAML to BER-TLV round-trip fidelity
├── test_hardware_read.py     # Live hardware read APDU validation
├── test_hardware_write.py    # Live hardware persistent write & atomic rollback validation
└── test_hardware_pinpad.py   # Live Class 2 PINpad verification & ACL enforcement validation

docs/
├── identiv_spr332.md        # Identiv SPR332 hardware specifications, IOCTLs, and acoustic codes
└── architecture.md          # End-to-end layered architecture guide and trace sequence diagrams
```

**Structure Decision**: A modular embedded architecture separating the on-card Java Card applet (`applet/`), host-side Python driver and PC/SC utilities (`tools/`), automated verification test suites (`tests/`), and comprehensive system documentation (`docs/`).

---

## Complexity Tracking

> *No constitutional violations or unjustified complexity. All principles satisfied.*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| *None* | N/A | N/A |
