namespace CanDoItAll.Ledger.Simulator;

public interface IBusinessUnitRegistry
{
    LedgerSimulatorBusinessObjectCommandDraft CreateBusinessUnitDraft(BusinessUnitState state);

    LedgerSimulatorBusinessObjectCommandDraft CreateAddressBindingDraft(BusinessUnitAddressBindingState state);

    BusinessUnitAddressBindingState RotateBinding(BusinessUnitAddressBindingState current, Address newAddress, DateTimeOffset rotatedAtUtc);
}

public interface IBusinessUnitAddressResolver
{
    Address ResolvePrimaryAddress(BusinessUnitState businessUnit);

    bool IsActive(BusinessUnitAddressBindingState binding, DateOnly economicDate);
}

public interface IEconomicEventGenerator
{
    IReadOnlyList<EconomicEventState> Generate(EconomicEventGenerationRequest request);
}

public interface IPayrollRuleProvider
{
    IReadOnlyList<EconomicEventState> GeneratePayrollEvents(PayrollPlanState plan, DateOnly periodStart);
}

public interface ITaxRuleProvider
{
    IReadOnlyList<EconomicEventState> GenerateTaxEvents(TaxRuleSetState ruleSet, Amount taxableBase, BusinessUnitId sourceBusinessUnitId, DateOnly periodEnd);
}

public interface IDepreciationRuleProvider
{
    DepreciationScheduleState BuildSchedule(AssetState asset, DepreciationMethod method);

    IReadOnlyList<EconomicEventState> GenerateDepreciationEvents(DepreciationScheduleState schedule, AssetState asset);
}

public interface IEconomicPostingService
{
    LedgerSimulatorBusinessObjectCommandDraft BuildEconomicEventDraft(EconomicEventState eventState);

    Task<SimulatorSubmitResult> PostEconomicEventAsync(
        BusinessObjectAccount account,
        EconomicEventState eventState,
        CancellationToken cancellationToken);
}

public interface IExternalBoundaryPostingService
{
    ExternalSettlementPlan BuildSettlementPlan(ExternalSettlementRequest request);
}

public interface ILedgerScenarioRunner
{
    ValueTask<ScenarioRunResult> RunAsync(ScenarioRunRequest request, CancellationToken cancellationToken);
}

public interface ILedgerProjectionService
{
    LedgerProjection BuildProjection(ProjectionRequest request);
}

public interface ILedgerCalibrationService
{
    CalibrationRecommendationState RecommendAssumptionUpdate(
        string projectId,
        string metricCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal plannedValue,
        decimal actualValue,
        IReadOnlyList<string> evidenceObjectIds);
}

public interface IActualTrackingService
{
    ActualEconomicEntryState ValidateActualEntry(ActualEntryRequest request);
}

public sealed record EconomicEventGenerationRequest(
    string ProjectId,
    RevenueStreamState? RevenueStream,
    CostPolicyState? CostPolicy,
    DateOnly StartDate,
    int Periods,
    string CurrencyCode);

public sealed class BusinessUnitRegistry(ILedgerSimulatorJsonSerializer serializer) : IBusinessUnitRegistry
{
    public LedgerSimulatorBusinessObjectCommandDraft CreateBusinessUnitDraft(BusinessUnitState state)
    {
        if (!state.KnownAddresses.Contains(state.PrimaryAddress))
        {
            throw new InvalidOperationException("Business unit primary address must be part of known addresses.");
        }

        return serializer.CreateDraft(
            LedgerSimulatorObjectTypes.BusinessUnit,
            state,
            state.PrimaryAddress);
    }

    public LedgerSimulatorBusinessObjectCommandDraft CreateAddressBindingDraft(BusinessUnitAddressBindingState state)
        => serializer.CreateDraft(
            LedgerSimulatorObjectTypes.BusinessUnitAddressBinding,
            state,
            state.Address);

    public BusinessUnitAddressBindingState RotateBinding(BusinessUnitAddressBindingState current, Address newAddress, DateTimeOffset rotatedAtUtc)
    {
        if (current.Status != BusinessUnitAddressBindingStatus.Active)
        {
            throw new InvalidOperationException("Only an active binding can be rotated.");
        }

        return current with
        {
            Address = newAddress,
            Status = BusinessUnitAddressBindingStatus.Active,
            ValidFromUtc = rotatedAtUtc,
            ValidToUtc = null,
            ParentAddress = current.Address,
            Metadata = current.Metadata
                .Concat(new[] { KeyValuePair.Create("rotatedFrom", current.Address.Value) })
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };
    }
}

