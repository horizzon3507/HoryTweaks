using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HoryTweaks.Networking.Handshake;

/// <summary>
/// Handles secure key exchange using Elliptic Curve Diffie-Hellman (ECDH) for establishing shared secrets.
/// Probes ECDH support once per process so Wine/Proton environments without NIST P-256 CNG support
/// do not repeatedly throw exceptions on every player join.
/// </summary>
internal sealed class SharedSecretExchange
{
    private static readonly object EcdhProbeLock = new();
    private static bool? _ecdhSupported;

    private ECDiffieHellman? _dh;
    private byte[] _publicKey;
    private int _tempKey;
    private byte[] _sharedSecret = [];
    private readonly bool _cryptoDisabled;
    private int? _remoteTempKey;

    /// <summary>
    /// Gets a value indicating whether cryptographic operations are available.
    /// </summary>
    internal bool CryptoAvailable => !_cryptoDisabled;

    /// <summary>
    /// Gets or sets a value indicating whether to use the fallback key exchange mechanism.
    /// </summary>
    internal bool UseFallback { get; set; }

    /// <summary>
    /// Gets a value indicating whether the key exchange data has been cleared.
    /// </summary>
    internal bool HasBeenCleared { get; private set; }

    /// <summary>
    /// Initializes a new instance of the SharedSecretExchange class with a new ECDH key pair when supported.
    /// </summary>
    internal SharedSecretExchange()
    {
        if (IsEcdhSupported())
        {
            try
            {
                _dh = ECDiffieHellman.Create();
                _dh.GenerateKey(ECCurve.NamedCurves.nistP256);
                _publicKey = _dh.ExportSubjectPublicKeyInfo();
            }
            catch (Exception ex)
            {
                MarkEcdhUnsupported(ex);
                _cryptoDisabled = true;
                DisposeDh();
                _publicKey = [];
            }
        }
        else
        {
            _cryptoDisabled = true;
            _publicKey = [];
        }

        _tempKey = GenerateSecureTempKey();
    }

    private static bool IsEcdhSupported()
    {
        if (_ecdhSupported.HasValue)
            return _ecdhSupported.Value;

        lock (EcdhProbeLock)
        {
            if (_ecdhSupported.HasValue)
                return _ecdhSupported.Value;

            try
            {
                using var probe = ECDiffieHellman.Create();
                probe.GenerateKey(ECCurve.NamedCurves.nistP256);
                _ = probe.ExportSubjectPublicKeyInfo();
                _ecdhSupported = true;
            }
            catch (Exception ex)
            {
                _ecdhSupported = false;
                BAUPlugin.Logger.Warning("ECDH unavailable on this runtime; using fallback handshake for all peers: " + ex.Message, "Handshake");
            }

            return _ecdhSupported.Value;
        }
    }

    private static void MarkEcdhUnsupported(Exception ex)
    {
        lock (EcdhProbeLock)
        {
            if (_ecdhSupported != false)
            {
                _ecdhSupported = false;
                BAUPlugin.Logger.Warning("ECDH failed; switching to fallback handshake for future peers: " + ex.Message, "Handshake");
            }
        }
    }

    private static int GenerateSecureTempKey()
    {
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer) & int.MaxValue;
    }

    internal void SetRemoteTempKey(int key)
    {
        _remoteTempKey = key;
    }

    internal byte[] GetPublicKey()
    {
        return _cryptoDisabled ? [] : _publicKey;
    }

    internal int GetTempKey()
    {
        return _tempKey;
    }

    internal byte[] GetSharedSecret()
    {
        return _sharedSecret;
    }

    internal byte[] GenerateSharedSecret(byte[] otherPartyPublicKey)
    {
        if (_sharedSecret.Length > 0)
            return _sharedSecret;

        if (_cryptoDisabled || UseFallback || _dh == null)
        {
            if (!_remoteTempKey.HasValue)
                return [];

            _sharedSecret = DeriveFallbackSecret(_tempKey, _remoteTempKey.Value);
            return _sharedSecret;
        }

        try
        {
            if (otherPartyPublicKey == null || otherPartyPublicKey.Length == 0)
                return [];

            using var otherPartyDH = ECDiffieHellman.Create();
            otherPartyDH.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);
            _sharedSecret = _dh.DeriveKeyMaterial(otherPartyDH.PublicKey);

            DisposeDh();
            return _sharedSecret;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error("Error generating shared secret: " + ex.Message, logConsole: false);
            return [];
        }
    }

    internal int GetSharedSecretHash()
    {
        if (_sharedSecret.Length == 0)
            return 0;

        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(_sharedSecret, hashBytes);

        int numericHash = (hashBytes[0] << 24) |
                          (hashBytes[1] << 16) |
                          (hashBytes[2] << 8) |
                          hashBytes[3];

        return numericHash == int.MinValue ? int.MaxValue : Math.Abs(numericHash);
    }

    private static byte[] DeriveFallbackSecret(int local, int remote)
    {
        int a = local < remote ? local : remote;
        int b = local < remote ? remote : local;

        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, a);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], b);

        return SHA256.HashData(buffer);
    }

    internal void ClearData()
    {
        if (HasBeenCleared)
            return;

        HasBeenCleared = true;
        DisposeDh();
        _publicKey = [];
        _tempKey = 0;
        _sharedSecret = [];
    }

    private void DisposeDh()
    {
        var dh = _dh;
        _dh = null;
        dh?.Dispose();
    }
}
