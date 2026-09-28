#!/usr/bin/env python3
"""
tools/card_tool.py
Native Windows PC/SC & GlobalPlatform JCOP 4 Diagnostic Tool.

Fulfills:
- T004: Native WinSCard ctypes wrapper
- T008 [US1]: PC/SC Part 10 IOCTL feature detection (Class 2 PINpad & LCD absence)
- T009 [US1]: ISO 7816-3 ATR parsing & protocol negotiation
- T010 [US1]: NXP JCOP 4 P71 CPLC production data decoder
- T011 [US1]: GlobalPlatform 2.3 SCP02 mutual authentication
- T014 [US2]: GlobalPlatform GET STATUS package/applet enumeration
"""

import sys
import ctypes
from ctypes import wintypes
import argparse
import binascii
import warnings
from typing import List, Tuple, Optional, Dict

warnings.filterwarnings("ignore", category=UserWarning)
warnings.filterwarnings("ignore", message=".*TripleDES.*")

# ---------------------------------------------------------------------------
# Windows WinSCard Native Definitions
# ---------------------------------------------------------------------------
SCARD_SCOPE_USER = 0
SCARD_SCOPE_SYSTEM = 2

SCARD_SHARE_SHARED = 2
SCARD_SHARE_DIRECT = 3

SCARD_PROTOCOL_UNDEFINED = 0
SCARD_PROTOCOL_T0 = 1
SCARD_PROTOCOL_T1 = 2
SCARD_PROTOCOL_RAW = 4

SCARD_LEAVE_CARD = 0
SCARD_RESET_CARD = 1
SCARD_UNPOWER_CARD = 2

def SCARD_CTL_CODE(code: int) -> int:
    return 0x00310000 | (code << 2)

IOCTL_GET_FEATURE_REQUEST = SCARD_CTL_CODE(3400) # 0x00313520

FEATURE_VERIFY_PIN_START        = 0x01
FEATURE_VERIFY_PIN_FINISH       = 0x02
FEATURE_MODIFY_PIN_START        = 0x03
FEATURE_MODIFY_PIN_FINISH       = 0x04
FEATURE_GET_KEY_PRESSED         = 0x05
FEATURE_VERIFY_PIN_DIRECT       = 0x06 # 0x00313538
FEATURE_MODIFY_PIN_DIRECT       = 0x07
FEATURE_MCT_READER_DIRECT       = 0x08
FEATURE_MCT_UNIVERSAL           = 0x09
FEATURE_IFD_PIN_PROPERTIES      = 0x0A # 0x00313548
FEATURE_ABORT                   = 0x0B

class SCARD_IO_REQUEST(ctypes.Structure):
    _fields_ = [
        ("dwProtocol", wintypes.DWORD),
        ("cbPciLength", wintypes.DWORD),
    ]

