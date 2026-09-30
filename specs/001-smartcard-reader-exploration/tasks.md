---
description: "Task list for the Smart Card Hello World GUI (.NET 11 AOT + WinUI 3 + barebones applet)"
---

# Tasks: Smart Card Hello World GUI (.NET 11 AOT + WinUI 3 + Barebones Applet)

**Input**: Design documents from `/specs/001-smartcard-reader-exploration/`

**Prerequisites**: spec.md (required; pivot session 2026-09-28), plan.md (present, pre-pivot), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included. spec.md requests jCardSim coverage (SC-003), service tests on hardware (User Story 2), and a FlaUI `dotnet test` cycle (FR-005, SC-002).

**Organization**: Tasks are grouped by user story so each story can be implemented and tested on its own.

**Artifact note**: User stories and APDU bytes come from spec.md, including the 2026-09-30 clarification that `TxtOutput` is `(Gol)` and `TxtStatus` is `OK (9000)` (FR-006). `plan.md`, `research.md`, `data-model.md`, `quickstart.md`, and `contracts/` were regenerated on 2026-09-30 for this Hello World feature. Constitution v1.0.1 still binds: no `new` inside `process()`, transient RAM ≤ 3,264 bytes, Java Card 3.0.5, physical SPR332 + JCOP 4 as the source of truth, no deletion of factory MIFARE AID `D276000085304A434F900101` / package `D276000085304A434F9001`, no GlobalPlatform master-key change, and no PIN prompt in this feature.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Applet: `applet/` (Maven, Java Card 3.0.5, jCardSim)
- Host library: `src/SmartCard.Core/`
- WinUI 3 app: `src/SmartCard.WinUI/`
- FlaUI / service tests: `tests/SmartCard.UiTests/`
- Solution: `SmartCard.slnx`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution layout for the pivoted host (.NET 11, WinUI 3, FlaUI) and the barebones applet

