#!/usr/bin/env python3
"""
tools/tlv_tool.py
ISO/IEC 7816-4 BER-TLV Encoder/Decoder and Card Storage CLI.

Fulfills:
- T005: BER-TLV encoder/decoder engine with bidirectional JSON/YAML mapping
- T018 [US2]: CLI read command querying tags with JSON/YAML formatting
- T023 [US3]: CLI write command serializing JSON/YAML into PUT DATA APDUs
"""

import sys
import json
import argparse
from typing import Union, Dict, Any, Tuple, List
from pathlib import Path

try:
    import yaml
    HAS_YAML = True
except ImportError:
    HAS_YAML = False

from tools.card_tool import CardConnection

# Default applet AID
BER_STORAGE_AID = bytes.fromhex("F0737065630101")

# ---------------------------------------------------------------------------
# BER-TLV Encoder / Decoder Engine
# ---------------------------------------------------------------------------
class BerTlvCodec:
    @staticmethod
    def encode_tag(tag: int) -> bytes:
        if tag <= 0xFF:
            return bytes([tag])
        elif tag <= 0xFFFF:
            return bytes([(tag >> 8) & 0xFF, tag & 0xFF])
        else:
            return bytes([(tag >> 16) & 0xFF, (tag >> 8) & 0xFF, tag & 0xFF])

    @staticmethod
    def decode_tag(data: bytes, offset: int = 0) -> Tuple[int, int, bool]:
        """Returns (tag, bytes_consumed, is_constructed)"""
        b0 = data[offset]
        is_constructed = bool(b0 & 0x20)
        
        # Check if 2-byte tag (standard ISO 7816-4 where low 5 bits are 0x1F, or 0xEF prefix)
        if (b0 & 0x1F) == 0x1F or b0 == 0xEF:
            b1 = data[offset + 1]
            tag = (b0 << 8) | b1
            return (tag, 2, is_constructed)
        else:
            return (b0, 1, is_constructed)

    @staticmethod
    def encode_length(length: int) -> bytes:
        if length < 0x80:
            return bytes([length])
        elif length <= 0xFF:
            return bytes([0x81, length])
        elif length <= 0xFFFF:
            return bytes([0x82, (length >> 8) & 0xFF, length & 0xFF])
        else:
            raise ValueError(f"Length {length} exceeds supported 16-bit capacity")

    @staticmethod
    def decode_length(data: bytes, offset: int) -> Tuple[int, int]:
        """Returns (length, bytes_consumed)"""
        b0 = data[offset]
        if b0 < 0x80:
            return (b0, 1)
        elif b0 == 0x81:
            return (data[offset + 1], 2)
        elif b0 == 0x82:
            length = (data[offset + 1] << 8) | data[offset + 2]
            return (length, 3)
        else:
            raise ValueError(f"Unsupported BER length header 0x{b0:02X}")

    @classmethod
    def encode(cls, tag: int, value: Union[dict, str, int, bytes, list]) -> bytes:
        tag_bytes = cls.encode_tag(tag)
        
        if isinstance(value, dict):
            # Constructed tag: encode child nodes
            val_bytes = bytearray()
            for k, v in value.items():
                child_tag = int(k, 16) if isinstance(k, str) and k.startswith("0x") else (int(k) if str(k).isdigit() else 0xDF01)
                val_bytes.extend(cls.encode(child_tag, v))
            val_bytes = bytes(val_bytes)
        elif isinstance(value, str):
            val_bytes = value.encode("utf-8")
        elif isinstance(value, int):
            # Encode integer as compact big-endian
            val_bytes = value.to_bytes((value.bit_length() + 7) // 8 or 1, byteorder="big")
        elif isinstance(value, bytes):
            val_bytes = value
        elif isinstance(value, list):
            # List of elements
            val_bytes = bytearray()
            for item in value:
                val_bytes.extend(cls.encode(0xDF02, item))
            val_bytes = bytes(val_bytes)
        else:
            val_bytes = str(value).encode("utf-8")

        len_bytes = cls.encode_length(len(val_bytes))
        return tag_bytes + len_bytes + val_bytes

    @classmethod
    def decode(cls, data: bytes, offset: int = 0, end: Optional[int] = None) -> Tuple[Dict[str, Any], int]:
        """Decodes BER-TLV stream into nested Python dictionary"""
        if end is None:
            end = len(data)
            
        nodes = {}
        curr = offset
        
        while curr < end:
            tag, tag_len, is_constructed = cls.decode_tag(data, curr)
            curr += tag_len
            
            length, len_len = cls.decode_length(data, curr)
            curr += len_len
            
            val_data = data[curr:curr + length]
            tag_hex = f"0x{tag:04X}" if tag > 0xFF else f"0x{tag:02X}"
            
            if is_constructed:
                child_dict, _ = cls.decode(data, curr, curr + length)
                nodes[tag_hex] = child_dict
            else:
                # Try UTF-8 string, fallback to hex
                try:
                    nodes[tag_hex] = val_data.decode("utf-8")
                except UnicodeDecodeError:
                    nodes[tag_hex] = val_data.hex().upper()
                    
            curr += length
            
        return nodes, curr

    @classmethod
    def json_to_tlv(cls, root_tag: int, json_str: str) -> bytes:
        data = json.loads(json_str)
        return cls.encode(root_tag, data)

    @classmethod
    def tlv_to_json(cls, tlv_bytes: bytes, indent: int = 2) -> str:
        nodes, _ = cls.decode(tlv_bytes)
        return json.dumps(nodes, indent=indent)

    @classmethod
    def tlv_to_yaml(cls, tlv_bytes: bytes) -> str:
        nodes, _ = cls.decode(tlv_bytes)
        if HAS_YAML:
            return yaml.dump(nodes, sort_keys=False)
        return json.dumps(nodes, indent=2)

# ---------------------------------------------------------------------------
# Card Operations
# ---------------------------------------------------------------------------
def select_applet(conn: CardConnection, aid: bytes = BER_STORAGE_AID) -> Tuple[bytes, int]:
    apdu = b"\x00\xA4\x04\x00" + bytes([len(aid)]) + aid + b"\x00"
    return conn.transmit(apdu)

def read_card_tag(conn: CardConnection, tag: int) -> Tuple[bytes, int]:
    tag_bytes = BerTlvCodec.encode_tag(tag)
    p1 = tag_bytes[0] if len(tag_bytes) >= 1 else 0x00
    p2 = tag_bytes[1] if len(tag_bytes) >= 2 else 0x00
    # ISO 7816-4 GET DATA (00 CA P1 P2 00)
    apdu = bytes([0x00, 0xCA, p1, p2, 0x00])
    return conn.transmit(apdu)

def write_card_tag(conn: CardConnection, tag: int, tlv_payload: bytes) -> Tuple[bytes, int]:
    tag_bytes = BerTlvCodec.encode_tag(tag)
    p1 = tag_bytes[0] if len(tag_bytes) >= 1 else 0x00
    p2 = tag_bytes[1] if len(tag_bytes) >= 2 else 0x00
    # ISO 7816-4 PUT DATA (00 DA P1 P2 Lc Data)
    apdu = bytes([0x00, 0xDA, p1, p2, len(tlv_payload)]) + tlv_payload
    return conn.transmit(apdu)

# ---------------------------------------------------------------------------
# CLI Entrypoint
# ---------------------------------------------------------------------------
def main():
    parser = argparse.ArgumentParser(description="ISO 7816-4 BER-TLV Data Tool for Java Card")
    subparsers = parser.add_subparsers(dest="command", help="Sub-commands")

    # Command: encode
    enc_parser = subparsers.add_parser("encode", help="Encode JSON to BER-TLV hex")
    enc_parser.add_argument("--tag", type=str, default="DF01", help="Root tag (hex, default: DF01)")
    enc_parser.add_argument("--input", type=str, required=True, help="Input JSON string or file path")

    # Command: decode
    dec_parser = subparsers.add_parser("decode", help="Decode BER-TLV hex to JSON/YAML")
    dec_parser.add_argument("--hex", type=str, required=True, help="Hex string of BER-TLV stream")
    dec_parser.add_argument("--format", choices=["json", "yaml"], default="json")

    # Command: read
    read_parser = subparsers.add_parser("read", help="Read tag from smart card applet")
    read_parser.add_argument("--tag", type=str, default="DF01", help="Target tag in hex (e.g. DF01, EF01)")
    read_parser.add_argument("--format", choices=["json", "yaml", "raw"], default="json")

    # Command: write
    write_parser = subparsers.add_parser("write", help="Write JSON/YAML object to smart card applet")
    write_parser.add_argument("--tag", type=str, default="DF01", help="Target tag in hex (e.g. DF01, EF01)")
    write_parser.add_argument("--input", type=str, required=True, help="Input JSON string or file path")

    args = parser.parse_args()

    if args.command == "encode":
        tag = int(args.tag, 16)
        in_str = Path(args.input).read_text(encoding="utf-8") if Path(args.input).is_file() else args.input
        tlv_bytes = BerTlvCodec.json_to_tlv(tag, in_str)
        print(f"BER-TLV Hex ({len(tlv_bytes)} bytes): {tlv_bytes.hex().upper()}")

    elif args.command == "decode":
        raw = bytes.fromhex(args.hex.replace(" ", ""))
        if args.format == "yaml":
            print(BerTlvCodec.tlv_to_yaml(raw))
        else:
            print(BerTlvCodec.tlv_to_json(raw))

    elif args.command == "read":
        tag = int(args.tag, 16)
        conn = CardConnection()
        try:
            conn.connect()
            _, sw = select_applet(conn)
            if sw != 0x9000:
                print(f"[!] Applet selection failed: SW=0x{sw:04X} (Has the applet been deployed?)")
                return
            
            data, sw = read_card_tag(conn, tag)
            if sw == 0x9000:
                if args.format == "raw":
                    print(f"Data: {data.hex().upper()}")
                elif args.format == "yaml":
                    print(BerTlvCodec.tlv_to_yaml(data))
                else:
                    print(BerTlvCodec.tlv_to_json(data))
            elif sw == 0x6982:
                print(f"[!] SW=0x6982: Security status not satisfied. PIN verification required on Identiv PINpad.")
            elif sw == 0x6A88:
                print(f"[!] SW=0x6A88: Referenced data object tag 0x{tag:04X} not found.")
            else:
                print(f"[!] Read failed with SW=0x{sw:04X}")
        finally:
            conn.release()

    elif args.command == "write":
        tag = int(args.tag, 16)
        in_str = Path(args.input).read_text(encoding="utf-8") if Path(args.input).is_file() else args.input
        tlv_bytes = BerTlvCodec.json_to_tlv(tag, in_str)
        conn = CardConnection()
        try:
            conn.connect()
            _, sw = select_applet(conn)
            if sw != 0x9000:
                print(f"[!] Applet selection failed: SW=0x{sw:04X}")
                return
            
            data, sw = write_card_tag(conn, tag, tlv_bytes)
            if sw == 0x9000:
                print(f"[+] Write successful! Tag 0x{tag:04X} updated ({len(tlv_bytes)} bytes written).")
            elif sw == 0x6982:
                print(f"[!] SW=0x6982: Security status not satisfied. PIN verification required before writing.")
            else:
                print(f"[!] Write failed with SW=0x{sw:04X}")
        finally:
            conn.release()

    else:
        parser.print_help()

if __name__ == "__main__":
    main()