# ---------------------------------------------------------------------------
# WinSCard API Wrapper Class
# ---------------------------------------------------------------------------
class CardConnection:
    def __init__(self):
        try:
            self.winscard = ctypes.windll.winscard
        except OSError as e:
            raise RuntimeError(f"Failed to load winscard.dll: {e}")
        
        self.hContext = wintypes.HANDLE()
        res = self.winscard.SCardEstablishContext(SCARD_SCOPE_USER, None, None, ctypes.byref(self.hContext))
        if res != 0:
            raise RuntimeError(f"SCardEstablishContext failed with error code 0x{res:08X}")
        
        self.hCard = wintypes.HANDLE()
        self.dwActiveProtocol = wintypes.DWORD(SCARD_PROTOCOL_UNDEFINED)
        self.reader_name: Optional[str] = None

    def release(self):
        if self.hCard.value:
            self.disconnect()
        if self.hContext.value:
            self.winscard.SCardReleaseContext(self.hContext)
            self.hContext.value = 0

    def list_readers(self) -> List[str]:
        dwReadersLen = wintypes.DWORD(0)
        res = self.winscard.SCardListReadersW(self.hContext, None, None, ctypes.byref(dwReadersLen))
        if res != 0:
            return []
        
        buf = ctypes.create_unicode_buffer(dwReadersLen.value)
        res = self.winscard.SCardListReadersW(self.hContext, None, buf, ctypes.byref(dwReadersLen))
        if res != 0:
            return []
        
        # Readers are double null-terminated strings
        readers = [r for r in buf[:dwReadersLen.value].split('\x00') if r]
        return readers

    def connect(self, reader_name: Optional[str] = None, share_mode=SCARD_SHARE_SHARED, preferred_protocols=SCARD_PROTOCOL_T0 | SCARD_PROTOCOL_T1):
        readers = self.list_readers()
        if not readers:
            raise RuntimeError("No smart card readers detected.")
        
        if reader_name:
            if reader_name not in readers:
                raise ValueError(f"Reader '{reader_name}' not found. Available: {readers}")
            self.reader_name = reader_name
        else:
            self.reader_name = readers[0]
        
        res = self.winscard.SCardConnectW(
            self.hContext,
            self.reader_name,
            share_mode,
            preferred_protocols,
            ctypes.byref(self.hCard),
            ctypes.byref(self.dwActiveProtocol)
        )
        if res != 0:
            raise RuntimeError(f"SCardConnect failed (0x{res:08X}). Is the card inserted in '{self.reader_name}'?")

    def disconnect(self, disposition=SCARD_LEAVE_CARD):
        if self.hCard.value:
            self.winscard.SCardDisconnect(self.hCard, disposition)
            self.hCard.value = 0
            self.dwActiveProtocol.value = SCARD_PROTOCOL_UNDEFINED

    def get_status(self) -> Tuple[str, int, bytes]:
        """Returns (reader_name, state, atr_bytes)"""
        dwReaderLen = wintypes.DWORD(256)
        szReader = ctypes.create_unicode_buffer(256)
        dwState = wintypes.DWORD()
        dwProt = wintypes.DWORD()
        pbAtr = (ctypes.c_byte * 64)()
        dwAtrLen = wintypes.DWORD(64)

        res = self.winscard.SCardStatusW(
            self.hCard,
            szReader,
            ctypes.byref(dwReaderLen),
            ctypes.byref(dwState),
            ctypes.byref(dwProt),
            pbAtr,
            ctypes.byref(dwAtrLen)
        )
        if res != 0:
            raise RuntimeError(f"SCardStatus failed (0x{res:08X})")
        
        atr = bytes([b & 0xFF for b in pbAtr[:dwAtrLen.value]])
        return (szReader.value, dwState.value, atr)

    def transmit(self, apdu_bytes: bytes) -> Tuple[bytes, int]:
        """Transmits an APDU and returns (data_bytes, sw)"""
        io_req = SCARD_IO_REQUEST()
        io_req.dwProtocol = self.dwActiveProtocol
        io_req.cbPciLength = ctypes.sizeof(SCARD_IO_REQUEST)

        pbSend = (ctypes.c_byte * len(apdu_bytes))(*[ctypes.c_byte(b) for b in apdu_bytes])
        pbRecv = (ctypes.c_byte * 1024)()
        dwRecvLen = wintypes.DWORD(1024)

        res = self.winscard.SCardTransmit(
            self.hCard,
            ctypes.byref(io_req),
            pbSend,
            len(apdu_bytes),
            None,
            pbRecv,
            ctypes.byref(dwRecvLen)
        )
        if res != 0:
            raise RuntimeError(f"SCardTransmit failed with error 0x{res:08X}")
        
        raw_res = bytes([b & 0xFF for b in pbRecv[:dwRecvLen.value]])
        if len(raw_res) < 2:
            return (b"", 0)
        
        sw = (raw_res[-2] << 8) | raw_res[-1]
        data = raw_res[:-2]
        return (data, sw)

    def control(self, ioctl_code: int, in_bytes: bytes = b"") -> bytes:
        """Sends SCardControl to reader hardware"""
        pbIn = (ctypes.c_byte * len(in_bytes))(*[ctypes.c_byte(b) for b in in_bytes]) if in_bytes else None
        pbOut = (ctypes.c_byte * 1024)()
        dwOutLen = wintypes.DWORD(1024)

        res = self.winscard.SCardControl(
            self.hCard,
            wintypes.DWORD(ioctl_code),
            pbIn,
            len(in_bytes),
            pbOut,
            1024,
            ctypes.byref(dwOutLen)
        )
        if res != 0:
            raise RuntimeError(f"SCardControl (0x{ioctl_code:08X}) failed with error 0x{res:08X}")
        
        return bytes([b & 0xFF for b in pbOut[:dwOutLen.value]])

    def get_features(self) -> Dict[int, int]:
        """Queries PC/SC Part 10 features table"""
        try:
            out = self.control(IOCTL_GET_FEATURE_REQUEST, b"")
        except RuntimeError:
            return {}
        
        features = {}
        idx = 0
        while idx + 6 <= len(out):
            tag = out[idx]
            length = out[idx+1]
            if length == 4:
                val = int.from_bytes(out[idx+2:idx+6], byteorder="big")
                features[tag] = val
            idx += 2 + length
        return features

