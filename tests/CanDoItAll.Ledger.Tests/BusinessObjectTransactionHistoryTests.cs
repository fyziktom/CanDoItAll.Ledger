using System.Text;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class BusinessObjectTransactionHistoryTests
{
    private static readonly LedgerAccountContext Context = new(new LedgerId("history-test"));
    private static readonly Address Owner = Address.FromPublicKey(Hex.Utf8("history-owner"));

    [Fact]
    public void SelectVersions_FollowsCanonicalPreviousVersionReferencesWithoutLineage()
    {
        LedgerTransactionSummary first = CreateVersion('a', 1);
        BusinessObjectId firstId = IdOf(first);
        LedgerTransactionSummary second = CreateVersion('b', 2, firstId);
        BusinessObjectId secondId = IdOf(second);
        LedgerTransactionSummary third = CreateVersion('c', 3, secondId);
        LedgerTransactionSummary unrelatedClone = CreateVersion(
            'd',
            1,
            firstId,
            BusinessObjectReferenceRelationships.CloneSource);

        IReadOnlyList<BusinessObjectVersionTransaction> versions =
            BusinessObjectTransactionHistory.SelectVersions(
                IdOf(third),
                [unrelatedClone, third, first, second]);

        Assert.Equal([firstId, secondId, IdOf(third)], versions.Select(version => version.ObjectId));
        Assert.All(versions, version => Assert.Null(version.Output.BusinessObject));
    }

    [Fact]
    public void SelectVersions_UsesDataBindingForServerFilteredLegacyHistory()
    {
        LedgerTransactionSummary first = CreateVersion('e', 1, includeReferences: false);
        LedgerTransactionSummary second = CreateVersion('f', 2, includeReferences: false, includeSideBusinessObject: true);

        IReadOnlyList<BusinessObjectVersionTransaction> versions =
            BusinessObjectTransactionHistory.SelectVersions(
                IdOf(second),
                [second, first],
                transactionsAreLineageFiltered: true);

        Assert.Equal(2, versions.Count);
        Assert.Equal([1, 2], versions.Select(version => version.Output.BusinessObjectVersion));
        Assert.All(versions, version => Assert.Equal(
            version.Transaction.Data!.DataHash,
            version.Output.BusinessObjectDataHash));
    }

    [Fact]
    public void TicketValidation_AcceptsPublicDetailsOnlyHistory()
    {
        const string availableState =
            "{\"name\":\"Ticket\",\"description\":\"\",\"text\":\"\",\"tags\":\"#FreeToUse\",\"fields\":{}}";
        const string usedState =
            "{\"name\":\"Ticket\",\"description\":\"\",\"text\":\"\",\"tags\":\"#Used\",\"fields\":{\"used\":\"true\"}}";
        LedgerTransactionSummary first = CreateVersion('1', 1, stateJson: availableState, includeReferences: false);
        LedgerTransactionSummary second = CreateVersion('2', 2, stateJson: usedState, includeReferences: false);

        BusinessObjectTicketHistoryValidation validation = BusinessObjectTicketVerification.ValidateHistory(
            "ticket",
            IdOf(second),
            [second, first]);

        Assert.Equal(BusinessObjectTicketHistoryStatus.Used, validation.Status);
        Assert.Equal(2, validation.Entries.Count);
        Assert.Equal(second.TxId, validation.UsedTxId);
    }

    private static LedgerTransactionSummary CreateVersion(
        char txCharacter,
        int version,
        BusinessObjectId? sourceId = null,
        string relationship = BusinessObjectReferenceRelationships.PreviousVersion,
        string stateJson = "{}",
        bool includeReferences = true,
        bool includeSideBusinessObject = false)
    {
        var txId = new TxId(new string(txCharacter, 64));
        string references = includeReferences && sourceId is not null
            ? $"[{{\"relationship\":\"{relationship}\",\"objectId\":\"{sourceId.Value.Value}\"}}]"
            : "[]";
        string envelope =
            $"{{\"objectType\":\"ticket\",\"schemaVersion\":\"1.0\",\"stateJson\":{stateJson},\"references\":{references}}}";
        HashHex dataHash = Hex.Sha256Hex(Encoding.UTF8.GetBytes(envelope));
        var outputs = new List<LedgerTransactionOutputSummary>
        {
            new(
                0,
                Owner,
                Amount.Zero,
                BusinessObjectDetails: new LedgerBusinessObjectOutputDetails(
                    BusinessObjectId.FromOutPoint(txId, 0),
                    version,
                    version == 1 ? BusinessObjectOperation.Create : BusinessObjectOperation.Update,
                    dataHash))
        };
        if (includeSideBusinessObject)
        {
            outputs.Add(new LedgerTransactionOutputSummary(
                1,
                Owner,
                Amount.Zero,
                BusinessObjectDetails: new LedgerBusinessObjectOutputDetails(
                    BusinessObjectId.FromOutPoint(txId, 1),
                    1,
                    BusinessObjectOperation.Create,
                    new HashHex(new string('9', 64)))));
        }

        return new LedgerTransactionSummary(
            Context,
            txId,
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            DateTimeOffset.UnixEpoch.AddMinutes(version),
            [Owner],
            [Owner],
            [],
            outputs,
            new TransactionData(
                "application/json",
                dataHash,
                envelope,
                null,
                TransactionDataItemType.BusinessObject),
            version);
    }

    private static BusinessObjectId IdOf(LedgerTransactionSummary transaction)
        => BusinessObjectId.FromOutPoint(transaction.TxId, 0);
}
