using System.Text.Json;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;
using CanDoItAll.Economy.Ledger;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class LedgerVerificationGoldenVectorTests
{
    private static readonly LedgerId LedgerId = new("default");
    private static readonly Address Alice = Address.FromPublicKey(Hex.Utf8("alice-public"));
    private static readonly Address Bob = Address.FromPublicKey(Hex.Utf8("bob-public"));
    private static readonly HashHex ZeroHash = new(new string('0', 64));

    [Fact]
    public void CanonicalHashGoldenVectors_MatchRuntime()
    {
        GoldenVectorDocument vector = LoadVector();
        var hasher = CreateHasher();
        var payloadBuilder = new SignaturePayloadBuilder(hasher);
        LedgerTransaction genesis = CreateGoldenGenesis(vector);
        TxId genesisTxId = hasher.CalculateTxId(genesis);
        LedgerTransaction confirmedGenesisTransaction = genesis with
        {
            TxId = genesisTxId,
            TxHash = hasher.CalculateTxHash(genesis)
        };
        var confirmedGenesis = new ConfirmedTransaction(
            confirmedGenesisTransaction,
            vector.Genesis.LedgerSequence,
            new HashHex(vector.Genesis.PreviousLedgerHash),
            new HashHex(vector.Genesis.LedgerEntryHash),
            SignatureBundle.Empty,
            DateTimeOffset.Parse(vector.Genesis.ConfirmedAt));

        Assert.Equal("cdia.economy.golden-vectors.transaction-hashes.v1", vector.Schema);
        Assert.Equal(Alice.Value, vector.Actors.Alice.Address);
        Assert.Equal(Bob.Value, vector.Actors.Bob.Address);
        Assert.Equal(vector.Genesis.TxId, genesisTxId.Value);
        Assert.Equal(vector.Genesis.TxHash, confirmedGenesisTransaction.TxHash!.Value.Value);
        Assert.Equal(
            vector.Genesis.OutPointKey0,
            hasher.CalculateOutPointKey(LedgerId, null, new OutPoint(genesisTxId, 0), Alice).Value.Value);
        Assert.Equal(
            vector.Genesis.LedgerEntryHash,
            payloadBuilder.BuildLedgerEntryPayloadHash(confirmedGenesis).Value);

        LedgerTransaction transfer = CreateGoldenTransfer(vector, genesisTxId);
        TxId transferTxId = hasher.CalculateTxId(transfer);
        HashHex transferPayloadHash = payloadBuilder.BuildIndependentInputPayloadHash(transfer with { TxId = transferTxId }, transfer.Inputs[0]);
        LedgerTransaction signedTransfer = SignGoldenTransfer(transfer, transferTxId, transferPayloadHash);
        LedgerTransaction confirmedTransferTransaction = signedTransfer with
        {
            TxHash = hasher.CalculateTxHash(signedTransfer)
        };
        var confirmedTransfer = new ConfirmedTransaction(
            confirmedTransferTransaction,
            vector.Transfer.LedgerSequence,
            new HashHex(vector.Transfer.PreviousLedgerHash),
            new HashHex(vector.Transfer.LedgerEntryHash),
            SignatureBundle.Empty,
            DateTimeOffset.Parse(vector.Transfer.ConfirmedAt));

        Assert.Equal(vector.Transfer.TxId, transferTxId.Value);
        Assert.Equal(vector.Transfer.IndependentInputPayloadHash0, transferPayloadHash.Value);
        Assert.Equal(vector.Transfer.TxHash, confirmedTransferTransaction.TxHash!.Value.Value);
        Assert.Equal(
            vector.Transfer.OutPointKey0,
            hasher.CalculateOutPointKey(LedgerId, null, new OutPoint(transferTxId, 0), Bob).Value.Value);
        Assert.Equal(
            vector.Transfer.LedgerEntryHash,
            payloadBuilder.BuildLedgerEntryPayloadHash(confirmedTransfer).Value);
    }

    [Fact]
    public void LedgerChainVerifier_AcceptsCanonicalChain()
    {
        ConfirmedTransaction[] chain = CreateConfirmedChain();
        var verifier = new LedgerChainVerifier(CreateHasher(), new SignaturePayloadBuilder(CreateHasher()));

        LedgerChainVerificationReport report = verifier.Verify(chain);

        Assert.True(report.IsValid, string.Join("; ", report.Issues.Select(x => x.Message)));
        Assert.Equal(2, report.TransactionCount);
        Assert.Equal(chain[1].LedgerEntryHash, report.VerifiedLedgerHash);
    }

    [Fact]
    public void LedgerChainVerifier_DetectsCanonicalChainMismatches()
    {
        ConfirmedTransaction[] chain = CreateConfirmedChain();
        var verifier = new LedgerChainVerifier(CreateHasher(), new SignaturePayloadBuilder(CreateHasher()));
        HashHex wrongHash = Hex.Sha256Hex("wrong"u8.ToArray());

        IReadOnlyList<(ConfirmedTransaction[] Chain, LedgerChainVerificationIssueCode Code)> cases =
        [
            (Replace(chain, 1, chain[1] with { LedgerSequence = 3 }), LedgerChainVerificationIssueCode.SequenceMismatch),
            (Replace(chain, 1, chain[1] with { PreviousLedgerHash = wrongHash }), LedgerChainVerificationIssueCode.PreviousLedgerHashMismatch),
            (Replace(chain, 1, chain[1] with { Transaction = chain[1].Transaction with { TxId = new TxId("stale-client-id") } }), LedgerChainVerificationIssueCode.TxIdMismatch),
            (Replace(chain, 1, chain[1] with { Transaction = chain[1].Transaction with { TxHash = wrongHash } }), LedgerChainVerificationIssueCode.TxHashMismatch),
            (Replace(chain, 1, chain[1] with { LedgerEntryHash = wrongHash }), LedgerChainVerificationIssueCode.LedgerEntryHashMismatch),
            (Replace(chain, 1, chain[1] with { AdminSignature = SignatureBundle.Empty }), LedgerChainVerificationIssueCode.MissingAdminSignature)
        ];

        foreach ((ConfirmedTransaction[] mutatedChain, LedgerChainVerificationIssueCode expectedCode) in cases)
        {
            LedgerChainVerificationReport report = verifier.Verify(mutatedChain);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, x => x.Code == expectedCode);
        }
    }

    [Fact]
    public void LedgerChainVerifier_VerifiesTrustedAdminSignaturesCryptographically()
    {
        ConfirmedTransaction[] chain = CreateConfirmedChain();
        (byte[] publicKey, byte[] privateKey) = EcdsaP256KeyMaterial.CreateKeyPair();
        HashHex publicKeyHash = Address.CalculatePublicKeyHash(publicKey);
        ConfirmedTransaction[] signedChain = chain
            .Select(confirmed => confirmed with
            {
                AdminSignature = SignAdmin(
                    confirmed.LedgerEntryHash,
                    "trusted-ledger-admin",
                    publicKey,
                    privateKey)
            })
            .ToArray();
        var trustedKey = new LedgerAdminVerificationKey(
            "trusted-ledger-admin",
            SignatureAlgorithmId.EcdsaP256Sha256,
            publicKeyHash,
            publicKey);
        var registry = new SignatureProviderRegistry(new ISignatureProvider[] { new EcdsaP256SignatureProvider() });
        var verifier = new LedgerChainVerifier(
            CreateHasher(),
            new SignaturePayloadBuilder(CreateHasher()),
            registry,
            new[] { trustedKey });

        LedgerChainVerificationReport valid = verifier.Verify(signedChain);
        Assert.True(valid.IsValid, string.Join("; ", valid.Issues.Select(x => x.Message)));

        SignatureEnvelope originalSignature = signedChain[1].AdminSignature.Signatures[0];
        ConfirmedTransaction[] tamperedSignature = Replace(
            signedChain,
            1,
            signedChain[1] with
            {
                AdminSignature = new SignatureBundle(new[]
                {
                    originalSignature with { SignatureBytes = originalSignature.SignatureBytes.Reverse().ToArray() }
                })
            });
        LedgerChainVerificationReport tampered = verifier.Verify(tamperedSignature);
        Assert.False(tampered.IsValid);
        Assert.Contains(tampered.Issues, x => x.Code == LedgerChainVerificationIssueCode.AdminSignatureCryptographicVerificationFailed);

        (byte[] otherPublicKey, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        var untrustedVerifier = new LedgerChainVerifier(
            CreateHasher(),
            new SignaturePayloadBuilder(CreateHasher()),
            registry,
            new[]
            {
                new LedgerAdminVerificationKey(
                    "other-admin",
                    SignatureAlgorithmId.EcdsaP256Sha256,
                    Address.CalculatePublicKeyHash(otherPublicKey),
                    otherPublicKey)
            });
        LedgerChainVerificationReport untrusted = untrustedVerifier.Verify(signedChain);
        Assert.False(untrusted.IsValid);
        Assert.Contains(untrusted.Issues, x => x.Code == LedgerChainVerificationIssueCode.UntrustedAdminSignature);
    }

    private static ConfirmedTransaction[] CreateConfirmedChain()
    {
        GoldenVectorDocument vector = LoadVector();
        var hasher = CreateHasher();
        var payloadBuilder = new SignaturePayloadBuilder(hasher);

        LedgerTransaction genesis = CreateGoldenGenesis(vector);
        LedgerTransaction normalizedGenesis = genesis with
        {
            TxId = hasher.CalculateTxId(genesis),
            TxHash = hasher.CalculateTxHash(genesis)
        };
        var temporaryGenesis = new ConfirmedTransaction(
            normalizedGenesis,
            1,
            ZeroHash,
            ZeroHash,
            SignatureBundle.Empty,
            DateTimeOffset.Parse(vector.Genesis.ConfirmedAt));
        HashHex genesisEntryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporaryGenesis);
        ConfirmedTransaction confirmedGenesis = temporaryGenesis with
        {
            LedgerEntryHash = genesisEntryHash,
            AdminSignature = SignAdmin(genesisEntryHash)
        };

        LedgerTransaction transfer = CreateGoldenTransfer(vector, normalizedGenesis.TxId!.Value);
        HashHex payloadHash = payloadBuilder.BuildIndependentInputPayloadHash(transfer with { TxId = hasher.CalculateTxId(transfer) }, transfer.Inputs[0]);
        LedgerTransaction signedTransfer = SignGoldenTransfer(transfer, hasher.CalculateTxId(transfer), payloadHash);
        LedgerTransaction normalizedTransfer = signedTransfer with
        {
            TxHash = hasher.CalculateTxHash(signedTransfer)
        };
        var temporaryTransfer = new ConfirmedTransaction(
            normalizedTransfer,
            2,
            genesisEntryHash,
            ZeroHash,
            SignatureBundle.Empty,
            DateTimeOffset.Parse(vector.Transfer.ConfirmedAt));
        HashHex transferEntryHash = payloadBuilder.BuildLedgerEntryPayloadHash(temporaryTransfer);
        ConfirmedTransaction confirmedTransfer = temporaryTransfer with
        {
            LedgerEntryHash = transferEntryHash,
            AdminSignature = SignAdmin(transferEntryHash)
        };

        return [confirmedGenesis, confirmedTransfer];
    }

    private static LedgerTransaction CreateGoldenGenesis(GoldenVectorDocument vector)
        => new(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            DateTimeOffset.Parse(vector.Genesis.Timestamp),
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, Alice, Amount.FromInt64(100))
            },
            new TransactionData(
                "application/json",
                new HashHex(vector.Genesis.DataHash ?? throw new InvalidOperationException("Genesis vector must include a data hash.")),
                "{\"memo\":\"golden\"}",
                null));

    private static LedgerTransaction CreateGoldenTransfer(GoldenVectorDocument vector, TxId genesisTxId)
        => new(
            LedgerId,
            null,
            Version: 1,
            TransactionKind.Transfer,
            SigningMode.Independent,
            DateTimeOffset.Parse(vector.Transfer.Timestamp),
            new[] { new TransactionInput(0, new OutPoint(genesisTxId, 0), Alice, SignatureBundle.Empty) },
            new[]
            {
                new TransactionOutput(0, Bob, Amount.FromInt64(60)),
                new TransactionOutput(1, Alice, Amount.FromInt64(40))
            },
            null);

    private static LedgerTransaction SignGoldenTransfer(LedgerTransaction transaction, TxId txId, HashHex payloadHash)
    {
        byte[] publicKey = Hex.Utf8("alice-public");
        return transaction with
        {
            TxId = txId,
            Inputs = new[]
            {
                transaction.Inputs[0] with
                {
                    SignaturePayloadHash = payloadHash,
                    SignatureBundle = new SignatureBundle(new[]
                    {
                        new SignatureEnvelope(
                            SignatureAlgorithmId.EcdsaP256Sha256,
                            "alice",
                            Address.CalculatePublicKeyHash(publicKey),
                            payloadHash,
                            Hex.Utf8("alice-signature"),
                            publicKey)
                    })
                }
            }
        };
    }

    private static SignatureBundle SignAdmin(HashHex payloadHash)
        => new(new[]
        {
            new SignatureEnvelope(
                SignatureAlgorithmId.VefSecp256K1Sha256,
                "golden-admin",
                Hex.Sha256Hex("admin-public"u8.ToArray()),
                payloadHash,
                Hex.Utf8("admin-signature"))
        });

    private static SignatureBundle SignAdmin(
        HashHex payloadHash,
        string keyId,
        byte[] publicKey,
        byte[] privateKey)
    {
        var provider = new EcdsaP256SignatureProvider();
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(payloadHash.Value);
        return new SignatureBundle(new[]
        {
            new SignatureEnvelope(
                SignatureAlgorithmId.EcdsaP256Sha256,
                keyId,
                Address.CalculatePublicKeyHash(publicKey),
                payloadHash,
                provider.Sign(payload, privateKey),
                publicKey)
        });
    }

    private static ITransactionHasher CreateHasher()
        => new TransactionHasher(new DefaultTransactionCanonicalizer());

    private static ConfirmedTransaction[] Replace(ConfirmedTransaction[] source, int index, ConfirmedTransaction replacement)
    {
        ConfirmedTransaction[] copy = source.ToArray();
        copy[index] = replacement;
        return copy;
    }

    private static GoldenVectorDocument LoadVector()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, "docs", "golden-vectors", "transaction-hashes-v1.json");
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GoldenVectorDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Golden vector document could not be loaded.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.Ledger.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record GoldenVectorDocument(
        string Schema,
        string LedgerId,
        string? ScenarioId,
        GoldenVectorActors Actors,
        GoldenVectorTransaction Genesis,
        GoldenVectorTransaction Transfer);

    private sealed record GoldenVectorActors(
        GoldenVectorActor Alice,
        GoldenVectorActor Bob);

    private sealed record GoldenVectorActor(
        string PublicKeyUtf8,
        string Address,
        string PublicKeyHash);

    private sealed record GoldenVectorTransaction(
        string Timestamp,
        string ConfirmedAt,
        string TxId,
        string TxHash,
        string OutPointKey0,
        string PreviousLedgerHash,
        long LedgerSequence,
        string LedgerEntryHash,
        string? DataHash,
        string? IndependentInputPayloadHash0);
}