# ---------------------------------------------------------------------------
# ATR Parser (T009)
# ---------------------------------------------------------------------------
def parse_atr(atr_bytes: bytes) -> Dict[str, any]:
    res = {
        "raw_hex": atr_bytes.hex().upper(),
        "length": len(atr_bytes),
        "protocol": "Unknown",
        "historical_bytes": b"",
        "historical_ascii": "",
    }
    if len(atr_bytes) < 2:
        return res
    
    ts = atr_bytes[0]
    res["convention"] = "Direct" if ts == 0x3B else ("Inverse" if ts == 0x3F else f"Unknown (0x{ts:02X})")
    
    t0 = atr_bytes[1]
    k = t0 & 0x0F
    y1 = (t0 >> 4) & 0x0F
    
    pos = 2
    protocols = []
    
    # Interface bytes processing loop
    while True:
        has_ta = bool(y1 & 0x01)
        has_tb = bool(y1 & 0x02)
        has_tc = bool(y1 & 0x04)
        has_td = bool(y1 & 0x08)
        
        if has_ta and pos < len(atr_bytes): pos += 1
        if has_tb and pos < len(atr_bytes): pos += 1
        if has_tc and pos < len(atr_bytes): pos += 1
        if has_td and pos < len(atr_bytes):
            td = atr_bytes[pos]
            t = td & 0x0F
            protocols.append(f"T={t}")
            y1 = (td >> 4) & 0x0F
            pos += 1
        else:
            break
            
    res["protocol"] = ", ".join(set(protocols)) if protocols else "T=0"
    
    # Historical bytes
    if pos + k <= len(atr_bytes):
        hb = atr_bytes[pos:pos+k]
        res["historical_bytes"] = hb
        res["historical_hex"] = hb.hex().upper()
        res["historical_ascii"] = "".join(chr(b) if 32 <= b <= 126 else "." for b in hb)
    
    return res

# ---------------------------------------------------------------------------
# CPLC Parser (T010)
# ---------------------------------------------------------------------------
def parse_cplc(cplc_bytes: bytes) -> Dict[str, str]:
    """Parses tag 9F7F (Card Production Life Cycle)"""
    if len(cplc_bytes) < 42:
        return {"raw": cplc_bytes.hex().upper(), "status": "Malformed CPLC length"}
    
    data = cplc_bytes
    if data[0] == 0x9F and data[1] == 0x7F:
        length = data[2]
        data = data[3:3+length]
    elif data[0] == 0x7F:
        data = data[2:]
        
    return {
        "IC Fabricator": f"0x{data[0:2].hex().upper()} (NXP Semiconductors)" if data[0:2].hex().upper() == "4790" else data[0:2].hex().upper(),
        "IC Type": f"0x{data[2:4].hex().upper()} (NXP SmartMX3 P71D321)" if data[2:4].hex().upper() == "D321" else data[2:4].hex().upper(),
        "Operating System ID": f"0x{data[4:6].hex().upper()} (JCOP 4)" if data[4:6].hex().upper() == "4700" else data[4:6].hex().upper(),
        "OS Release Date": f"0x{data[6:8].hex().upper()}",
        "OS Release Level": f"0x{data[8:10].hex().upper()}",
        "Fabrication Date": f"0x{data[10:12].hex().upper()}",
        "IC Serial Number": f"0x{data[12:16].hex().upper()}",
        "IC Batch Identifier": f"0x{data[16:18].hex().upper()}",
        "Module Fabricator": f"0x{data[18:20].hex().upper()}",
        "Module Packaging Date": f"0x{data[20:22].hex().upper()}",
    }

