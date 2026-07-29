namespace CanDoItAll.Ledger.Simulator;

public static class LedgerSimulatorPeriods
{
    public static IEnumerable<DateOnly> Months(DateOnly start, DateOnly end)
    {
        DateOnly current = new(start.Year, start.Month, 1);
        DateOnly last = new(end.Year, end.Month, 1);
        while (current <= last)
        {
            yield return current;
            current = current.AddMonths(1);
        }
    }

    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static string MonthKey(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";

    public static bool IsActive(DateOnly period, DateOnly start, DateOnly? end)
        => MonthStart(period) >= MonthStart(start) && (end is null || MonthStart(period) <= MonthStart(end.Value));
}

public static class LedgerScenarioMetadataKeys
{
    public const string ScenarioId = "scenarioId";
    public const string BusinessUnitId = "businessUnitId";
    public const string AccountAddress = "accountAddress";
    public const string CounterpartyId = "counterpartyId";
    public const string CustomerContractId = "customerContractId";
    public const string ProductOrServiceId = "productOrServiceId";
    public const string FinancialStatementSection = "financialStatementSection";
    public const string Period = "period";
    public const string Description = "description";
    public const string WorkOrderId = "workOrderId";
    public const string WorkAllocationRuleId = "workAllocationRuleId";
    public const string WorkDeliveryId = "workDeliveryId";
    public const string ProductFlowPlanId = "productFlowPlanId";
    public const string ProductFlowStepId = "productFlowStepId";
    public const string DemandScheduleId = "demandScheduleId";
    public const string InvoiceId = "invoiceId";
    public const string StrategyCode = "strategyCode";
    public const string FlowStepKind = "flowStepKind";
    public const string Quantity = "quantity";
    public const string FinancialScheduleId = "financialScheduleId";
    public const string ParentFinancialScheduleId = "parentFinancialScheduleId";
    public const string FinancialSchedulePeriod = "financialSchedulePeriod";
    public const string ApprovalStatus = "approvalStatus";
    public const string ApprovalScope = "approvalScope";
}

public static class LedgerScenarioEventFactory
{
    public static EconomicEventState Create(
        string projectId,
        ScenarioId scenarioId,
        string correlation,
        int sequence,
        EconomicEventKind kind,
        EconomicEventStatus status,
        EconomicImpactKind impactKind,
        BusinessUnitId businessUnitId,
        string categoryCode,
        Amount amount,
        string currencyCode,
        DateOnly economicDate,
        FinancialStatementSection section,
        string? counterpartyId = null,
        string? contractId = null,
        string? productOrServiceId = null,
        string? sourceObjectId = null,
        string? description = null,
        Address? accountAddress = null,
        TokenTransfer? tokenEffect = null,
        BusinessUnitId? destinationBusinessUnitId = null,
        IReadOnlyDictionary<string, string>? additionalMetadata = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LedgerScenarioMetadataKeys.ScenarioId] = scenarioId.Value,
            [LedgerScenarioMetadataKeys.BusinessUnitId] = businessUnitId.Value,
            [LedgerScenarioMetadataKeys.FinancialStatementSection] = section.ToString(),
            [LedgerScenarioMetadataKeys.Period] = LedgerSimulatorPeriods.MonthKey(economicDate)
        };

        if (accountAddress is not null)
        {
            metadata[LedgerScenarioMetadataKeys.AccountAddress] = accountAddress.Value.Value;
        }

        if (!string.IsNullOrWhiteSpace(counterpartyId))
        {
            metadata[LedgerScenarioMetadataKeys.CounterpartyId] = counterpartyId;
        }

        if (!string.IsNullOrWhiteSpace(contractId))
        {
            metadata[LedgerScenarioMetadataKeys.CustomerContractId] = contractId;
        }

        if (!string.IsNullOrWhiteSpace(productOrServiceId))
        {
            metadata[LedgerScenarioMetadataKeys.ProductOrServiceId] = productOrServiceId;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            metadata[LedgerScenarioMetadataKeys.Description] = description;
        }

        if (additionalMetadata is not null)
        {
            foreach (KeyValuePair<string, string> item in additionalMetadata)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value is not null)
                {
                    metadata[item.Key] = item.Value;
                }
            }
        }

        return EconomicEventGenerator.CreateEvent(
            projectId,
            correlation,
            sequence,
            kind,
            status,
            impactKind,
            businessUnitId,
            destinationBusinessUnitId,
            categoryCode,
            amount,
            currencyCode,
            economicDate,
            economicDate,
            1m,
            sourceObjectId: sourceObjectId,
            metadata: metadata,
            sourceAddress: accountAddress) with
        {
            TokenEffect = tokenEffect
        };
    }
}

public static class LedgerScenarioQuantities
{
    public static decimal DemandForMonth(DemandScheduleState schedule, DateOnly month)
    {
        if (!LedgerSimulatorPeriods.IsActive(month, schedule.StartDate, schedule.EndDate))
        {
            return 0m;
        }

        int monthsFromStart = ((month.Year - schedule.StartDate.Year) * 12) + month.Month - schedule.StartDate.Month;
        decimal quantity = schedule.InitialQuantity;
        if (schedule.MonthlyGrowthRate != 0m)
        {
            quantity *= (decimal)Math.Pow((double)(1m + schedule.MonthlyGrowthRate), monthsFromStart);
        }

        foreach (DemandChangeState change in schedule.DemandChanges.OrderBy(x => x.EffectiveDate))
        {
            if (month >= LedgerSimulatorPeriods.MonthStart(change.EffectiveDate))
            {
                quantity = (quantity + change.QuantityDelta) * change.Multiplier;
            }
        }

        return Math.Max(0m, decimal.Round(quantity, 4));
    }

    public static decimal FteForMonth(DepartmentStaffingState staffing, DateOnly month)
    {
        decimal fte = staffing.StartingFte;
        foreach (HeadcountEventState item in staffing.HeadcountEvents.OrderBy(x => x.EffectiveDate))
        {
            if (month >= LedgerSimulatorPeriods.MonthStart(item.EffectiveDate))
            {
                fte += item.FteDelta;
            }
        }

        return Math.Max(0m, fte);
    }

    public static decimal NewHireFteForMonth(DepartmentStaffingState staffing, DateOnly month)
        => staffing.HeadcountEvents
            .Where(x => LedgerSimulatorPeriods.MonthStart(x.EffectiveDate) == LedgerSimulatorPeriods.MonthStart(month) && x.FteDelta > 0m)
            .Sum(x => x.FteDelta);

    public static bool IsCadenceMonth(ScenarioCadence cadence, DateOnly startDate, DateOnly month)
    {
        int months = ((month.Year - startDate.Year) * 12) + month.Month - startDate.Month;
        if (months < 0)
        {
            return false;
        }

        return cadence switch
        {
            ScenarioCadence.Once => months == 0,
            ScenarioCadence.Monthly => true,
            ScenarioCadence.Quarterly => months % 3 == 0,
            ScenarioCadence.HalfYearly => months % 6 == 0,
            ScenarioCadence.Annual => months % 12 == 0,
            _ => true
        };
    }
}
