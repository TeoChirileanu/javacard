<!--
Sync Impact Report:
- Version change: initial template -> v1.0.0
- Ratification Date: 2026-09-28
- Principles Defined:
  - I. Real Hardware & PC/SC First
  - II. Zero-Allocation & Strict Memory Safety (NON-NEGOTIABLE)
  - III. Standards-Compliant Protocol Engineering
  - IV. Hardware Guardrails & Factory Asset Protection
  - V. Secure PINpad & Hardware Credential Isolation
- Added Sections: Security & Hardware Constraints, Development & Quality Gates, Governance
- Removed Sections: Template placeholders
- Deferred Items: None
-->

# Java Card JCOP 4 P71 & Identiv Platform Constitution

## Core Principles

### I. Real Hardware & PC/SC First
All card-side applets, host scripts, and communication protocols MUST be verified directly against the physical smart card (`NXP SmartMX3 P71D321` running `JCOP 4`) connected via the `Identiv SPR332 / SPRx32` USB reader using native Windows PC/SC (`winscard.dll`). While simulation may assist initial prototyping, physical hardware timing, real APDU response codes, and actual hardware coprocessor execution are the ultimate source of truth.

### II. Zero-Allocation & Strict Memory Safety (NON-NEGOTIABLE)
Java Card smart card microcontrollers have extreme memory limitations and physical flash wear constraints.
- Object instantiation (`new`, `newarray`) inside the runtime APDU handling method (`process()`) is strictly prohibited.
- All persistent arrays, cryptographic keys, and state objects MUST be pre-allocated during applet instantiation (`install()`).
- Transient byte arrays (`JCSystem.makeTransientByteArray`) MUST be used for scratchpads, cryptographic intermediates, and session buffers.
- Total transient memory allocation across any active applet session MUST strictly remain within the **3,264 bytes** hardware RAM boundary.

### III. Standards-Compliant Protocol Engineering
All APDU exchanges and security domains MUST strictly adhere to international smart card standards:
- Card-level APDU framing and status words MUST conform to ISO/IEC 7816-3 (T=1 transmission protocol) and ISO/IEC 7816-4.
- Administrative card management MUST adhere to GlobalPlatform Card Specification v2.3, using Secure Channel Protocol SCP02 (Option 55) or SCP03.
- Applet source code MUST target the Java Card 3.0.5 Classic Edition API specification.

### IV. Hardware Guardrails & Factory Asset Protection
The target NXP P71 card hosts factory-installed modules and specific cryptographic life-cycles:
- The factory NXP MIFARE Plus EV1 4k emulation module (`AID: D276000085304A434F900101` / package `D276000085304A434F9001`) MUST NEVER be deleted or uninstalled.
- GlobalPlatform master keys MUST NOT be modified or replaced without an explicit, verified key-rotation ceremony and persistent storage of backup keys to avoid bricking the card.
- Life-cycle transitions (e.g. `CARD_LOCKED`, `TERMINATED`) MUST be protected against accidental execution.

### V. Secure PINpad & Hardware Credential Isolation
The project leverages the Identiv SPR332 Class 2 PINpad reader to maintain maximum credential isolation:
- PIN entry MUST prioritize on-reader verification via PC/SC Part 10 (`FEATURE_VERIFY_PIN_DIRECT`, IOCTL `0x00313538`), ensuring PIN keystrokes travel directly to the card chip without traversing host PC memory.
- User feedback MUST respect the acoustic buzzer signaling semantics, recognizing that the reader has no LCD display (`wLcdLayout = 0`) and relies on audible cues for keystrokes, session start, and completion.

## Security & Hardware Constraints

1. **Storage Budget**:
   - Persistent NVM (Flash): Maximum **~140 KB** available for executable load files (CAP packages) and persistent data.
   - Transient RAM: Maximum **3.2 KB** shared pool.
2. **Cryptographic Acceleration**:
   - Asymmetric operations MUST utilize hardware coprocessors: RSA (up to 2048/4096-bit CRT), ECC (NIST P-256/384/521, Brainpool).
   - Symmetric operations MUST utilize AES (128/192/256-bit) or 3DES coprocessors.
   - On-chip True Random Number Generator (TRNG) MUST be used for all nonce and challenge generation.
3. **Transaction Atomicity**:
   - Complex multi-field persistent updates MUST be wrapped in `JCSystem.beginTransaction()` and `JCSystem.commitTransaction()` to guarantee rollback safety in case of unexpected card tear or power loss.

## Development & Quality Gates

1. **Host-Side Tooling**:
   - Python utilities interacting with the card MUST rely on direct Windows PC/SC (`winscard.dll`) or standard CCID without requiring proprietary third-party drivers.
   - All tools MUST gracefully handle smart card removal, card resets, and APDU error status words (`SW != 9000`).
2. **Build and Deployment Verification**:
   - Every compiled CAP package MUST be verified for bytecode version compatibility (Java Card 3.0.5 target level).
   - Applet deployment scripts MUST verify successful card authentication (SCP02) and execute a status query (`GET STATUS`) before and after installation.
3. **Documentation Integrity**:
   - Every newly implemented APDU command MUST document its CLA, INS, P1, P2, Lc, Data payload, Le, and potential Status Words (SW1/SW2).

## Governance

- This Constitution defines the non-negotiable operational principles for Java Card development in this repository.
- Any modification to these principles requires updating this document, bumping the version number, and recording an amendment entry.
- Versioning Policy:
  - **MAJOR (x.0.0)**: Breaking changes to hardware architecture, memory budget invariants, or security policies.
  - **MINOR (1.x.0)**: Addition of new cryptographic algorithms, platform profiles, or new reader features.
  - **PATCH (1.0.x)**: Clarifications, documentation corrections, or minor adjustments.

**Version**: 1.0.0 | **Ratified**: 2026-09-28 | **Last Amended**: 2026-09-28
