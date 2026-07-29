using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class MessagePublicKeyDiscoveryTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("default"));

    [Fact]
    public async Task Discovery_returns_public_key_from_matching_transaction_signature()
    {
        (byte[] bobPublic, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        Address bob = Address.FromPublicKey(bobPublic);
        var client = new RecordingReadClient([
            CreateSummary(bob, bobPublic)
        ]);
        var discovery = new LedgerTransactionPublicKeyDiscovery(client);

        LedgerPublicKeyDiscoveryResult? result = await discovery.FindPublicKeyAsync(Context, bob, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(bob, result.Address);
        Assert.Equal(bobPublic, result.PublicKeyMaterial);
        Assert.Equal(new TxId("tx-match"), result.TxId);
        Assert.Equal(bob, client.LastQuery?.Address);
        Assert.False(client.LastQuery?.IncludeData ?? true);
    }

    [Fact]
    public async Task Discovery_rejects_signature_public_key_that_does_not_match_requested_address()
    {
        (byte[] alicePublic, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        (byte[] bobPublic, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        Address bob = Address.FromPublicKey(bobPublic);
        var client = new RecordingReadClient([
            CreateSummary(bob, alicePublic)
        ]);
        var discovery = new LedgerTransactionPublicKeyDiscovery(client);

        LedgerPublicKeyDiscoveryResult? result = await discovery.FindPublicKeyAsync(Context, bob, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Discovery_ignores_output_only_transactions_without_receiver_signature()
    {
        (byte[] alicePublic, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        (byte[] bobPublic, _) = EcdsaP256KeyMaterial.CreateKeyPair();
        Address alice = Address.FromPublicKey(alicePublic);
        Address bob = Address.FromPublicKey(bobPublic);
        var client = new RecordingReadClient([
            CreateSummary(alice, alicePublic, outputAddress: bob)
        ]);
        var discovery = new LedgerTransactionPublicKeyDiscovery(client);

        LedgerPublicKeyDiscoveryResult? result = await discovery.FindPublicKeyAsync(Context, bob, CancellationToken.None);

        Assert.Null(result);
    }

    private static LedgerTransactionSummary CreateSummary(
        Address inputAddress,
        byte[] publicKeyMaterial,
        Address? outputAddress = null)
    {
        var signature = new SignatureEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "key",
            Address.CalculatePublicKeyHash(publicKeyMaterial),
            new HashHex(new string('a', 64)),
            [1, 2, 3],
            publicKeyMaterial);

        var input = new LedgerTransactionInputSummary(
            0,
            new OutPoint(new TxId("previous"), 0),
            inputAddress,
            signatureBundle: new SignatureBundle([signature]));

        return new LedgerTransactionSummary(
            Context,
            new TxId("tx-match"),
            TransactionKind.Transfer,
            TransactionStatus.Confirmed,
            DateTimeOffset.Parse("2026-07-02T12:00:00Z"),
            [inputAddress],
            [outputAddress ?? inputAddress],
            [input],
            [
                new LedgerTransactionOutputSummary(
                    0,
                    outputAddress ?? inputAddress,
                    Amount.FromInt64(1))
            ]);
    }

    private sealed class RecordingReadClient(IReadOnlyList<LedgerTransactionSummary> transactions) : ILedgerReadClient
    {
        public LedgerTransactionQuery? LastQuery { get; private set; }

        public Task<LedgerAccountBalance> GetBalanceAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerBusinessObjectHolding>> GetBusinessObjectsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerSpendableOutput>> GetSpendableOutputsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerTransactionSummary>> GetTransactionsAsync(LedgerAddressQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerTransactionSummary>> QueryTransactionsAsync(LedgerTransactionQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(transactions);
        }
    }
}