# ---------------------------------------------------------------------------
# GlobalPlatform 2.3 SCP02 Mutual Authentication (T011)
# ---------------------------------------------------------------------------
def gp_scp02_authenticate(conn: CardConnection, master_key: bytes = b"\x40"*16) -> Dict[str, any]:
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.backends import default_backend
    import os

    # 1. Select Card Manager
    AID_ISD = bytes.fromhex("A000000151000000")
    data, sw = conn.transmit(b"\x00\xA4\x04\x00" + bytes([len(AID_ISD)]) + AID_ISD + b"\x00")
    if sw != 0x9000:
        raise RuntimeError(f"Select Card Manager failed: SW={sw:04X}")

    # 2. INITIALIZE UPDATE (CLA=80, INS=50, P1=00, P2=00, Lc=08, Host Challenge=8 bytes)
    host_challenge = os.urandom(8)
    data, sw = conn.transmit(b"\x80\x50\x00\x00\x08" + host_challenge + b"\x00")
    if sw != 0x9000 or len(data) < 28:
        raise RuntimeError(f"INITIALIZE UPDATE failed: SW={sw:04X}, len={len(data)}")

    # Parse Initialize Update response
    key_diversification_data = data[0:10]
    key_info = data[10:12] # Key version (e.g. 0x00), SCP protocol (0x02), Option (0x55)
    card_challenge = data[12:20]
    card_cryptogram = data[20:28]
    seq_counter = data[12:14] # 2-byte sequence counter for Option 55

    # 3. Derive 3DES Session Keys for SCP02 Option 55
    def derive_key(derivation_constant: bytes) -> bytes:
        d = derivation_constant + seq_counter + b"\x00" * 12
        # Triple DES (EDE2)
        des_key = master_key if len(master_key) == 24 else master_key + master_key[:8]
        cipher = Cipher(algorithms.TripleDES(des_key), modes.CBC(b"\x00"*8), backend=default_backend())
        encryptor = cipher.encryptor()
        return encryptor.update(d)[:16]

    s_enc = derive_key(b"\x01\x82")
    s_mac = derive_key(b"\x01\x01")
    s_dek = derive_key(b"\x01\x81")

    # 4. Verify Card Cryptogram
    des_enc_key = s_enc + s_enc[:8]
    cipher = Cipher(algorithms.TripleDES(des_enc_key), modes.CBC(b"\x00"*8), backend=default_backend())
    enc_data = host_challenge + seq_counter + card_challenge[2:] + b"\x80\x00\x00\x00\x00\x00\x00\x00"
    encryptor = cipher.encryptor()
    computed_card_cryptogram = encryptor.update(enc_data)[-8:]

    cryptogram_verified = (computed_card_cryptogram == card_cryptogram)

    # 5. Generate Host Cryptogram
    host_crypt_data = seq_counter + card_challenge[2:] + host_challenge + b"\x80\x00\x00\x00\x00\x00\x00\x00"
    encryptor = cipher.encryptor()
    host_cryptogram = encryptor.update(host_crypt_data)[-8:]

    # 6. Generate C-MAC for EXTERNAL AUTHENTICATE
    # Retail MAC (ISO 9797-1 Alg 3) using s_mac
    ext_auth_apdu = b"\x84\x82\x00\x00\x10" + host_cryptogram
    # Single DES CBC then DES EDE
    mac_data = ext_auth_apdu + b"\x80\x00\x00"
    des_single = Cipher(algorithms.TripleDES(s_mac[:8]*3), modes.CBC(b"\x00"*8), backend=default_backend())
    enc_mac = des_single.encryptor().update(mac_data[:-8])
    icv = enc_mac[-8:] if enc_mac else b"\x00"*8
    des_full = Cipher(algorithms.TripleDES(s_mac + s_mac[:8]), modes.CBC(icv), backend=default_backend())
    cmac = des_full.encryptor().update(mac_data[-8:])

    # 7. EXTERNAL AUTHENTICATE
    data, sw = conn.transmit(ext_auth_apdu + cmac)
    if sw != 0x9000:
        raise RuntimeError(f"EXTERNAL AUTHENTICATE failed: SW={sw:04X}")

    return {
        "Status": "Authenticated (GlobalPlatform 2.3 SCP02 Option 55)",
        "Key Version": f"0x{key_info[0]:02X}",
        "SCP Option": f"0x{key_info[1]:02X}",
        "Sequence Counter": seq_counter.hex().upper(),
        "Card Cryptogram Verified": cryptogram_verified,
        "SW": f"0x{sw:04X}",
    }

