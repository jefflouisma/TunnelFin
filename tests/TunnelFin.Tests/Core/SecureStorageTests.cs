using FluentAssertions;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Tests for SecureStorage (T092).
/// Tests secure key storage for Ed25519 private keys per FR-038.
/// </summary>
public class SecureStorageTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly byte[] _encryptionKey;
    private readonly SecureStorage _storage;

    public SecureStorageTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"tunnelfin_test_{Guid.NewGuid()}.dat");
        _encryptionKey = new byte[32];
        Random.Shared.NextBytes(_encryptionKey);
        _storage = new SecureStorage(_testStoragePath, _encryptionKey);
    }

    public void Dispose()
    {
        if (File.Exists(_testStoragePath))
        {
            File.Delete(_testStoragePath);
        }
    }

    [Fact]
    public void Constructor_Should_Throw_When_StoragePath_Empty()
    {
        // Act
        var act = () => new SecureStorage("", _encryptionKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Storage path cannot be empty*");
    }

    [Fact]
    public void Constructor_Should_Throw_When_EncryptionKey_Invalid()
    {
        // Arrange
        var invalidKey = new byte[16]; // Wrong size

        // Act
        var act = () => new SecureStorage(_testStoragePath, invalidKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Encryption key must be 32 bytes*");
    }

    [Fact]
    public void StorePrivateKey_Should_Store_32_Byte_Key()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);

        // Act
        _storage.StorePrivateKey(privateKey);

        // Assert
        File.Exists(_testStoragePath).Should().BeTrue();
    }

    [Fact]
    public void StorePrivateKey_Should_Throw_When_Key_Invalid()
    {
        // Arrange
        var invalidKey = new byte[16]; // Wrong size

        // Act
        var act = () => _storage.StorePrivateKey(invalidKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Private key must be 32 bytes*");
    }

    [Fact]
    public void RetrievePrivateKey_Should_Return_Stored_Key()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        _storage.StorePrivateKey(privateKey);

        // Act
        var retrieved = _storage.RetrievePrivateKey();

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(privateKey);
    }

    [Fact]
    public void RetrievePrivateKey_Should_Return_Null_When_No_Key_Stored()
    {
        // Act
        var retrieved = _storage.RetrievePrivateKey();

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void RetrievePrivateKey_Should_Return_Null_When_Decryption_Fails()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        _storage.StorePrivateKey(privateKey);

        // Create new storage with different key
        var wrongKey = new byte[32];
        Random.Shared.NextBytes(wrongKey);
        var wrongStorage = new SecureStorage(_testStoragePath, wrongKey);

        // Act
        var retrieved = wrongStorage.RetrievePrivateKey();

        // Assert
        retrieved.Should().BeNull("decryption should fail with wrong key");
    }

    [Fact]
    public void DeletePrivateKey_Should_Remove_Stored_Key()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        _storage.StorePrivateKey(privateKey);

        // Act
        _storage.DeletePrivateKey();

        // Assert
        File.Exists(_testStoragePath).Should().BeFalse();
        _storage.HasPrivateKey().Should().BeFalse();
    }

    [Fact]
    public void HasPrivateKey_Should_Return_True_When_Key_Stored()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        _storage.StorePrivateKey(privateKey);

        // Act
        var hasKey = _storage.HasPrivateKey();

        // Assert
        hasKey.Should().BeTrue();
    }

    [Fact]
    public void HasPrivateKey_Should_Return_False_When_No_Key_Stored()
    {
        // Act
        var hasKey = _storage.HasPrivateKey();

        // Assert
        hasKey.Should().BeFalse();
    }

    [Fact]
    public void StorePrivateKey_Should_Overwrite_Existing_Key()
    {
        // Arrange
        var key1 = new byte[32];
        var key2 = new byte[32];
        Random.Shared.NextBytes(key1);
        Random.Shared.NextBytes(key2);

        // Act
        _storage.StorePrivateKey(key1);
        _storage.StorePrivateKey(key2);
        var retrieved = _storage.RetrievePrivateKey();

        // Assert
        retrieved.Should().BeEquivalentTo(key2, "second key should overwrite first");
    }

    [Fact]
    public void Constructor_Should_Throw_When_StoragePath_Is_Null()
    {
        // Act
        var act = () => new SecureStorage(null!, _encryptionKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("storagePath");
    }

    [Fact]
    public void Constructor_Should_Throw_When_EncryptionKey_Is_Null()
    {
        // Act
        var act = () => new SecureStorage(_testStoragePath, null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("encryptionKey");
    }

    [Fact]
    public void StorePrivateKey_Should_Throw_When_PrivateKey_Is_Null()
    {
        // Act
        var act = () => _storage.StorePrivateKey(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("privateKey");
    }

    [Fact]
    public void DeletePrivateKey_Should_Not_Throw_When_No_Key_Exists()
    {
        // Act
        var act = () => _storage.DeletePrivateKey();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_Should_Create_Directory_If_Not_Exists()
    {
        // Arrange
        var nestedPath = Path.Combine(Path.GetTempPath(), $"tunnelfin_test_{Guid.NewGuid()}", "nested", "storage.dat");

        // Act
        var storage = new SecureStorage(nestedPath, _encryptionKey);

        // Assert
        var directory = Path.GetDirectoryName(nestedPath);
        Directory.Exists(directory).Should().BeTrue();

        // Cleanup
        if (directory != null && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RetrievePrivateKey_Should_Return_Null_When_File_Is_Corrupted()
    {
        // Arrange
        var privateKey = new byte[32];
        Random.Shared.NextBytes(privateKey);
        _storage.StorePrivateKey(privateKey);

        // Corrupt the file
        File.WriteAllBytes(_testStoragePath, new byte[] { 1, 2, 3 });

        // Act
        var retrieved = _storage.RetrievePrivateKey();

        // Assert
        retrieved.Should().BeNull("corrupted file should return null");
    }
}