- [X] T001 Create solution `SmartCard.slnx` with directories `applet/src/org/speckit/javacard/berstorage/`, `applet/test/org/speckit/javacard/berstorage/`, `src/SmartCard.Core/`, `src/SmartCard.WinUI/`, and `tests/SmartCard.UiTests/`
- [X] T002 [P] Configure the unpackaged WinUI 3 app in `src/SmartCard.WinUI/SmartCard.WinUI.csproj` for `TargetFramework` `net11.0-windows10.0.26100.0`, `WindowsPackageType` `None`, Windows App SDK, nullable enable, and the win-dev-skills v0.6.1 project shape from spec.md
- [X] T003 [P] Configure the host library in `src/SmartCard.Core/SmartCard.Core.csproj` for `TargetFramework` `net11.0-windows`, nullable enable, and `AllowUnsafeBlocks` so Native AOT P/Invoke can compile
- [X] T004 [P] Configure `applet/pom.xml` for Java Card 3.0.5, jCardSim, JUnit 5, and a JaCoCo CLASS rule with line and branch `COVEREDRATIO` minimum `1.00`
- [X] T005 [P] Configure the NUnit + `FlaUI.UIA3` project in `tests/SmartCard.UiTests/SmartCard.UiTests.csproj` on `net11.0-windows10.0.26100.0`, referencing `src/SmartCard.Core/SmartCard.Core.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: One APDU contract and the shared host types every story implements against

**⚠️ CRITICAL**: No user story work begins until this phase is complete

- [X] T006 [P] Rewrite `specs/001-smartcard-reader-exploration/contracts/apdu_interface.md` so the binding commands are only SELECT, READ BINARY, and UPDATE BINARY, each with CLA, INS, P1, P2, Lc, Data, Le, and SW1/SW2. Verbatim bytes from the User Story 1 independent test: SELECT `00 A4 04 00 08 F0 73 70 65 63 01 01 00` → `90 00`; READ BINARY `00 B0 00 00 00` → zero or more data bytes and `90 00`; UPDATE BINARY `00 D6 00 00 0D 48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` → `90 00`. NVM buffer length is 128 octets (FR-001). A payload longer than 128 octets is rejected or truncated safely (edge case). SELECT of a missing applet returns `6A82`.
- [X] T007 [P] Add reader, APDU command, APDU response, and operation-result models in `src/SmartCard.Core/Models/SmartCardModels.cs`. The applet AID field is the 8 bytes `F0 73 70 65 63 01 01 00`. The success status word is `0x9000`. The NVM capacity constant is 128.
- [X] T008 [P] Declare `[LibraryImport("winscard.dll")]` methods `SCardEstablishContext`, `SCardListReaders`, `SCardConnect`, `SCardTransmit`, `SCardDisconnect`, and `SCardReleaseContext` in `src/SmartCard.Core/Interop/WinSCardNative.cs`, including error codes `SCARD_E_NO_READERS_AVAILABLE` and `SCARD_E_READER_UNAVAILABLE` (FR-003)

**Checkpoint**: AID, 128-octet buffer, and the three APDUs are fixed. User story work can start.

---

## Phase 3: User Story 1 - Barebones "Hello World" Java Card Applet (Priority: P1) 🎯 MVP

**Goal**: A Java Card applet short enough to read in two minutes, storing 128 octets in Flash and answering ISO 7816-4 READ BINARY and UPDATE BINARY.

**Independent Test**: Send, in jCardSim or on the card: (1) `00 A4 04 00 08 F0 73 70 65 63 01 01 00` → `90 00`; (2) `00 B0 00 00 00` → empty data, `90 00`; (3) `00 D6 00 00 0D 48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` → `90 00`; (4) `00 B0 00 00 00` → `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21 90 00`.

### Tests for User Story 1

- [X] T009 [P] [US1] Write jCardSim tests first in `applet/test/org/speckit/javacard/berstorage/BerStorageAppletTest.java` for the four independent-test steps above, for acceptance scenario 4 (after deselect/reset the same READ BINARY still returns `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` and `90 00`), and for a payload longer than 128 octets. JaCoCo on `BerStorageApplet` must reach line and branch `COVEREDRATIO` `1.00` (SC-003).

### Implementation for User Story 1

- [X] T010 [US1] Implement `applet/src/org/speckit/javacard/berstorage/BerStorageApplet.java` in under 100 lines, commented entirely in Romanian (FR-001). `process()` accepts only READ BINARY INS `B0` and UPDATE BINARY INS `D6`. Allocate the 128-octet persistent buffer in `install()`. No `new` and no `newarray` inside `process()` (constitution II). Keep total transient RAM within 3,264 bytes. Target Java Card 3.0.5 Classic (constitution III). Package RID `F0 73 70 65 63`, applet AID `F0 73 70 65 63 01 01 00`. An empty READ returns 0 data bytes and `9000`. UPDATE BINARY keeps the bytes in NVM across power loss.
- [X] T011 [US1] Build a Java Card 3.0.5 CAP to `applet/target/berstorage.cap` from `applet/pom.xml`. Installation authenticates to the card, runs GET STATUS before and after, leaves factory AID `D276000085304A434F900101` and package `D276000085304A434F9001` in place, and does not change GlobalPlatform master keys (constitution IV).

**Checkpoint**: User Story 1 is testable in jCardSim with the four APDU steps and SC-003 coverage, without the WinUI app.

---

## Phase 4: User Story 2 - .NET 11 Native AOT SmartCard Service (Priority: P1)

**Goal**: A Native AOT PC/SC service that talks to the Identiv SPR332 through `winscard.dll` with `[LibraryImport]`.

**Independent Test**: Service tests call `SCardEstablishContext`, `SCardListReaders`, `SCardConnect`, and `SCardTransmit` on the connected reader and card.

### Tests for User Story 2

- [X] T012 [P] [US2] Write service tests first in `tests/SmartCard.UiTests/SmartCardServiceTests.cs`: the reader list contains `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`; connect with `SCARD_PROTOCOL_T1` returns an ATR; transmit of SELECT `00 A4 04 00 08 F0 73 70 65 63 01 01 00` returns SW `0x9000`; a missing reader surfaces `SCARD_E_NO_READERS_AVAILABLE` or `SCARD_E_READER_UNAVAILABLE`.

### Implementation for User Story 2

- [X] T013 [P] [US2] Define list, connect (`SCARD_PROTOCOL_T1`), transmit, and disconnect on `src/SmartCard.Core/Services/ISmartCardService.cs` with no Python runtime (FR-002)
- [X] T014 [US2] Implement `src/SmartCard.Core/Services/SmartCardService.cs` on `src/SmartCard.Core/Interop/WinSCardNative.cs`. On reader loss, refresh the reader list. Return status words other than `9000` to the caller (constitution host quality gate). Depends on T012 and T013.
- [X] T015 [P] [US2] Fill `specs/001-smartcard-reader-exploration/contracts/winscard_native_contract.md` with the `[LibraryImport("winscard.dll")]` methods, the reader name `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`, and protocol `SCARD_PROTOCOL_T1`

**Checkpoint**: User Story 2 lists the SPR332, connects T=1, and SELECTs the applet, without the WinUI buttons.

---

## Phase 5: User Story 3 - WinUI 3 Desktop Hello World App (Priority: P2)

**Goal**: Two large buttons, „Citeste” and „Scrie”, and a result area for the read → write → read cycle.

**Independent Test**: Launch the WinUI 3 app and walk Pas 1 → Pas 2 → Pas 3 by hand.

### Implementation for User Story 3

- [X] T016 [US3] Build `src/SmartCard.WinUI/MainPage.xaml` with buttons „Citeste” and „Scrie” and AutomationIds `BtnRead`, `BtnWrite`, `TxtOutput`, and `TxtStatus`, plus an InfoBar (FR-004)
- [X] T017 [US3] In `src/SmartCard.WinUI/MainPage.xaml.cs`, Citeste on an empty card sets `TxtOutput` to exactly `(Gol)` (FR-006). Scrie sends UPDATE BINARY of `"Hello, World!"` (`Lc` `0D`, data `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21`) and sets `TxtStatus` to exactly `OK (9000)`. The next Citeste sets `TxtOutput` to `"Hello, World!"`.
- [X] T018 [US3] When the reader has no card, show InfoBar text `Introduceti cardul in cititor` and keep the window responsive, in `src/SmartCard.WinUI/MainPage.xaml` and `src/SmartCard.WinUI/MainPage.xaml.cs`
- [X] T019 [US3] When the reader is removed during a session, handle `SCARD_E_NO_READERS_AVAILABLE` or `SCARD_E_READER_UNAVAILABLE` and refresh the device list in `src/SmartCard.WinUI/MainPage.xaml.cs`
- [X] T020 [US3] Host the page from `src/SmartCard.WinUI/MainWindow.xaml` and `src/SmartCard.WinUI/App.xaml.cs` so the manual Pas 1 → Pas 2 → Pas 3 path launches

**Checkpoint**: User Story 3 completes the three clicks on a card that already runs User Story 1, using the User Story 2 service.

---

## Phase 6: User Story 4 - Automatizare Test UI cu FlaUI & Modelare UFO (Priority: P3)

**Goal**: A FlaUI UIA3 test launches the app, clicks by AutomationId, and checks the hello-world cycle with no human input, as a base for a UI-focused agent.

**Independent Test**: `dotnet test` on `tests/SmartCard.UiTests/SmartCard.UiTests.csproj`.

Clarification 2026-09-30 makes US3 and US4 use the same strings: `TxtOutput` is exactly `(Gol)` on an empty read, and `TxtStatus` is exactly `OK (9000)` after a successful write (FR-006).

### Tests for User Story 4

- [X] T021 [US4] In `tests/SmartCard.UiTests/UiTestBase.cs`, start the compiled WinUI executable and bring the main window to the foreground
- [X] T022 [US4] In `tests/SmartCard.UiTests/HelloWorldUiFlowTests.cs`, click `BtnRead` and assert `TxtOutput` equals `(Gol)`; click `BtnWrite` and assert `TxtStatus` is `OK (9000)`; click `BtnRead` again and assert `TxtOutput` contains exactly `"Hello, World!"`. The run command is `dotnet test` on `tests/SmartCard.UiTests/SmartCard.UiTests.csproj` (SC-002). Depends on T021.
- [X] T023 [P] [US4] Fill `specs/001-smartcard-reader-exploration/contracts/ui_automation_contract.md` with AutomationIds `BtnRead`, `BtnWrite`, `TxtOutput`, `TxtStatus` and the three assertion strings from T022

**Checkpoint**: `dotnet test` drives Citeste → Scrie → Citeste without a person at the keyboard.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Bring the pre-pivot design docs in line with spec.md, and record the measurable success criteria

- [X] T024 [P] Rewrite `specs/001-smartcard-reader-exploration/quickstart.md` to the four APDU steps and the WinUI Pas 1 → Pas 2 → Pas 3 flow, then run that guide on the physical Identiv SPR332 and the JCOP 4 card
- [X] T025 [P] Rewrite `specs/001-smartcard-reader-exploration/data-model.md` so the stored entity is one 128-octet persistent buffer (FR-001). Drop the pre-pivot field constraints: `magic` `0x54 0x4C`, `version` `0x01`, BER-TLV tag and length forms, public tags `0xDF01 - 0xDF7F`, protected tags `0xEF01 - 0xEFFF`, `tryLimit` 3, `maxPinSize` 8, and `defaultPin` `1234`
- [X] T026 [P] Update `specs/001-smartcard-reader-exploration/plan.md` and `specs/001-smartcard-reader-exploration/research.md` so language, dependencies, source tree, and decisions match spec.md (.NET 11 Native AOT, WinUI 3, FlaUI, READ BINARY / UPDATE BINARY, 128-octet NVM)
- [X] T027 [P] Turn on Native AOT publish in `src/SmartCard.WinUI/SmartCard.WinUI.csproj` and `src/SmartCard.WinUI/Properties/PublishProfiles/win-x64.pubxml`, and record a cold start under 1 s (SC-001)
- [X] T028 Run `mvn -f applet/pom.xml test` and `dotnet test tests/SmartCard.UiTests/SmartCard.UiTests.csproj`, and keep JaCoCo line and branch `COVEREDRATIO` at `1.00` (SC-003) and the FlaUI cycle green (SC-002)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — blocks all user stories
- **User Stories (Phase 3+)**: Depend on Foundational
  - US1 and US2 are both P1 and can proceed in parallel after Phase 2
  - US2's SELECT `9000` assertion needs the User Story 1 applet installed on the card under test
  - US3 depends on US2's service and on a card running US1
  - US4 depends on US3's AutomationIds and labels
- **Polish (Phase 7)**: Depends on the stories you choose to ship. T028 runs after US1 and US4 tests exist. T027 can run once the WinUI project builds.

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2. No dependency on US2, US3, or US4. MVP.
- **User Story 2 (P1)**: After Phase 2. Enumeration and T=1 connect are independent of US1. The SELECT `0x9000` test needs the US1 applet on the card.
- **User Story 3 (P2)**: After US2. The three-click check needs US1 on the card.
- **User Story 4 (P3)**: After US3. FlaUI only drives the UI User Story 3 exposes.

### Within Each User Story

- Tests (T009, T012, T021, T022) are written and failing before the implementation they lock
- Models and contracts before services
- Service before the page handlers
- Page before the FlaUI clicks
- Story checkpoint before the next priority

### Parallel Opportunities

- T002, T003, T004, T005 in parallel
- T006, T007, T008 in parallel
- After Phase 2: US1 (T009 → T010 → T011) in parallel with US2 (T012, T013, T015, then T014)
- T023 in parallel with T021
- T024, T025, T026, T027 in parallel

---

## Parallel Example: User Story 1

```text
# After Phase 2, start the failing simulator test (no host app required):
Task: "T009 [US1] jCardSim tests in applet/test/org/speckit/javacard/berstorage/BerStorageAppletTest.java"

