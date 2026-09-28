#!/usr/bin/env python3
"""
tests/test_tlv_serialization.py
Unit tests for ISO 7816-4 BER-TLV encoder/decoder and arbitrary depth recursion.

Fulfills:
- T006: Unit tests for BER-TLV codec round-trip validation with arbitrary depth
"""

import unittest
import json
import sys
from pathlib import Path

repo_root = Path(__file__).resolve().parent.parent
if str(repo_root) not in sys.path:
    sys.path.insert(0, str(repo_root))

from tools.tlv_tool import BerTlvCodec

class TestBerTlvCodec(unittest.TestCase):
    def test_primitive_string_roundtrip(self):
        encoded = BerTlvCodec.encode(0xDF01, "Hello SmartCard")
        decoded, _ = BerTlvCodec.decode(encoded)
        self.assertIn("0xDF01", decoded)
        self.assertEqual(decoded["0xDF01"], "Hello SmartCard")

    def test_primitive_integer_roundtrip(self):
        encoded = BerTlvCodec.encode(0xDF02, 12345678)
        decoded, _ = BerTlvCodec.decode(encoded)
        self.assertIn("0xDF02", decoded)

    def test_length_encoding_boundaries(self):
        # Short form (len < 128)
        len1 = BerTlvCodec.encode_length(50)
        self.assertEqual(len1, b"\x32")
        val, consumed = BerTlvCodec.decode_length(len1, 0)
        self.assertEqual((val, consumed), (50, 1))

        # Long form 2-byte (128 <= len <= 255)
        len2 = BerTlvCodec.encode_length(200)
        self.assertEqual(len2, b"\x81\xC8")
        val, consumed = BerTlvCodec.decode_length(len2, 0)
        self.assertEqual((val, consumed), (200, 2))

        # Long form 3-byte (256 <= len <= 65535)
        len3 = BerTlvCodec.encode_length(1024)
        self.assertEqual(len3, b"\x82\x04\x00")
        val, consumed = BerTlvCodec.decode_length(len3, 0)
        self.assertEqual((val, consumed), (1024, 3))

    def test_two_byte_tag_recognition(self):
        # Tag 0xDF01 (low 5 bits of first byte 0xDF is 0x1F, so 2 bytes)
        tag, consumed, is_constructed = BerTlvCodec.decode_tag(b"\xDF\x01\x00", 0)
        self.assertEqual(tag, 0xDF01)
        self.assertEqual(consumed, 2)
        # Bit 6 of 0xDF is 0 (0b11011111), so primitive
        self.assertFalse(is_constructed)

        # Constructed tag 0xEF01 (bit 6 of 0xEF is 1: 0b11101111)
        tag, consumed, is_constructed = BerTlvCodec.decode_tag(b"\xEF\x01\x00", 0)
        self.assertEqual(tag, 0xEF01)
        self.assertEqual(consumed, 2)
        self.assertTrue(is_constructed)

    def test_arbitrary_depth_nesting(self):
        # Create an 8-level deeply nested dictionary structure
        current = {"0xDF01": "Leaf Value at Depth 8"}
        for depth in range(7, 0, -1):
            parent_tag = f"0xEF0{depth}" # Constructed tag
            current = {parent_tag: current}

        # Encode top level
        encoded = BerTlvCodec.encode(0xEF00, current)
        self.assertTrue(len(encoded) > 0)

        # Decode back and verify leaf value
        decoded, _ = BerTlvCodec.decode(encoded)
        
        # Traverse down 8 levels
        node = decoded["0xEF00"]
        for depth in range(1, 8):
            tag_key = f"0xEF0{depth}"
            self.assertIn(tag_key, node)
            node = node[tag_key]

        self.assertIn("0xDF01", node)
        self.assertEqual(node["0xDF01"], "Leaf Value at Depth 8")

    def test_json_to_tlv_roundtrip(self):
        sample_json = json.dumps({
            "0xDF01": "Identiv-SPR332",
            "0xDF02": "JCOP4-P71",
            "0xEF01": {
                "0xDF10": "Protected Record 1",
                "0xDF11": "Protected Record 2"
            }
        })
        tlv_bytes = BerTlvCodec.json_to_tlv(0xEF00, sample_json)
        json_out = BerTlvCodec.tlv_to_json(tlv_bytes)
        parsed = json.loads(json_out)
        
        self.assertIn("0xEF00", parsed)
        root = parsed["0xEF00"]
        self.assertEqual(root["0xDF01"], "Identiv-SPR332")
        self.assertEqual(root["0xDF02"], "JCOP4-P71")
        self.assertEqual(root["0xEF01"]["0xDF10"], "Protected Record 1")

if __name__ == "__main__":
    unittest.main()