public sealed class BusinessUnitAddressResolver : IBusinessUnitAddressResolver
{
    public Address ResolvePrimaryAddress(BusinessUnitState businessUnit)
    {
        if (!businessUnit.KnownAddresses.Contains(businessUnit.PrimaryAddress))
        {
            throw new InvalidOperationException($"Business unit '{businessUnit.BusinessUnitId}' has a primary address that is not in the known-address set.");
        }

        return businessUnit.PrimaryAddress;
    }

    public bool IsActive(BusinessUnitAddressBindingState binding, DateOnly economicDate)
    {
        DateTimeOffset dateStart = new(economicDate, TimeOnly.MinValue, TimeSpan.Zero);
        return binding.Status == BusinessUnitAddressBindingStatus.Active
            && binding.ValidFromUtc <= dateStart
            && (binding.ValidToUtc is null || binding.ValidToUtc >= dateStart);
    }
}

public sealed class EconomicEventGenerator : IEconomicEventGenerator
{
    public IReadOnlyList<EconomicEventState> Generate(EconomicEventGenerationRequest request)
    {
        if (request.Periods <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "At least one period is required.");
        }

        var events = new List<EconomicEventState>();
        for (var i = 0; i < request.Periods; i++)
        {
            DateOnly date = request.StartDate.AddMonths(i);
            if (request.RevenueStream is { } revenue)
            {
                Amount revenueAmount = Amount.FromDecimal(revenue.UnitPrice * revenue.UnitsPerPeriod);
                string correlation = $"{revenue.RevenueStreamId}:{date:yyyyMM}";
                events.Add(CreateEvent(
                    request.ProjectId,
                    correlation,
                    1,
                    EconomicEventKind.Revenue,
                    EconomicEventStatus.Invoiced,
                    EconomicImpactKind.AccrualOnly,
                    revenue.RevenueBusinessUnitId,
                    null,
                    "revenue",
                    revenueAmount,
                    request.CurrencyCode,
                    date,
                    date.AddDays(revenue.CollectionDelayDays),
                    revenue.Confidence,
                    sourceObjectId: revenue.RevenueStreamId));

                events.Add(CreateEvent(
                    request.ProjectId,
                    correlation,
                    2,
                    EconomicEventKind.Revenue,
                    EconomicEventStatus.Planned,
                    EconomicImpactKind.CashOnly,
                    revenue.RevenueBusinessUnitId,
                    null,
                    "cash-collection",
                    revenueAmount,
                    request.CurrencyCode,
                    date.AddDays(revenue.CollectionDelayDays),
                    date.AddDays(revenue.CollectionDelayDays),
                    revenue.Confidence,
                    sourceObjectId: revenue.RevenueStreamId));

                if (revenue.UnitCost > 0)
                {
                    events.Add(CreateEvent(
                        request.ProjectId,
                        correlation,
                        3,
                        revenue.Kind == RevenueStreamKind.Product ? EconomicEventKind.Cogs : EconomicEventKind.Expense,
                        EconomicEventStatus.Planned,
                        revenue.Kind == RevenueStreamKind.Product ? EconomicImpactKind.CashAndAccrual : EconomicImpactKind.CashOnly,
                        revenue.RevenueBusinessUnitId,
                        null,
                        "cogs",
                        Amount.FromDecimal(revenue.UnitCost * revenue.UnitsPerPeriod),
                        request.CurrencyCode,
                        date,
                        date,
                        revenue.Confidence,
                        sourceObjectId: revenue.RevenueStreamId));
                }
            }

            if (request.CostPolicy is { } cost)
            {
                string correlation = $"{cost.CostPolicyId}:{date:yyyyMM}";
                events.Add(CreateEvent(
                    request.ProjectId,
                    correlation,
                    1,
                    EconomicEventKind.Expense,
                    EconomicEventStatus.Planned,
                    cost.ImpactKind,
                    cost.BusinessUnitId,
                    null,
                    cost.CategoryCode,
                    cost.AmountPerPeriod,
                    request.CurrencyCode,
                    date,
                    date.AddDays(cost.PaymentDelayDays),
                    0.95m,
                    sourceObjectId: cost.CostPolicyId));
            }
        }