# In parallel, another person can start User Story 2:
Task: "T012 [US2] service tests in tests/SmartCard.UiTests/SmartCardServiceTests.cs"
Task: "T013 [US2] ISmartCardService in src/SmartCard.Core/Services/ISmartCardService.cs"
Task: "T015 [US2] winscard_native_contract.md"
```

---

## Parallel Example: User Story 2

```text
Task: "T012 [US2] service tests in tests/SmartCard.UiTests/SmartCardServiceTests.cs"
Task: "T013 [US2] ISmartCardService in src/SmartCard.Core/Services/ISmartCardService.cs"
Task: "T015 [US2] fill contracts/winscard_native_contract.md"

# Then, after T012 and T013:
Task: "T014 [US2] SmartCardService in src/SmartCard.Core/Services/SmartCardService.cs"
```

---

## Parallel Example: User Story 4

```text
Task: "T021 [US4] launch and foreground in tests/SmartCard.UiTests/UiTestBase.cs"
Task: "T023 [US4] fill contracts/ui_automation_contract.md"

# Then:
Task: "T022 [US4] click cycle in tests/SmartCard.UiTests/HelloWorldUiFlowTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run the four APDU steps in jCardSim
5. Demo the applet alone before any UI

### Incremental Delivery

1. Setup + Foundational
2. User Story 1 → jCardSim hello-world → demo (MVP)
3. User Story 2 → reader list, T=1 connect, SELECT `9000` on the SPR332
4. User Story 3 → manual Citeste / Scrie / Citeste
5. User Story 4 → `dotnet test` covers that cycle
6. Polish → docs match spec.md, Native AOT cold start under 1 s

