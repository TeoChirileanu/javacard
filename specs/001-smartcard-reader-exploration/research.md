# Technical Research: Smart Card Hello World GUI

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30
**Status**: Complete
**Spec**: [spec.md](spec.md) pivot 2026-09-28, string clarification 2026-09-30
**Constitution**: v1.0.1

## Decisions

### Decision 1: Flat 128-octet NVM, READ BINARY and UPDATE BINARY

- **Decision**: One persistent `byte[128]` and a `short` length. SELECT, READ BINARY (`00 B0`), and UPDATE BINARY (`00 D6`) only.
- **Rationale**: The 2026-09-28 spec pivot asks for a file a developer can read in two minutes (FR-001). ISO 7816-4 already names these instructions. An empty card returns `9000` and zero data bytes, which the UI renders as `(Gol)`.
- **Alternatives considered**:
  - BER-TLV with GET DATA / PUT DATA and an 8-level tree. Rejected by the pivot. It hides the hello-world path behind a codec.
  - CBOR. Same objection, plus no ISO 7816-4 instruction maps onto it directly.

### Decision 2: Reject a payload longer than 128 octets

- **Decision**: If `Lc` is greater than 128, the applet throws `6700` (wrong length) and does not change `memory` or `dataLength`.
- **Rationale**: The spec allows reject or safe truncation. Rejection keeps the previous `"Hello, World!"` intact and matches a host that already refuses text over 128 bytes with status `6700`. Truncation would silently drop bytes.
- **Alternatives considered**: Truncate to 128 and return `9000`. Rejected because the caller cannot tell that the card stored a prefix.

### Decision 3: Atomic update of buffer and length

- **Decision**: `Util.arrayCopy` into `memory` and the assignment of `dataLength` run inside `JCSystem.beginTransaction()` / `commitTransaction()`.
- **Rationale**: Constitution transaction atomicity. A tear between the copy and the length update would otherwise make a later READ BINARY return a mix of old and new bytes.
- **Alternatives considered**: Rely on a single `arrayCopy`. Rejected because `dataLength` is a second persistent field.

### Decision 4: AID `F0 73 70 65 63 01 01 00` (8 bytes)

- **Decision**: Package RID `F0 73 70 65 63`. Applet AID `F0 73 70 65 63 01 01 00`. SELECT APDU `00 A4 04 00 08 F0 73 70 65 63 01 01 00`.
- **Rationale**: This is the byte string in the User Story 1 independent test. The proprietary RID stays clear of factory MIFARE AID `D276000085304A434F900101` and package `D276000085304A434F9001` (constitution IV).
- **Alternatives considered**: A 7-byte AID `F0 73 70 65 63 01 01` from the pre-pivot contract. Rejected because the spec's SELECT uses `Lc` `08` and a trailing `00`.

### Decision 5: Host is .NET 11 `[LibraryImport("winscard.dll")]`, not Python

- **Decision**: `src/SmartCard.Core` calls `SCardEstablishContext`, `SCardListReaders`, `SCardConnect` with `SCARD_PROTOCOL_T1` (`0x00000002`), `SCardTransmit`, `SCardDisconnect`, and `SCardReleaseContext`.
- **Rationale**: FR-002 and FR-003 require C# on .NET 11, Native AOT, and source-generated P/Invoke. The reader string asserted by User Story 2 is `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`.
- **Alternatives considered**:
  - Python `ctypes` on `winscard.dll`. Removed by the pivot.
  - `System.Device` or a third-party PC/SC package. Rejected because AOT needs a direct `[LibraryImport]` and no reflection-based `DllImport`.

### Decision 6: WinUI 3 unpackaged app

- **Decision**: `src/SmartCard.WinUI` is an unpackaged WinUI 3 app (`WindowsPackageType` `None`) on `net11.0-windows10.0.26100.0`, shaped like win-dev-skills v0.6.1. Buttons „Citeste” and „Scrie” expose AutomationIds `BtnRead` and `BtnWrite`.
- **Rationale**: FR-004. Unpackaged launch is what FlaUI starts by executable path.
- **Alternatives considered**: MSIX-packaged identity. Extra install step for a hello-world window, and a harder path for a local UI test.

### Decision 7: Canonical status strings

