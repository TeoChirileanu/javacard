# Contract: WinSCard native surface

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30
**Library**: `winscard.dll`
**Binding**: `[LibraryImport]` in `src/SmartCard.Core/Interop/WinSCardNative.cs` (FR-003). No reflection `DllImport`.

## Functions

| Entry | Role |
|---|---|
| `SCardEstablishContext` | Open a user scope (`SCARD_SCOPE_USER` = `0`). |
| `SCardListReadersW` | Multi-string of reader names, UTF-16. |
| `SCardConnectW` | Connect. Preferred protocol `SCARD_PROTOCOL_T1` = `0x00000002`. |
| `SCardTransmit` | Send one APDU and receive data plus SW. |
| `SCardDisconnect` | Close the card handle. |
| `SCardReleaseContext` | Close the context. |

## Required observations (User Story 2)

- The reader list contains `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0` when the Identiv SPR332 is attached.
- Connect on that reader with T=1 returns a card handle and an ATR.
- Transmit of `00 A4 04 00 08 F0 73 70 65 63 01 01 00` returns SW `0x9000` when the applet is installed.

## Errors the host must surface

| Name | Value | UI effect |
|---|---|---|
| `SCARD_S_SUCCESS` | `0` | Continue. |
| `SCARD_E_NO_SMARTCARD` | `0x8010000C` | InfoBar `Introduceti cardul in cititor`. Window stays responsive. |
| `SCARD_E_NO_READERS_AVAILABLE` | `0x8010002E` | Refresh the reader list. |
| `SCARD_E_READER_UNAVAILABLE` | `0x80100017` | Refresh the reader list. |
| `SCARD_E_UNKNOWN_READER` | `0x80100009` | Refresh the reader list. |

Status words other than `9000` are returned to the caller. They are not treated as transport success.