        return events
            .OrderBy(x => x.EconomicDate)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.SourceTemplateId, StringComparer.Ordinal)
            .ThenBy(x => x.EventId, StringComparer.Ordinal)
            .ToArray();
    }

    public static EconomicEventState CreateEvent(
        string projectId,
        string correlation,
        int sequence,
        EconomicEventKind kind,
        EconomicEventStatus status,
        EconomicImpactKind impactKind,
        BusinessUnitId sourceBusinessUnitId,
        BusinessUnitId? destinationBusinessUnitId,
        string categoryCode,
        Amount amount,
        string currencyCode,
        DateOnly economicDate,
        DateOnly? expectedSettlementDate,
        decimal confidence,
        string? sourceTemplateId = null,
        string? sourceObjectId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? subcategoryCode = null,
        Address? sourceAddress = null,
        Address? destinationAddress = null)
        => new(
            projectId,
            LedgerSimulatorIdentifiers.EventId(projectId, correlation, sequence).Value,
            kind,
            status,
            impactKind,
            sourceBusinessUnitId,
            destinationBusinessUnitId,
            sourceAddress,
            destinationAddress,
            categoryCode,
            subcategoryCode,
            amount,
            currencyCode,
            null,
            economicDate,
            expectedSettlementDate,
            expectedSettlementDate,
            null,
            null,
            confidence,
            sequence,
            true,
            sourceTemplateId,
            sourceObjectId,
            correlation,
            new Dictionary<string, string>(),
            metadata ?? new Dictionary<string, string>());
}

public sealed class GenericPayrollRuleProvider : IPayrollRuleProvider
{
    public IReadOnlyList<EconomicEventState> GeneratePayrollEvents(PayrollPlanState plan, DateOnly periodStart)
    {
        Amount withholding = Amount.FromDecimal(decimal.Round(plan.GrossWagesPerPeriod.Value * plan.EmployeeWithholdingRate, 2));
        Amount employer = Amount.FromDecimal(decimal.Round(plan.GrossWagesPerPeriod.Value * plan.EmployerContributionRate, 2));
        Amount net = Amount.FromDecimal(plan.GrossWagesPerPeriod.Value - withholding.Value);
        DateOnly payDate = new(periodStart.Year, periodStart.Month, Math.Clamp(plan.PaymentDay, 1, DateTime.DaysInMonth(periodStart.Year, periodStart.Month)));
        return new[]
        {
            EconomicEventGenerator.CreateEvent(plan.ProjectId, $"{plan.PayrollPlanId}:{periodStart:yyyyMM}", 1, EconomicEventKind.Payroll, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, plan.BusinessUnitId, null, "payroll-net", net, "USD", payDate, payDate, 0.98m, sourceObjectId: plan.PayrollPlanId),
            EconomicEventGenerator.CreateEvent(plan.ProjectId, $"{plan.PayrollPlanId}:{periodStart:yyyyMM}", 2, EconomicEventKind.EmployeeWithholding, EconomicEventStatus.Planned, EconomicImpactKind.AccrualOnly, plan.BusinessUnitId, null, "payroll-withholding", withholding, "USD", payDate, payDate.AddDays(15), 0.98m, sourceObjectId: plan.PayrollPlanId),
            EconomicEventGenerator.CreateEvent(plan.ProjectId, $"{plan.PayrollPlanId}:{periodStart:yyyyMM}", 3, EconomicEventKind.EmployerContribution, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, plan.BusinessUnitId, null, "employer-contribution", employer, "USD", payDate, payDate.AddDays(15), 0.98m, sourceObjectId: plan.PayrollPlanId)
        };
    }
}

public sealed class GenericTaxRuleProvider : ITaxRuleProvider
{
    public IReadOnlyList<EconomicEventState> GenerateTaxEvents(TaxRuleSetState ruleSet, Amount taxableBase, BusinessUnitId sourceBusinessUnitId, DateOnly periodEnd)
    {
        Amount tax = Amount.FromDecimal(decimal.Round(taxableBase.Value * ruleSet.Rate, 2));
        DateOnly dueDate = periodEnd.AddDays(ruleSet.DueDelayDays);
        return new[]
        {
            EconomicEventGenerator.CreateEvent(ruleSet.ProjectId, $"{ruleSet.TaxRuleSetId}:{periodEnd:yyyyMMdd}", 1, EconomicEventKind.Tax, EconomicEventStatus.Due, EconomicImpactKind.AccrualOnly, sourceBusinessUnitId, ruleSet.AuthorityBusinessUnitId, ruleSet.TaxCode, tax, "USD", periodEnd, dueDate, 0.95m, sourceObjectId: ruleSet.TaxRuleSetId),
            EconomicEventGenerator.CreateEvent(ruleSet.ProjectId, $"{ruleSet.TaxRuleSetId}:{periodEnd:yyyyMMdd}", 2, EconomicEventKind.Tax, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, sourceBusinessUnitId, ruleSet.AuthorityBusinessUnitId, $"{ruleSet.TaxCode}-payment", tax, "USD", dueDate, dueDate, 0.95m, sourceObjectId: ruleSet.TaxRuleSetId)
        };
    }
}