- **Decision**: Empty read sets `TxtOutput` to exactly `(Gol)`. Successful write sets `TxtStatus` to exactly `OK (9000)`. The following read sets `TxtOutput` to `"Hello, World!"`.
- **Rationale**: Clarification session 2026-09-30 and FR-006. The longer phrases `(Gol) - 0 octeti primiti` and `Scriere reusita: OK (9000)` are withdrawn.
- **Alternatives considered**: Keep both phrases and let the FlaUI test use a substring. Rejected because User Story 4 asserts equality on `(Gol)` and `OK (9000)`.

### Decision 8: No PIN in feature 001

- **Decision**: No VERIFY APDU, no `OwnerPIN`, no `FEATURE_VERIFY_PIN_DIRECT` call.
- **Rationale**: The pivot removed PIN. Constitution v1.0.1 says Principle V applies when a feature collects a PIN, and a feature that does not MUST NOT add a PIN prompt to satisfy the principle.
- **Alternatives considered**: Keep a dormant PIN so the old PINpad contract stays live. Rejected because it contradicts the pivot and the amendment.

### Decision 9: jCardSim for coverage, the physical card for truth

- **Decision**: JaCoCo line and branch coverage of `BerStorageApplet` is `1.00` under jCardSim (SC-003). Power-cycle retention, ATR, and T=1 connect are checked on the JCOP 4 card in the SPR332 (constitution I).
- **Rationale**: The simulator cannot prove NVM across a real reset. The card cannot produce a branch-coverage number.
- **Alternatives considered**: Hardware-only tests. They cannot measure SC-003.

### Decision 10: Native AOT publish for the startup budget

- **Decision**: Release publish of `src/SmartCard.WinUI` sets `PublishAot` for `win-x64` (`Properties/PublishProfiles/win-x64.pubxml`). The published exe is native (no CLR header). `CopyWinUiAppResources` copies `SmartCard.WinUI.pri` and the page `.xbf` files into the publish directory, because a Native AOT publish does not copy that XAML index. SC-001 is a cold start under 1 s, measured on that published executable.
- **Rationale**: FR-002 requires Native AOT. A framework-dependent `dotnet run` does not answer SC-001.
- **Measured 2026-09-30** on the published `win-x64` exe, process start until `MainWindowHandle` is set. Cold-cache start: 663 ms. Later starts: 424 ms, 369 ms, 388 ms, 399 ms, 379 ms, then 432 ms, 365 ms, 427 ms. Window-title visibility on one trio was 755 ms, 411 ms, 379 ms. Every sample is under 1 s, so SC-001 is met.
- **Alternatives considered**: ReadyToRun without AOT. It still ships a runtime and does not meet the Native AOT requirement.

### Decision 11: Simulator reader and RAM emulation stay

- **Decision**: `src/SmartCard.Core/Services/SmartCardService.cs` keeps the always-listed reader `[Simulator] Java Card Virtual Reader (ISO 7816-4)` and the RAM emulation fallback used when SELECT of the on-card applet fails. Both remain retained prototyping outside US1–US4. The WinUI label for that mode is `Emulare RAM`. The physical path stays `Fizic EEPROM`.
- **Rationale**: Constitution I allows simulation to assist prototyping. The SPR332 and the JCOP 4 card remain the source of truth for ATR, status words, and power-cycle retention. These two paths are outside FR-001 through FR-006 and are not a success criterion of feature 001. Convergence task T029 records that they stay.
- **Alternatives considered**: Remove the simulator entry and the fallback so every click hits the card. Rejected because a labeled prototype path is still needed for an empty-card UI cycle and for a card whose applet is not flashed yet.

## Best practices carried into the design

1. Document every command in `contracts/apdu_interface.md` with CLA, INS, P1, P2, Lc, Data, Le, and SW.
2. Map reader-removal codes `0x8010002E` and `0x80100017` to a refreshed reader list, and map no-card to InfoBar text `Introduceti cardul in cititor` without blocking the window.
3. Install the CAP with GlobalPlatformPro so SCP02 authentication and GET STATUS happen before and after load. The install command must not delete the factory MIFARE package and must not change master keys.
4. The on-screen CAP-install progress indicator is existing host behavior. It is outside FR-001 through FR-006 and is not a success criterion of feature 001.
