using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Rendering;

public static class RuleBusinessObjectDisplay
{
    public static bool CanDisplay(IBusinessObjectView businessObject)
        => TryReadRule(businessObject, out _);

    public static bool TryReadRule(IBusinessObjectView businessObject, out AccountRuleDefinition rule)
    {
        rule = AccountRuleDefinition.Empty;
        if (string.IsNullOrWhiteSpace(businessObject.StateJson))
        {
            return false;
        }

        bool isRuleCandidate = businessObject is GenericBusinessObjectView { ItemType: TransactionDataItemType.Rule };
        if (!isRuleCandidate)
        {
            return false;
        }

        var data = new TransactionData(
            RuleTokenAuthority.RuleContentType,
            businessObject.DataHash,
            businessObject.StateJson,
            businessObject is GenericBusinessObjectView generic ? generic.ExternalRef : null,
            TransactionDataItemType.Rule);
        var reference = new BusinessObjectRef(
            new BusinessObjectLineageKey("display-only"),
            businessObject.Version,
            businessObject.Operation,
            businessObject.DataHash);

        return RuleTokenAuthority.TryParseData(data, reference, out rule);
    }
}