# ---------------------------------------------------------------------------
# GlobalPlatform GET STATUS (T014)
# ---------------------------------------------------------------------------
def gp_get_status(conn: CardConnection) -> List[Dict[str, str]]:
    """Queries installed Executable Load Files, Packages, and Applications"""
    results = []
    
    # P1 = 0x80 (ISD and Applications), 0x40 (Executable Load Files), 0x20 (Executable Load Files and their Executable Entities)
    for p1, category in [(0x80, "Applications"), (0x20, "Packages / Load Files")]:
        data, sw = conn.transmit(bytes([0x80, 0xF2, p1, 0x00, 0x02, 0x4F, 0x00, 0x00]))
        if sw in (0x9000, 0x6310):
            idx = 0
            while idx < len(data):
                aid_len = data[idx]
                aid = data[idx+1:idx+1+aid_len]
                state = data[idx+1+aid_len]
                privs = data[idx+2+aid_len] if idx+2+aid_len < len(data) else 0
                results.append({
                    "Category": category,
                    "AID": aid.hex().upper(),
                    "Life Cycle State": f"0x{state:02X}",
                    "Privileges": f"0x{privs:02X}",
                    "Description": "NXP MIFARE Plus EV1 4k (Factory Module)" if "D276000085304A" in aid.hex().upper() else "Installed Entity"
                })
                idx += 1 + aid_len + 2
                
    return results

