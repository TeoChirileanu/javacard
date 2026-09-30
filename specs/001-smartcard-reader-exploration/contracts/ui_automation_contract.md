# Contract: WinUI AutomationIds for the hello-world cycle

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30
**Window**: unpackaged WinUI 3 executable from `src/SmartCard.WinUI`
**Driver**: FlaUI UIA3, `tests/SmartCard.UiTests`

## Controls

| AutomationId | Role | Assertion |
|---|---|---|
| `BtnRead` | Button „Citeste” | `.Click()` runs READ BINARY. |
| `BtnWrite` | Button „Scrie” | `.Click()` runs UPDATE BINARY of `"Hello, World!"`. |
| `TxtOutput` | Result text | After the first read on an empty card, the text is exactly `(Gol)`. After the write and a second read, the text is exactly `"Hello, World!"`. |
| `TxtStatus` | Status text | After a successful write, the text is exactly `OK (9000)` (FR-006). |

## Sequence (User Story 4)

1. Launch the compiled executable and bring the main window to the foreground.
2. Click `BtnRead`. `TxtOutput` equals `(Gol)`.
3. Click `BtnWrite`. `TxtStatus` equals `OK (9000)`.
4. Click `BtnRead`. `TxtOutput` equals `"Hello, World!"`.

The independent run is `dotnet test` on `tests/SmartCard.UiTests/SmartCard.UiTests.csproj` (SC-002).

## Missing card

When the reader has no card, an InfoBar shows `Introduceti cardul in cititor` and the window remains usable.
