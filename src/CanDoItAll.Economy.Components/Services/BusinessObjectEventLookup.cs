using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Sdk;

namespace CanDoItAll.Economy.Components.Services;

internal static class BusinessObjectEventLookup
{
    public static async Task<BusinessObjectEventLookupResult> LookupAsync(
        ILedgerReadClient ledgerReadClient,
        IBusinessObjectCastRegistry castRegistry,
        LedgerAccountContext context,
        string? query,
        CancellationToken cancellationToken)
        => await LookupAsync(
            (businessObjectQuery, token) => ledgerReadClient.GetBusinessObjectAsync(businessObjectQuery, token),
            (ledgerContext, txId, includeData, token) => ledgerReadClient.GetTransactionAsync(ledgerContext, txId, includeData, token),
            castRegistry,
            context,
            query,
            cancellationToken).ConfigureAwait(false);

    public static async Task<BusinessObjectEventLookupResult> LookupAsync(
        EconomyClient economyClient,
        IBusinessObjectCastRegistry castRegistry,
        LedgerAccountContext context,
        string? query,
        CancellationToken cancellationToken)
        => await LookupAsync(
            (businessObjectQuery, token) => economyClient.GetBusinessObjectAsync(businessObjectQuery, token),
            (ledgerContext, txId, includeData, token) => economyClient.GetTransactionAsync(ledgerContext, txId, includeData, token),
            castRegistry,
            context,
            query,
            cancellationToken).ConfigureAwait(false);

    private static async Task<BusinessObjectEventLookupResult> LookupAsync(
        Func<LedgerBusinessObjectQuery, CancellationToken, Task<LedgerBusinessObjectHolding?>> getBusinessObjectAsync,
        Func<LedgerAccountContext, TxId, bool, CancellationToken, Task<LedgerTransactionSummary?>> getTransactionAsync,
        IBusinessObjectCastRegistry castRegistry,
        LedgerAccountContext context,
        string? query,
        CancellationToken cancellationToken)
    {
        string normalized = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BusinessObjectEventLookupResult.NotFound("Enter an event BO id or transaction hash.");
        }

        LedgerBusinessObjectHolding? holding = null;
        string lookupMode = "BusinessObject id";
        var requestedObjectId = new BusinessObjectId(normalized);

        if (requestedObjectId.IsCanonicalOutPointId())
        {
            try
            {
                holding = await getBusinessObjectAsync(
                    new LedgerBusinessObjectQuery(
                        context,
                        ObjectId: requestedObjectId,
                        ObjectType: "event",
                        Take: 1),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsLookupMiss(exception))
            {
                holding = null;
            }
        }

        if (holding is null)
        {
            lookupMode = "Transaction hash";
            try
            {
                LedgerTransactionSummary? transaction = await getTransactionAsync(
                    context,
                    new TxId(normalized),
                    true,
                    cancellationToken).ConfigureAwait(false);
                BusinessObjectId? objectId = transaction?.Outputs
                    .Where(output => output.HasBusinessObject)
                    .OrderBy(output => output.OutputIndex)
                    .Select(output => BusinessObjectId.FromOutPoint(transaction.TxId, output.OutputIndex))
                    .FirstOrDefault();

                if (objectId is not null)
                {
                    holding = await getBusinessObjectAsync(
                        new LedgerBusinessObjectQuery(
                            context,
                            ObjectId: objectId,
                            ObjectType: "event",
                            Take: 1),
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (IsLookupMiss(exception))
            {
                holding = null;
            }
        }

        if (holding is null)
        {
            return BusinessObjectEventLookupResult.NotFound($"No event BO was found for '{ShortValue(normalized)}'.");
        }

        if (!holding.ObjectType.Equals("event", StringComparison.OrdinalIgnoreCase))
        {
            return BusinessObjectEventLookupResult.NotFound($"The resolved BO is '{holding.ObjectType}', not an event.");
        }

        IBusinessObjectView view = await castRegistry
            .CastAsync(CreateCastSource(context, holding), cancellationToken)
            .ConfigureAwait(false);
        return new BusinessObjectEventLookupResult(
            true,
            $"{lookupMode} resolved to {EventDisplayName(view)}.",
            view);
    }

    public static BusinessObjectCastSource CreateCastSource(
        LedgerAccountContext context,
        LedgerBusinessObjectHolding holding)
        => new(
            context,
            holding.ObjectId,
            holding.ObjectType,
            holding.SchemaVersion,
            holding.Version,
            holding.OwnerAddress,
            holding.Operation,
            holding.DataHash,
            holding.CurrentOutPoint,
            holding.StateJson,
            holding.ExternalRef,
            holding.ItemType,
            holding.CreatedAt);

    private static bool IsLookupMiss(Exception exception)
        => exception is NotSupportedException
            or ArgumentException
            or FormatException
            or InvalidOperationException;

    private static string EventDisplayName(IBusinessObjectView view)
        => view is ITypedBusinessObjectView typed && !string.IsNullOrWhiteSpace(typed.Name)
            ? typed.Name
            : view.ObjectId.Value;

    private static string ShortValue(string value)
        => value.Length > 36 ? $"{value[..24]}...{value[^8..]}" : value;
}
