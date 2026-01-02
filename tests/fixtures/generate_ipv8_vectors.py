#!/usr/bin/env python3
"""
Generate IPv8 protocol test vectors for C# cross-language verification.
Produces hex dumps of IPv8 messages using py-ipv8 for byte-level compatibility testing (FR-048).

Requirements:
    pip install git+https://github.com/Tribler/py-ipv8.git

Usage:
    python generate_ipv8_vectors.py > ipv8_test_vectors.json
"""

import json
import struct
import sys
from binascii import hexlify

try:
    from ipv8.messaging.anonymization.payload import CreatePayload, CreatedPayload, ExtendPayload, ExtendedPayload
    from ipv8.messaging.serialization import Serializer
    PYIPV8_AVAILABLE = True
except ImportError as e:
    PYIPV8_AVAILABLE = False
    print(f"Warning: py-ipv8 not installed or import failed: {e}", file=sys.stderr)

def generate_basic_serialization_vectors():
    """Generate test vectors for basic data type serialization."""
    vectors = {}

    # UInt32 (big-endian)
    vectors['uint32_examples'] = [
        {
            'value': 0x12345678,
            'format': '>I',
            'hex': hexlify(struct.pack('>I', 0x12345678)).decode('ascii'),
            'description': 'Circuit ID example'
        },
        {
            'value': 0xABCD1234,
            'format': '>I',
            'hex': hexlify(struct.pack('>I', 0xABCD1234)).decode('ascii'),
            'description': 'Circuit ID example 2'
        }
    ]

    # UInt16 (big-endian)
    vectors['uint16_examples'] = [
        {
            'value': 8080,
            'format': '>H',
            'hex': hexlify(struct.pack('>H', 8080)).decode('ascii'),
            'description': 'Port number example'
        },
        {
            'value': 0x1234,
            'format': '>H',
            'hex': hexlify(struct.pack('>H', 0x1234)).decode('ascii'),
            'description': 'Port number example 2'
        }
    ]

    # UInt64 (big-endian)
    vectors['uint64_examples'] = [
        {
            'value': 0x123456789ABCDEF0,
            'format': '>Q',
            'hex': hexlify(struct.pack('>Q', 0x123456789ABCDEF0)).decode('ascii'),
            'description': 'Timestamp example'
        }
    ]

    # Boolean
    vectors['boolean_examples'] = [
        {
            'value': True,
            'format': '?',
            'hex': hexlify(struct.pack('?', True)).decode('ascii'),
            'description': 'Boolean true'
        },
        {
            'value': False,
            'format': '?',
            'hex': hexlify(struct.pack('?', False)).decode('ascii'),
            'description': 'Boolean false'
        }
    ]

    return vectors

def generate_create_message_vector():
    """Generate CREATE message test vector using py-ipv8."""
    if not PYIPV8_AVAILABLE:
        return {
            'message_type': 'CREATE',
            'description': 'IPv8 circuit CREATE message',
            'error': 'py-ipv8 not installed',
            'note': 'Install with: pip install git+https://github.com/Tribler/py-ipv8.git'
        }

    # Create test data
    # NOTE: identifier is 16-bit (ushort), not 32-bit!
    circuit_id = 0x12345678
    identifier = 0xABCD  # 16-bit value
    node_public_key = bytes(range(32))  # 0x00, 0x01, 0x02, ..., 0x1F
    ephemeral_key = bytes(range(32, 64))  # 0x20, 0x21, 0x22, ..., 0x3F

    # Create payload
    payload = CreatePayload(circuit_id, identifier, node_public_key, ephemeral_key)

    # Serialize using py-ipv8's serializer
    serializer = Serializer()
    serialized = serializer.pack_serializable(payload)

    return {
        'message_type': 'CREATE',
        'description': 'IPv8 circuit CREATE message (format: I H varlenH varlenH)',
        'fields': {
            'circuit_id': f'0x{circuit_id:08X}',
            'identifier': f'0x{identifier:04X}',
            'node_public_key': hexlify(node_public_key).decode('ascii'),
            'ephemeral_key': hexlify(ephemeral_key).decode('ascii')
        },
        'hex': hexlify(serialized).decode('ascii'),
        'length': len(serialized),
        'note': 'identifier is 16-bit (ushort), not 32-bit (uint)'
    }

def generate_created_message_vector():
    """Generate CREATED message test vector using py-ipv8."""
    if not PYIPV8_AVAILABLE:
        return {
            'message_type': 'CREATED',
            'description': 'IPv8 circuit CREATED message',
            'error': 'py-ipv8 not installed'
        }

    circuit_id = 0x12345678
    identifier = 0xABCD  # 16-bit value
    ephemeral_key = bytes(range(32, 64))
    auth = b'test_auth_data'
    candidates_enc = b''  # Empty encoded candidate list

    payload = CreatedPayload(circuit_id, identifier, ephemeral_key, auth, candidates_enc)
    serializer = Serializer()
    serialized = serializer.pack_serializable(payload)

    return {
        'message_type': 'CREATED',
        'description': 'IPv8 circuit CREATED message',
        'fields': {
            'circuit_id': f'0x{circuit_id:08X}',
            'identifier': f'0x{identifier:04X}',
            'ephemeral_key': hexlify(ephemeral_key).decode('ascii'),
            'auth': hexlify(auth).decode('ascii'),
            'candidates_enc_length': len(candidates_enc)
        },
        'hex': hexlify(serialized).decode('ascii'),
        'length': len(serialized)
    }

def main():
    """Generate all test vectors and output as JSON."""
    test_vectors = {
        'version': '1.0',
        'generator': 'generate_ipv8_vectors.py',
        'description': 'IPv8 protocol test vectors for C# cross-language verification',
        'pyipv8_available': PYIPV8_AVAILABLE,
        'basic_serialization': generate_basic_serialization_vectors(),
        'messages': {
            'create_message': generate_create_message_vector(),
            'created_message': generate_created_message_vector()
        }
    }

    print(json.dumps(test_vectors, indent=2))

if __name__ == '__main__':
    main()