### Parallel Team Strategy

1. Everyone finishes Setup and Foundational
2. Then:
   - Developer A: User Story 1 (applet + jCardSim)
   - Developer B: User Story 2 (WinSCard service)
3. After both: Developer C takes User Story 3, then User Story 4

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [US1]–[US4] map to spec.md priorities P1, P1, P2, P3
- Pre-pivot data-model constraints (`magic` `0x54 0x4C`, `OwnerPIN` `tryLimit` 3, `maxPinSize` 8, `defaultPin` `1234`, BER-TLV depth 8) are removed in T025 because the 2026-09-28 spec pivot replaced that model with a 128-octet buffer
- `contracts/pinpad_ioctl.md` and the Python tools `tools/card_tool.py`, `tools/tlv_tool.py`, `tools/pinpad_tool.py` sit outside these four stories
- Empty-read and write-status strings are `(Gol)` and `OK (9000)` (spec clarification 2026-09-30, FR-006)
- This file replaces the 2026-09-28 task list whose stories were architectural diagnostics, BER-TLV read/write, and PINpad verify

## Phase 8: Convergence

- [X] T029 Record in `specs/001-smartcard-reader-exploration/research.md` that the always-listed simulator reader and the RAM emulation fallback in `src/SmartCard.Core/Services/SmartCardService.cs` remain retained prototyping outside US1–US4. Leave that implementation in place. per plan: source tree (unrequested)
