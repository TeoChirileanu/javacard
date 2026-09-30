# Data Model: 128-octet Hello World Buffer

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30
**Status**: Complete
**Supersedes**: the 2026-09-28 BER-TLV / `OwnerPIN` model

## 1. On-card layout

```text
Persistent NVM (allocated in install(), never in process())
+---------------------------+----------------------+
| memory : byte[128]        | dataLength : short   |
| octets 0 .. dataLength-1  | 0 .. 128             |
| are the stored value      | 0 means empty        |
+---------------------------+----------------------+

Transient RAM
process() uses the APDU buffer only.
No JCSystem.makeTransientByteArray.
Total transient allocation for this applet: 0 extra bytes (budget 3,264).
```

## 2. Entities

### Entity: `PersistentBuffer`

Owned by `org.speckit.javacard.berstorage.BerStorageApplet`.

| Field | Type | Constraint |
|---|---|---|
| `memory` | `byte[128]` | Allocated once in `install()`. Length is exactly 128 octets (FR-001). |
| `dataLength` | `short` | `0` at install. Legal range `0..128`. `0` means a READ BINARY returns zero data bytes and `9000`. |

**Identity**: one buffer per applet instance. AID `F0 73 70 65 63 01 01 00`.

**Validation**:
- UPDATE BINARY with `Lc` `0` sets `dataLength` to `0` and leaves `memory` contents unread.
- UPDATE BINARY with `Lc` from `1` to `128` copies that many bytes to `memory[0]` and sets `dataLength` to `Lc`.
- UPDATE BINARY with `Lc` greater than `128` returns `6700` and changes neither field (research Decision 2).
- Both field writes of a successful UPDATE run inside `JCSystem.beginTransaction()` / `commitTransaction()`.
- Values survive power loss and card removal (User Story 1, acceptance 4).

### Entity: `ApduExchange`

Host-side value in `src/SmartCard.Core/Models/SmartCardModels.cs`.

| Field | Constraint |
|---|---|
| Command CLA, INS, P1, P2 | As in `contracts/apdu_interface.md` |
| Command data | Absent for SELECT and READ BINARY. For UPDATE BINARY, the raw octets, length `Lc` `0..128`. |
| Response data | Empty when `dataLength` is `0`. Otherwise `memory[0 .. dataLength-1]`. |
| Status word | Success is `0x9000`. Wrong length is `0x6700`. Unsupported INS is `0x6D00`. Missing applet on SELECT is `0x6A82`. |

### Entity: `ReaderSession`

| Field | Constraint |
|---|---|
| Reader name | User Story 2 requires `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0` when that reader is attached. |
| Protocol | `SCARD_PROTOCOL_T1` (`0x00000002`). |
| Removal | `SCARD_E_NO_READERS_AVAILABLE` (`0x8010002E`) or `SCARD_E_READER_UNAVAILABLE` (`0x80100017`) clears the session and refreshes the reader list. |
| No card | The window shows InfoBar text `Introduceti cardul in cititor` and stays responsive. |

### Entity: `HelloWorldView`

Bound to `src/SmartCard.WinUI/MainPage.xaml`.

| Control | AutomationId | Constraint |
|---|---|---|
| Citeste | `BtnRead` | On an empty buffer, sets `TxtOutput` to exactly `(Gol)` (FR-006). After a successful write of `"Hello, World!"`, sets `TxtOutput` to `"Hello, World!"`. |
| Scrie | `BtnWrite` | Sends UPDATE BINARY of the 13 octets `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` (`Lc` `0D`). On `9000`, sets `TxtStatus` to exactly `OK (9000)` (FR-006). |
| Result | `TxtOutput` | Holds `(Gol)` or the stored text. |
| Status | `TxtStatus` | Holds `OK (9000)` after a successful write. |

There is no `OwnerPIN`, no `StorageHeader` magic, and no BER-TLV node.

## 3. State

```text
Installed (dataLength = 0)
    | READ BINARY -> 9000, zero bytes -> UI "(Gol)"
    | UPDATE BINARY Lc 01..80 -> 9000
    v
Stored (dataLength = Lc)
    | READ BINARY -> 9000 + those bytes
    | power cycle -> still Stored
    | UPDATE BINARY Lc 00 -> 9000
    v
Installed (dataLength = 0)

From either state, UPDATE BINARY with Lc > 128 returns 6700 and stays put.
```
