# Quickstart: Hello World on the SPR332

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30

Validate the applet, the PC/SC service, and the WinUI cycle. Contracts live in [apdu_interface.md](contracts/apdu_interface.md), [winscard_native_contract.md](contracts/winscard_native_contract.md), and [ui_automation_contract.md](contracts/ui_automation_contract.md). The buffer rules are in [data-model.md](data-model.md).

## Prerequisites

- Identiv SPR332 attached. Windows service `SCardSvr` running.
- NXP JCOP 4 card in the contact slot.
- JDK 21 and the repo's Maven toolchain for `applet/`.
- .NET 11 SDK.
- GlobalPlatformPro at `tools/gp.jar`. Install must not delete factory package `D276000085304A434F9001` and must not change GlobalPlatform master keys.

## 1. Applet unit coverage

From the repo root:

```powershell
mvn -f applet/pom.xml test
```

**Expected**: the four User Story 1 steps pass in jCardSim.

1. `00 A4 04 00 08 F0 73 70 65 63 01 01 00` → `90 00`
2. `00 B0 00 00 00` → `90 00` and no data
3. `00 D6 00 00 0D 48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` → `90 00`
4. `00 B0 00 00 00` → `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21 90 00`

A case with `Lc` greater than 128 returns `6700` and the stored bytes stay unchanged. JaCoCo line and branch coverage of `BerStorageApplet` is `1.00`.

## 2. Load the CAP and check the physical card

```powershell
java -jar tools\gp.jar -v -f --install applet\target\berstorage.cap
```

Then, on the SPR332, repeat the four APDUs. Remove the card, insert it again, SELECT, and READ BINARY. The response is still `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21 90 00`.

GET STATUS before and after install still lists factory package `D276000085304A434F9001`.

## 3. Service

```powershell
dotnet test tests/SmartCard.UiTests/SmartCard.UiTests.csproj --filter FullyQualifiedName~SmartCardServiceTests
```

**Expected**: the reader list contains `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`, connect uses T=1 and returns an ATR, and SELECT returns SW `0x9000`.

## 4. Window, by hand

Launch `src/SmartCard.WinUI`.

1. Citeste on an empty buffer. `TxtOutput` is `(Gol)`.
2. Scrie. `TxtStatus` is `OK (9000)`.
3. Citeste. `TxtOutput` is `Hello, World!`.

With no card in the slot, the InfoBar reads `Introduceti cardul in cititor` and the window still responds.

## 5. FlaUI

```powershell
dotnet test tests/SmartCard.UiTests/SmartCard.UiTests.csproj --filter FullyQualifiedName~HelloWorldUiFlowTests
```

**Expected**: the window comes forward; `BtnRead` yields `(Gol)`; `BtnWrite` yields `OK (9000)`; the second `BtnRead` yields `Hello, World!`.

## 6. Startup budget

Publish `src/SmartCard.WinUI` with `PublishAot` for `win-x64` and measure a cold start. The budget is under 1 s (SC-001).