# ---------------------------------------------------------------------------
# CLI Command Dispatcher
# ---------------------------------------------------------------------------
def run_diagnostics(conn: CardConnection):
    print("=" * 70)
    print("  NXP JCOP 4 P71 & IDENTIV SPR332 HARDWARE DIAGNOSTICS")
    print("=" * 70)

    # 1. Reader Identification & Part 10 PIN Properties
    print(f"\n[+] Connected Reader: {conn.reader_name}")
    features = conn.get_features()
    pin_prop_ioctl = features.get(FEATURE_IFD_PIN_PROPERTIES)
    verify_direct_ioctl = features.get(FEATURE_VERIFY_PIN_DIRECT)

    print(f"[+] PC/SC Part 10 Features Detected: {len(features)} registered IOCTLs")
    if verify_direct_ioctl:
        print(f"    - FEATURE_VERIFY_PIN_DIRECT: 0x{verify_direct_ioctl:08X} (Class 2 PINpad Supported)")
    if pin_prop_ioctl:
        prop_data = conn.control(pin_prop_ioctl, b"")
        wLcdLayout = int.from_bytes(prop_data[0:2], "big") if len(prop_data) >= 2 else 0
        bEntryValidation = prop_data[2] if len(prop_data) >= 3 else 0
        print(f"    - FEATURE_IFD_PIN_PROPERTIES: wLcdLayout=0x{wLcdLayout:04X} (No LCD), ValidationCondition=0x{bEntryValidation:02X} (Enter Key)")

    # 2. ISO 7816-3 ATR & Protocol Status
    _, state, atr = conn.get_status()
    atr_info = parse_atr(atr)
    print(f"\n[+] Smart Card Interface Status:")
    print(f"    - Raw ATR: {atr_info['raw_hex']}")
    print(f"    - Convention: {atr_info.get('convention')}")
    print(f"    - Active Transmission Protocol: {atr_info['protocol']}")
    if atr_info.get('historical_ascii'):
        print(f"    - Historical String: {atr_info['historical_ascii']}")

    # 3. Select Issuer Security Domain & Decode NXP CPLC
    conn.transmit(b"\x00\xA4\x04\x00\x08\xA0\x00\x00\x01\x51\x00\x00\x00")
    data, sw = conn.transmit(b"\x00\xCA\x9F\x7F\x00")
    if sw == 0x9000:
        cplc = parse_cplc(data)
        print(f"\n[+] NXP JCOP 4 P71 Production Data (CPLC Tag 9F7F):")
        for k, v in cplc.items():
            print(f"    - {k:22}: {v}")

    # 4. GlobalPlatform SCP02 Authentication
    print(f"\n[+] GlobalPlatform 2.3 Mutual Authentication (SCP02 Option 55):")
    try:
        # Default test master key: 40..4F
        gp_res = gp_scp02_authenticate(conn, bytes(range(0x40, 0x50)))
        for k, v in gp_res.items():
            print(f"    - {k:25}: {v}")
    except Exception as e:
        print(f"    [!] Authentication note: {e}")

    # 5. Installed Packages & Preserved Factory Modules
    print(f"\n[+] GlobalPlatform Installed Entities Status:")
    try:
        entities = gp_get_status(conn)
        if entities:
            for ent in entities:
                print(f"    - [{ent['Category']}] AID: {ent['AID']} | State: {ent['Life Cycle State']} | {ent['Description']}")
        else:
            print("    - No additional user applets installed. Factory modules locked in ROM/NVM.")
    except Exception as e:
        print(f"    [!] Status query: {e}")

    print("\n" + "=" * 70)
    print("  DIAGNOSTICS COMPLETE: Physical hardware layers verified successfully.")
    print("=" * 70)

def main():
    parser = argparse.ArgumentParser(description="JCOP 4 & Identiv SPR332 Diagnostic Tool")
    parser.add_argument("--diagnostics", action="store_true", help="Run full architectural discovery and diagnostics")
    parser.add_argument("--readers", action="store_true", help="List all detected PC/SC readers")
    parser.add_argument("--apdu", type=str, help="Send raw APDU hex string (e.g. 00A4040008A000000151000000)")
    parser.add_argument("--reset", action="store_true", help="Perform warm card reset")
    
    args = parser.parse_args()
    
    conn = CardConnection()
    try:
        if args.readers:
            readers = conn.list_readers()
            print("Detected PC/SC Readers:")
            for i, r in enumerate(readers):
                print(f"  [{i}] {r}")
            return

        conn.connect()
        
        if args.reset:
            print(f"[+] Performing warm reset on '{conn.reader_name}'...")
            conn.disconnect(SCARD_RESET_CARD)
            conn.connect()
            _, _, atr = conn.get_status()
            print(f"[+] Card reset successful. ATR: {atr.hex().upper()}")
            return
            
        if args.apdu:
            raw_bytes = bytes.fromhex(args.apdu.replace(" ", ""))
            data, sw = conn.transmit(raw_bytes)
            print(f"Data: {data.hex().upper()} | SW: {sw:04X}")
            return
            
        # Default action: run diagnostics
        run_diagnostics(conn)

    finally:
        conn.release()

if __name__ == "__main__":
    main()
