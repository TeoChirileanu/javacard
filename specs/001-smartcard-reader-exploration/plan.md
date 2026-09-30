# Implementation Plan: Smart Card Hello World GUI (.NET 11 AOT + WinUI 3 + Barebones Applet)

**Branch**: `001-smartcard-reader-exploration` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-smartcard-reader-exploration/spec.md`

## Summary

Deliver a readable Java Card 3.0.5 applet that stores 128 octets in persistent Flash and answers ISO 7816-4 SELECT, READ BINARY, and UPDATE BINARY. The host is a .NET 11 WinUI 3 desktop app. It talks to the Identiv SPR332 through `winscard.dll` with `[LibraryImport]`, shows `(Gol)` on an empty read, writes `"Hello, World!"`, then shows `OK (9000)` and the same text on the next read. FlaUI UIA3 drives that cycle. This feature collects no PIN.

## Technical Context

**Language/Version**: Java Card 3.0.5 Classic (on-card); C# on .NET 11 (`net11.0-windows10.0.26100.0` for the app and UI tests, `net11.0-windows` for the library)

**Primary Dependencies**:
- On-card: `javacard.framework` (Java Card 3.0.5), jCardSim `3.0.5.x`, JUnit 5, JaCoCo with line and branch `COVEREDRATIO` minimum `1.00`
- Host: Windows `winscard.dll` via `[LibraryImport]`; Windows App SDK / WinUI 3, unpackaged (`WindowsPackageType` `None`); `FlaUI.UIA3` and NUnit
- Deployment of the CAP: GlobalPlatformPro `tools/gp.jar` (SCP02, GET STATUS before and after). No new Python card tool.

**Storage**: One persistent `byte[128]` plus a `short` length, both allocated in `install()`. Empty means length `0`. No BER-TLV header, no `OwnerPIN`.

**Testing**:
- jCardSim suite `applet/test/org/speckit/javacard/berstorage/BerStorageAppletTest.java` (SC-003)
- Service tests in `tests/SmartCard.UiTests/SmartCardServiceTests.cs` against the connected SPR332
- FlaUI flow in `tests/SmartCard.UiTests/HelloWorldUiFlowTests.cs` (SC-002)
- Manual Pas 1 → Pas 2 → Pas 3 on the WinUI window
- Physical card remains the source of truth for ATR, status words, and power-cycle retention (constitution I)

**Target Platform**:
- Card: NXP SmartMX3 P71D321, JCOP 4
- Reader: Identiv SPR332 / SPRx32, name `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`, protocol T=1
- Host: Windows 10/11 x64, inbox CCID, service `SCardSvr`

**Project Type**: Java Card CAP plus a WinUI 3 desktop app and a class library

**Performance Goals**:
- Native AOT cold start of the WinUI app under 1 s (SC-001)
- APDU round-trip for a payload up to 128 octets is dominated by the reader, not by managed allocations in `process()`

**Constraints**:
- No `new` or `newarray` inside `process()`. Transient RAM stays within 3,264 bytes.
- NVM buffer length is 128 octets. `Lc` greater than 128 is rejected with `6700` and leaves the previous contents unchanged.
- Applet AID is the 8 bytes `F0 73 70 65 63 01 01 00`. SELECT is `00 A4 04 00 08 F0 73 70 65 63 01 01 00`.
- Factory MIFARE AID `D276000085304A434F900101` and package `D276000085304A434F9001` are never deleted. GlobalPlatform master keys are never changed.
- Feature 001 collects no PIN (constitution v1.0.1, Principle V).
- Canonical UI strings (FR-006): empty `TxtOutput` is `(Gol)`; successful write sets `TxtStatus` to `OK (9000)`.

**Scale/Scope**:
- 1 applet class, `org.speckit.javacard.berstorage.BerStorageApplet`, under 100 lines, commented in Romanian (FR-001)
- 1 host library (`src/SmartCard.Core`) and 1 WinUI app (`src/SmartCard.WinUI`)
- 1 FlaUI test project (`tests/SmartCard.UiTests`)

## Constitution Check

*GATE: Evaluated against `.specify/memory/constitution.md` v1.0.1. Re-checked after Phase 1.*

| Principle | Requirement | Plan Status |
|---|---|---|
| **I. Real Hardware First** | SPR332 + JCOP 4 via `winscard.dll`; simulator may prototype | **PASS** — jCardSim covers the four APDU steps; quickstart repeats them on the physical card, including power-cycle. |
| **II. Zero-Allocation** | No `new` in `process()`; RAM ≤ 3,264; persistent state created in `install()` | **PASS** — the 128-octet buffer and length are allocated in `install()`. `process()` uses the APDU buffer only. There is no scratchpad, so no transient array is required. |
| **III. Standards** | ISO 7816-3 T=1, ISO 7816-4, GlobalPlatform 2.3 SCP02 or SCP03, Java Card 3.0.5 | **PASS** — READ BINARY `B0` and UPDATE BINARY `D6`. CAP install goes through GlobalPlatformPro SCP02 and GET STATUS. Applet source targets JC 3.0.5. |
| **IV. Factory Assets** | Never delete MIFARE `D276000085304A434F900101` / package `D276000085304A434F9001`; never change GP master keys | **PASS** — install targets only package RID `F0 73 70 65 63` and applet `F0 73 70 65 63 01 01 00`. |
| **V. PINpad** | PIN isolation applies only when a feature collects a PIN (v1.0.1) | **PASS** — feature 001 stores a public 128-octet buffer and adds no PIN prompt. |
| **Storage budget** | NVM ~140 KB, RAM 3.2 KB | **PASS** — one 128-octet array. |
| **Coprocessors / TRNG** | Required for cryptographic operations | **PASS** — this applet performs no cryptographic operation. |
| **Transaction atomicity** | Multi-field persistent updates use `beginTransaction` / `commitTransaction` | **PASS** — UPDATE BINARY writes `memory` and `dataLength` inside one transaction. |
| **Host quality gate** | Card removal and `SW != 9000` are surfaced | **PASS** — `SCARD_E_NO_READERS_AVAILABLE` (`0x8010002E`) and `SCARD_E_READER_UNAVAILABLE` (`0x80100017`) refresh the reader list. No-card shows InfoBar `Introduceti cardul in cititor`. Other status words reach the UI. |
| **Python utilities** | Any Python card tool uses `winscard.dll` or inbox CCID | **PASS** — this feature adds no Python card tool. The host is C# `[LibraryImport("winscard.dll")]`. |
| **APDU documentation** | CLA, INS, P1, P2, Lc, Data, Le, SW for every new command | **PASS** — `contracts/apdu_interface.md`. |

Post-design re-check: the data model, contracts, and quickstart introduce no further gate failure.

## Project Structure

### Documentation (this feature)

```text
specs/001-smartcard-reader-exploration/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── apdu_interface.md
│   ├── winscard_native_contract.md
│   ├── ui_automation_contract.md
│   └── pinpad_ioctl.md          # states that 001 exposes no PIN command
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
applet/
├── pom.xml
├── src/org/speckit/javacard/berstorage/BerStorageApplet.java
└── test/org/speckit/javacard/berstorage/BerStorageAppletTest.java

src/SmartCard.Core/
├── SmartCard.Core.csproj
├── Interop/WinSCardNative.cs
├── Models/SmartCardModels.cs
└── Services/
    ├── ISmartCardService.cs
    └── SmartCardService.cs

src/SmartCard.WinUI/
├── SmartCard.WinUI.csproj
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── MainPage.xaml / MainPage.xaml.cs
└── Properties/PublishProfiles/win-x64.pubxml

tests/SmartCard.UiTests/
├── SmartCard.UiTests.csproj
├── SmartCardServiceTests.cs
├── UiTestBase.cs
└── HelloWorldUiFlowTests.cs

tools/gp.jar                     # CAP install only; not a product surface of the four stories
SmartCard.slnx
```

**Structure Decision**: The on-card applet stays in `applet/`. The PC/SC library is `src/SmartCard.Core` so the WinUI app and the test project share it. The UI is unpackaged WinUI 3 in `src/SmartCard.WinUI`. FlaUI tests live in `tests/SmartCard.UiTests`.

## Complexity Tracking

> No constitutional violation remains after constitution v1.0.1. Principle V does not demand a PIN for this feature.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| *None* | N/A | N/A |
