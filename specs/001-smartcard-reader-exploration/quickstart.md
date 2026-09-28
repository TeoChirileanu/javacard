# Quickstart Validation Guide: Smart Card Exploration & BER-TLV Storage

**Feature Branch**: `001-smartcard-reader-exploration`  
**Date**: 2026-09-28  

This guide provides end-to-end instructions for validating the architecture, BER-TLV data reading/writing, and Identiv SPR332 PINpad direct hardware verification.

---

## Prerequisites

1. **Hardware**:
   - Identiv SPR332 / SPRx32 USB Class 2 PINpad reader connected via USB.
   - NXP SmartMX3 P71D321 (JCOP 4) card inserted in reader contact slot.
2. **Host Environment**:
   - Windows 10/11 x64 with Windows inbox CCID Smart Card Service running (`SCardSvr`).
   - Python 3.11+ installed.
   - Python package `cryptography` installed (`pip install cryptography`).

---

## 1. Step 1: Run Live Hardware Diagnostics & Stack Discovery

Verify physical connectivity, ATR decoding, CPLC production data, and GlobalPlatform Card Manager authentication:

```powershell
python tools/card_tool.py --diagnostics
```

**Expected Outcome**:
- Reader detected: `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`.
- ATR decoded: `3B D5 18 FF 81 91 FE 1F C3 80 73 C8 21 10 0A` (T=1 protocol).
- CPLC Fabricator: `47 90` (NXP Semiconductors), Chip: `D3 21` (P71D321).
- SCP02 mutual authentication against Card Manager `A000000151000000` succeeds with `SW: 9000`.
- Installed packages listed (verifying factory MIFARE `D276000085304A434F9001` is intact).

---

## 2. Step 2: Validate BER-TLV Serialization (Offline Unit Test)

Verify that JSON and YAML data structures serialize to and from ISO 7816-4 BER-TLV with 100% round-trip fidelity:

```powershell
python -m unittest tests/test_tlv_serialization.py
```

**Expected Outcome**:
- All test cases pass (`OK`).
- Arbitrary depth nested dictionaries correctly convert into constructed tags (`bit 6 = 1`).
- Primitive values (integers, strings, byte hex) correctly convert into leaf tags (`bit 6 = 0`).

---

## 3. Step 3: Test Unauthenticated / Public Read & Write Operations

Write and read arbitrary hierarchical key-value data on public branches:

```powershell
# Write a sample nested JSON document to public tag 0xDF01
python tools/tlv_tool.py write --tag DF01 --input '{"device": {"name": "Identiv-SPR332", "class": 2}}'

# Read back tag 0xDF01 and output as YAML
python tools/tlv_tool.py read --tag DF01 --format yaml
```

**Expected Outcome**:
- Write APDU returns `SW: 9000`.
- Read APDU returns matching YAML structure with identical key-value hierarchy.

---

## 4. Step 4: Validate Class 2 PINpad Hardware Verification (`FEATURE_VERIFY_PIN_DIRECT`)

Test PIN-gated access using the physical numeric keypad and acoustic feedback on the Identiv reader:

```powershell
# Attempting to read protected tag 0xEF01 before PIN verification
python tools/tlv_tool.py read --tag EF01
# -> Expected Result: Command rejected with SW: 6982 (Security status not satisfied)

# Trigger hardware PIN prompt on Identiv reader
python tools/pinpad_tool.py verify
```

**Interactive User Flow on Reader**:
1. Reader emits **two short beeps** (prompt).
2. Enter PIN `1234` on the numeric keypad (reader emits a click sound per keypress; no characters on host PC).
3. Press Green `Enter` key on reader keypad.
4. Reader emits a **melodic confirmation tone**; host receives `SW: 9000`.

```powershell
# Read protected tag 0xEF01 after successful PIN validation
python tools/tlv_tool.py read --tag EF01
# -> Expected Result: Returns protected data with SW: 9000!
```

---

## 5. Step 5: Power-Cycle & Persistence Verification

Reset the smart card session to confirm persistence in flash memory (NVM) and security lock state reset:

```powershell
python tools/card_tool.py --reset-card

# Verify public tag DF01 still exists
python tools/tlv_tool.py read --tag DF01
# -> Returns public data (NVM persistence verified)

# Verify protected tag EF01 is automatically re-locked
python tools/tlv_tool.py read --tag EF01
# -> Returns SW: 6982 (Automatic security reset on session reset verified)
```
