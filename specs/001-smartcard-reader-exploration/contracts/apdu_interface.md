# Contract: ISO 7816-4 APDU for the Hello World applet

**Feature Branch**: `001-smartcard-reader-exploration`
**Date**: 2026-09-30
**Applet AID**: `F0 73 70 65 63 01 01 00` (8 bytes)
**Package RID**: `F0 73 70 65 63`

Binding commands are SELECT, READ BINARY, and UPDATE BINARY. Feature 001 defines no VERIFY, GET DATA, PUT DATA, DELETE, or RESET PIN command.

## 1. Summary

| Command | CLA | INS | P1 | P2 | Lc | Data | Le | SW |
|---|---|---|---|---|---|---|---|---|
| SELECT | `00` | `A4` | `04` | `00` | `08` | `F0 73 70 65 63 01 01 00` | absent | `9000`, `6A82` |
| READ BINARY | `00` | `B0` | `00` | `00` | absent | absent | `00` | `9000` |
| UPDATE BINARY | `00` | `D6` | `00` | `00` | `00..80` | `Lc` octets | absent | `9000`, `6700` |
| other INS | `00` | other |  |  |  |  |  | `6D00` |

## 2. SELECT

- **CLA** `00`, **INS** `A4`, **P1** `04`, **P2** `00`
- **Lc** `08`
- **Data** `F0 73 70 65 63 01 01 00`
- **Le** absent
- **SW** `9000` applet selected, no response data. `6A82` application not found.
- User Story 1 step 1: `00 A4 04 00 08 F0 73 70 65 63 01 01 00` → `90 00`

## 3. READ BINARY

- **CLA** `00`, **INS** `B0`, **P1** `00`, **P2** `00`
- **Lc / Data** absent
- **Le** `00` (up to 256; this applet returns at most 128)
- **Response data**: `memory[0 .. dataLength-1]`, or no data bytes when `dataLength` is `0`
- **SW** `9000`
- Empty card: `00 B0 00 00 00` → `90 00`
- After `"Hello, World!"`: `00 B0 00 00 00` → `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21 90 00`
- The same response is required after a power cycle (User Story 1, acceptance 4).

## 4. UPDATE BINARY

- **CLA** `00`, **INS** `D6`, **P1** `00`, **P2** `00`
- **Lc** number of data bytes, `00` through `80` (0..128)
- **Data** the octets to store. The hello-world payload is `Lc` `0D` and `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21`
- **Le** absent
- **SW** `9000` stored. `6700` when `Lc` is greater than 128; `memory` and `dataLength` stay as they were.
- Full hello-world command: `00 D6 00 00 0D 48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` → `90 00`
- The copy and the length update are one Java Card transaction.

## 5. Anything else

Any other INS returns `6D00` (instruction not supported) and does not change NVM.
