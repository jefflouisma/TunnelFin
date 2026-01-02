#!/usr/bin/env python3
"""
Generate Ed25519 test vectors for C# cross-language verification.
Produces keypairs and signatures using PyNaCl for C# cross-validation (FR-049).

Requirements:
    pip install pynacl

Usage:
    python generate_ed25519_vectors.py > ed25519_test_vectors.json
"""

import json
from binascii import hexlify, unhexlify

try:
    from nacl.signing import SigningKey
    from nacl.encoding import RawEncoder
    PYNACL_AVAILABLE = True
except ImportError:
    PYNACL_AVAILABLE = False
    print("WARNING: PyNaCl not installed. Install with: pip install pynacl", file=__import__('sys').stderr)

def generate_keypair_from_seed(seed_hex):
    """Generate Ed25519 keypair from a 32-byte seed."""
    if not PYNACL_AVAILABLE:
        return {
            'error': 'PyNaCl not installed',
            'seed_hex': seed_hex,
            'private_key_seed_hex': 'INSTALL_PYNACL',
            'public_key_hex': 'INSTALL_PYNACL'
        }
    
    seed_bytes = unhexlify(seed_hex)
    signing_key = SigningKey(seed_bytes)
    
    # Get the 32-byte seed (compatible with NSec RawPrivateKey)
    private_seed = signing_key.encode(encoder=RawEncoder)
    
    # Get the 32-byte public key
    public_key = signing_key.verify_key.encode(encoder=RawEncoder)
    
    return {
        'seed_hex': seed_hex,
        'private_key_seed_hex': hexlify(private_seed).decode('ascii'),
        'public_key_hex': hexlify(public_key).decode('ascii'),
        'description': 'Ed25519 keypair from seed'
    }

def generate_signature(seed_hex, message):
    """Generate Ed25519 signature for a message."""
    if not PYNACL_AVAILABLE:
        return {
            'error': 'PyNaCl not installed',
            'seed_hex': seed_hex,
            'message': message,
            'signature_hex': 'INSTALL_PYNACL'
        }
    
    seed_bytes = unhexlify(seed_hex)
    signing_key = SigningKey(seed_bytes)
    
    message_bytes = message.encode('utf-8')
    signed = signing_key.sign(message_bytes, encoder=RawEncoder)
    
    # Extract just the signature (first 64 bytes of signed message)
    signature = signed[:64]
    
    return {
        'seed_hex': seed_hex,
        'message': message,
        'message_hex': hexlify(message_bytes).decode('ascii'),
        'signature_hex': hexlify(signature).decode('ascii'),
        'signature_length': len(signature),
        'description': 'Ed25519 signature (64 bytes: R || S)'
    }

def generate_test_vectors():
    """Generate comprehensive Ed25519 test vectors."""
    
    # Test seeds
    test_seeds = [
        '0000000000000000000000000000000000000000000000000000000000000000',
        '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef',
        'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff',
        '1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
        'deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef'
    ]
    
    # Test messages
    test_messages = [
        '',
        'Hello, World!',
        'The quick brown fox jumps over the lazy dog',
        'TunnelFin - Privacy-first torrent streaming',
        'A' * 1000  # Large message
    ]
    
    vectors = {
        'version': '1.0',
        'generator': 'generate_ed25519_vectors.py',
        'description': 'Ed25519 test vectors for C# cross-language verification',
        'pynacl_available': PYNACL_AVAILABLE,
        'keypairs': [],
        'signatures': []
    }
    
    # Generate keypairs
    for seed in test_seeds:
        vectors['keypairs'].append(generate_keypair_from_seed(seed))
    
    # Generate signatures
    for seed in test_seeds[:3]:  # Use first 3 seeds
        for message in test_messages[:4]:  # Use first 4 messages
            vectors['signatures'].append(generate_signature(seed, message))
    
    return vectors

def main():
    """Generate all test vectors and output as JSON."""
    test_vectors = generate_test_vectors()
    print(json.dumps(test_vectors, indent=2))

if __name__ == '__main__':
    main()

