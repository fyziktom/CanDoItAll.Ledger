using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Persistence.EFCore;

public sealed class EfLedgerSnapshotService(
    IDbContextFactory<LedgerDbContext> dbContextFactory,
    EfLedgerProjectionVerifier projectionVerifier,
    ILedgerAdminSigner? adminSigner = null,
    ISignatureProviderRegistry? signatureProviderRegistry = null)
{
    public async Task<LedgerSnapshot> CreateSnapshotAsync(
        LedgerId ledgerId,
        ScenarioId? scenarioId,
        CancellationToken cancellationToken)
    {
        if (adminSigner is null)
        {
            throw new InvalidOperationException("Snapshot creation requires an admin signer.");
        }

        EfLedgerProjectionVerificationReport projection = await projectionVerifier
            .VerifyAsync(ledgerId, scenarioId, cancellationToken)
            .ConfigureAwait(false);
        if (!projection.IsValid)
        {
            throw new InvalidOperationException("Cannot create a snapshot while projections are invalid: "
                + string.Join("; ", projection.Issues.Select(x => x.Message)));
        }

        DateTimeOffset createdAt = TruncateToMicroseconds(DateTimeOffset.UtcNow);
        string snapshotId = CreateSnapshotId(projection.HashSet);
        HashHex payloadHash = BuildSnapshotPayloadHash(
            snapshotId,
            projection.HashSet,
            createdAt);
        SignatureBundle adminSignature = adminSigner.Sign(payloadHash);
        var snapshot = new LedgerSnapshot(
            snapshotId,
            ledgerId,
            scenarioId,
            projection.HashSet.CurrentSequence,
            projection.HashSet.CurrentLedgerHash,
            projection.HashSet.UtxoSetHash,
            projection.HashSet.AddressStateHash,
            projection.HashSet.BusinessObjectStateHash,
            createdAt,
            adminSignature);

        await using LedgerDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        string? scenarioValue = scenarioId?.Value;
        LedgerSnapshotRecord? existing = await db.LedgerSnapshots
            .FirstOrDefaultAsync(
                x => x.LedgerId == ledgerId.Value
                    && x.ScenarioId == scenarioValue
                    && x.LedgerSequence == snapshot.LedgerSequence,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            db.LedgerSnapshots.Remove(existing);
        }

        db.LedgerSnapshots.Add(ToRecord(snapshot));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    public async Task<EfLedgerSnapshotVerificationReport> VerifySnapshotAsync(
        string snapshotId,
        CancellationToken cancellationToken)
    {
        await using LedgerDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        LedgerSnapshotRecord? row = await db.LedgerSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SnapshotId == snapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return new EfLedgerSnapshotVerificationReport(
                false,
                null,
                null,
                [new EfLedgerSnapshotVerificationIssue(EfLedgerSnapshotVerificationIssueCode.SnapshotMissing, snapshotId, "Snapshot does not exist.")]);
        }

        LedgerSnapshot snapshot = ToSnapshot(row);
        EfLedgerProjectionHashSet current = await projectionVerifier
            .CalculateHashSetAsync(snapshot.LedgerId, snapshot.ScenarioId, cancellationToken)
            .ConfigureAwait(false);
        var issues = new List<EfLedgerSnapshotVerificationIssue>();

        CompareHash("ledger-sequence", snapshot.LedgerSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), current.CurrentSequence.ToString(System.Globalization.CultureInfo.InvariantCulture), EfLedgerSnapshotVerificationIssueCode.SnapshotSequenceMismatch, issues);
        CompareHash("ledger-hash", snapshot.LedgerHash.Value, current.CurrentLedgerHash.Value, EfLedgerSnapshotVerificationIssueCode.SnapshotLedgerHashMismatch, issues);
        CompareHash("utxo-set", snapshot.UtxoSetHash.Value, current.UtxoSetHash.Value, EfLedgerSnapshotVerificationIssueCode.SnapshotProjectionHashMismatch, issues);
        CompareHash("address-state", snapshot.AddressStateHash.Value, current.AddressStateHash.Value, EfLedgerSnapshotVerificationIssueCode.SnapshotProjectionHashMismatch, issues);
        CompareHash("business-object-state", snapshot.BusinessObjectStateHash.Value, current.BusinessObjectStateHash.Value, EfLedgerSnapshotVerificationIssueCode.SnapshotProjectionHashMismatch, issues);
        VerifyAdminSignature(snapshot, issues);

        return new EfLedgerSnapshotVerificationReport(
            issues.Count == 0,
            snapshot,
            current,
            issues);
    }

    public static HashHex BuildSnapshotPayloadHash(
        string snapshotId,
        EfLedgerProjectionHashSet hashSet,
        DateTimeOffset createdAt)
    {
        string value = string.Join('|',
            "cdia.economy.snapshot.v1",
            snapshotId,
            hashSet.LedgerId.Value,
            hashSet.ScenarioId?.Value ?? string.Empty,
            hashSet.CurrentSequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hashSet.CurrentLedgerHash.Value,
            hashSet.UtxoSetHash.Value,
            hashSet.AddressStateHash.Value,
            hashSet.BusinessObjectStateHash.Value,
            createdAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new HashHex(Convert.ToHexString(hash).ToLowerInvariant());
    }

    private void VerifyAdminSignature(
        LedgerSnapshot snapshot,
        List<EfLedgerSnapshotVerificationIssue> issues)
    {
        HashHex payloadHash = BuildSnapshotPayloadHash(
            snapshot.SnapshotId,
            new EfLedgerProjectionHashSet(
                snapshot.LedgerId,
                snapshot.ScenarioId,
                snapshot.LedgerSequence,
                snapshot.LedgerHash,
                snapshot.UtxoSetHash,
                new HashHex(new string('0', 64)),
                snapshot.AddressStateHash,
                new HashHex(new string('0', 64)),
                snapshot.BusinessObjectStateHash,
                new HashHex(new string('0', 64)),
                new HashHex(new string('0', 64))),
            snapshot.CreatedAt);

        if (snapshot.AdminSignature.Signatures.Count == 0)
        {
            issues.Add(new EfLedgerSnapshotVerificationIssue(
                EfLedgerSnapshotVerificationIssueCode.SnapshotSignatureMissing,
                snapshot.SnapshotId,
                "Snapshot is missing an admin signature."));
            return;
        }

        foreach (SignatureEnvelope signature in snapshot.AdminSignature.Signatures)
        {
            if (signature.PayloadHash != payloadHash)
            {
                issues.Add(new EfLedgerSnapshotVerificationIssue(
                    EfLedgerSnapshotVerificationIssueCode.SnapshotSignaturePayloadMismatch,
                    snapshot.SnapshotId,
                    $"Snapshot signature payload hash {signature.PayloadHash.Value} does not match expected {payloadHash.Value}."));
                continue;
            }

            if (signature.PublicKeyMaterial is not { Length: > 0 } publicKeyMaterial)
            {
                issues.Add(new EfLedgerSnapshotVerificationIssue(
                    EfLedgerSnapshotVerificationIssueCode.SnapshotSignatureVerificationUnavailable,
                    snapshot.SnapshotId,
                    "Snapshot signature does not include public key material."));
                continue;
            }

            if (Address.CalculatePublicKeyHash(publicKeyMaterial) != signature.PublicKeyHash)
            {
                issues.Add(new EfLedgerSnapshotVerificationIssue(
                    EfLedgerSnapshotVerificationIssueCode.SnapshotSignaturePublicKeyMismatch,
                    snapshot.SnapshotId,
                    "Snapshot signature public key hash does not match public key material."));
                continue;
            }

            if (signatureProviderRegistry is null)
            {
                issues.Add(new EfLedgerSnapshotVerificationIssue(
                    EfLedgerSnapshotVerificationIssueCode.SnapshotSignatureVerificationUnavailable,
                    snapshot.SnapshotId,
                    "Snapshot signature verification requires a signature provider registry."));
                continue;
            }

            ISignatureProvider provider = signatureProviderRegistry.Get(signature.Algorithm);
            byte[] payload = Encoding.UTF8.GetBytes(payloadHash.Value);
            if (!provider.Verify(payload, signature.SignatureBytes, publicKeyMaterial))
            {
                issues.Add(new EfLedgerSnapshotVerificationIssue(
                    EfLedgerSnapshotVerificationIssueCode.SnapshotSignatureInvalid,
                    snapshot.SnapshotId,
                    "Snapshot admin signature failed cryptographic verification."));
            }
        }
    }

    private static string CreateSnapshotId(EfLedgerProjectionHashSet hashSet)
        => "snap-" + hashSet.CurrentSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "-" + hashSet.ProjectionHash.Value[..16];

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % 10), TimeSpan.Zero);
    }

    private static void CompareHash(
        string scope,
        string expected,
        string actual,
        EfLedgerSnapshotVerificationIssueCode code,
        List<EfLedgerSnapshotVerificationIssue> issues)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new EfLedgerSnapshotVerificationIssue(
            code,
            scope,
            $"Snapshot {scope} value {expected} does not match current value {actual}."));
    }

    private static LedgerSnapshotRecord ToRecord(LedgerSnapshot snapshot)
        => new()
        {
            SnapshotId = snapshot.SnapshotId,
            LedgerId = snapshot.LedgerId.Value,
            ScenarioId = snapshot.ScenarioId?.Value,
            LedgerSequence = snapshot.LedgerSequence,
            LedgerHash = snapshot.LedgerHash.Value,
            UtxoSetHash = snapshot.UtxoSetHash.Value,
            AddressStateHash = snapshot.AddressStateHash.Value,
            BusinessObjectStateHash = snapshot.BusinessObjectStateHash.Value,
            CreatedAt = snapshot.CreatedAt,
            AdminSignatureJson = EfSignatureBundleJson.Serialize(snapshot.AdminSignature)
        };

    private static LedgerSnapshot ToSnapshot(LedgerSnapshotRecord row)
        => new(
            row.SnapshotId,
            new LedgerId(row.LedgerId),
            string.IsNullOrWhiteSpace(row.ScenarioId) ? null : new ScenarioId(row.ScenarioId),
            row.LedgerSequence,
            new HashHex(row.LedgerHash),
            new HashHex(row.UtxoSetHash),
            new HashHex(row.AddressStateHash),
            new HashHex(row.BusinessObjectStateHash),
            row.CreatedAt,
            EfSignatureBundleJson.Deserialize(row.AdminSignatureJson));
}

public sealed record EfLedgerSnapshotVerificationReport(
    bool IsValid,
    LedgerSnapshot? Snapshot,
    EfLedgerProjectionHashSet? CurrentProjectionHashSet,
    IReadOnlyList<EfLedgerSnapshotVerificationIssue> Issues);

public sealed record EfLedgerSnapshotVerificationIssue(
    EfLedgerSnapshotVerificationIssueCode Code,
    string Scope,
    string Message);

public enum EfLedgerSnapshotVerificationIssueCode
{
    SnapshotMissing,
    SnapshotSequenceMismatch,
    SnapshotLedgerHashMismatch,
    SnapshotProjectionHashMismatch,
    SnapshotSignatureMissing,
    SnapshotSignaturePayloadMismatch,
    SnapshotSignaturePublicKeyMismatch,
    SnapshotSignatureVerificationUnavailable,
    SnapshotSignatureInvalid
}