public sealed class GenericDepreciationRuleProvider : IDepreciationRuleProvider
{
    public DepreciationScheduleState BuildSchedule(AssetState asset, DepreciationMethod method)
    {
        if (asset.UsefulLifeMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(asset), "Useful life must be positive.");
        }

        decimal depreciable = Math.Max(0m, asset.AcquisitionCost.Value - asset.SalvageValue.Value);
        return new DepreciationScheduleState(
            asset.ProjectId,
            $"dep:{LedgerSimulatorIdentifiers.NormalizeSegment(asset.ProjectId)}:{LedgerSimulatorIdentifiers.NormalizeSegment(asset.AssetId)}:{LedgerSimulatorIdentifiers.NormalizeSegment(method.ToString())}",
            asset.AssetId,
            method,
            asset.AcquisitionDate,
            asset.UsefulLifeMonths,
            Amount.FromDecimal(decimal.Round(depreciable / asset.UsefulLifeMonths, 2)),
            new Dictionary<string, string>());
    }

    public IReadOnlyList<EconomicEventState> GenerateDepreciationEvents(DepreciationScheduleState schedule, AssetState asset)
        => Enumerable.Range(0, schedule.Periods)
            .Select(index => EconomicEventGenerator.CreateEvent(
                asset.ProjectId,
                $"{schedule.ScheduleId}:{index + 1}",
                1,
                EconomicEventKind.Depreciation,
                EconomicEventStatus.Planned,
                EconomicImpactKind.NonCashPnl,
                asset.BusinessUnitId,
                null,
                "depreciation",
                schedule.PeriodAmount,
                "USD",
                schedule.StartDate.AddMonths(index),
                null,
                1m,
                sourceObjectId: asset.AssetId))
            .ToArray();
}

public sealed class EconomicPostingService(ILedgerSimulatorJsonSerializer serializer) : IEconomicPostingService
{
    public LedgerSimulatorBusinessObjectCommandDraft BuildEconomicEventDraft(EconomicEventState eventState)
        => serializer.CreateDraft(
            LedgerSimulatorObjectTypes.EconomicEvent,
            eventState,
            eventState.SourceAddress,
            eventState.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual ? eventState.Amount : null,
            eventState.TokenEffect,
            eventState.CorrelationId);

    public async Task<SimulatorSubmitResult> PostEconomicEventAsync(
        BusinessObjectAccount account,
        EconomicEventState eventState,
        CancellationToken cancellationToken)
    {
        LedgerSimulatorBusinessObjectCommandDraft draft = BuildEconomicEventDraft(eventState);
        return await account.CreateBusinessObjectAsync(draft.ToCreateBusinessObjectRequest(), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ExternalBoundaryPostingService(ILedgerSimulatorJsonSerializer serializer) : IExternalBoundaryPostingService
{
    public const string ExternalClearingTransferPolicy = "external-clearing-transfer";

    public ExternalSettlementPlan BuildSettlementPlan(ExternalSettlementRequest request)
    {
        request.Amount.EnsureNonNegative(nameof(request.Amount));
        var state = new ExternalSettlementState(
            request.ProjectId,
            request.SettlementId,
            request.BoundaryBusinessUnitId,
            request.BoundaryAddress,
            request.InternalBusinessUnitId,
            request.InternalAddress,
            request.Amount,
            request.CurrencyCode,
            request.IsInflow,
            ExternalClearingTransferPolicy,
            request.ExternalReference,
            request.SettlementDate,
            request.Metadata ?? new Dictionary<string, string>());

        LedgerSimulatorBusinessObjectCommandDraft draft = serializer.CreateDraft(
            LedgerSimulatorObjectTypes.ExternalSettlement,
            state,
            request.IsInflow ? request.InternalAddress : request.BoundaryAddress,
            request.Amount,
            externalRef: request.ExternalReference);

        PaymentRequest? clearingTransfer = request.Amount.Value == 0m
            ? null
            : new PaymentRequest(request.IsInflow ? request.InternalAddress : request.BoundaryAddress, request.Amount);

        return new ExternalSettlementPlan(state, draft, clearingTransfer, ExternalClearingTransferPolicy);
    }
}
