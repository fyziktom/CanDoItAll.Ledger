using CanDoItAll.Ledger.Simulator;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Ledger.Simulator.Persistence.EFCore;

public interface ILedgerSimulatorGraphProjectionWriter
{
    Task ReplaceScenarioGraphPointsAsync(
        string projectId,
        string scenarioId,
        IReadOnlyList<EconomicEventState> events,
        CancellationToken cancellationToken);
}

public interface ILedgerSimulatorGraphQueryService
{
    Task<LedgerGraphData> QueryAsync(LedgerGraphQueryFilter filter, CancellationToken cancellationToken);
}

public sealed class LedgerSimulatorGraphProjectionWriter(
    IDbContextFactory<LedgerSimulatorProjectionDbContext> dbContextFactory,
    ILedgerSimulatorJsonSerializer serializer) : ILedgerSimulatorGraphProjectionWriter
{
    public async Task ReplaceScenarioGraphPointsAsync(
        string projectId,
        string scenarioId,
        IReadOnlyList<EconomicEventState> events,
        CancellationToken cancellationToken)
    {
        await using LedgerSimulatorProjectionDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<LedgerSimulatorGraphPointRecord> existing = await db.GraphPoints
            .Where(x => x.ProjectId == projectId && x.ScenarioId == scenarioId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.GraphPoints.RemoveRange(existing);

        var rows = events.Select(x => ToRecord(x, serializer)).ToArray();
        db.GraphPoints.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static LedgerSimulatorGraphPointRecord ToRecord(EconomicEventState eventState, ILedgerSimulatorJsonSerializer serializer)
    {
        bool outflow = IsOutflow(eventState);
        bool cashImpact = eventState.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual;
        bool pnlWithoutCashMovement = eventState.ImpactKind is EconomicImpactKind.AccrualOnly or EconomicImpactKind.NonCashPnl;
        string section = Get(eventState, LedgerScenarioMetadataKeys.FinancialStatementSection) ?? MapSection(eventState);
        decimal amount = eventState.Amount.Value;
        decimal plannedInflow = 0m;
        decimal plannedOutflow = 0m;
        decimal committedInflow = 0m;
        decimal committedOutflow = 0m;
        decimal actualInflow = 0m;
        decimal actualOutflow = 0m;
        decimal simulatedInflow = 0m;
        decimal simulatedOutflow = 0m;

        if (cashImpact)
        {
            switch (eventState.Status)
            {
                case EconomicEventStatus.Actual:
                case EconomicEventStatus.Settled:
                case EconomicEventStatus.Reconciled:
                    (actualInflow, actualOutflow) = Split(amount, outflow);
                    break;
                case EconomicEventStatus.Committed:
                case EconomicEventStatus.Invoiced:
                case EconomicEventStatus.Due:
                case EconomicEventStatus.PartiallySettled:
                    (committedInflow, committedOutflow) = Split(amount, outflow);
                    break;
                case EconomicEventStatus.Simulated:
                    (simulatedInflow, simulatedOutflow) = Split(amount, outflow);
                    break;
                default:
                    (plannedInflow, plannedOutflow) = Split(amount, outflow);
                    break;
            }
        }

        return new LedgerSimulatorGraphPointRecord
        {
            ProjectId = eventState.ProjectId,
            ScenarioId = Get(eventState, LedgerScenarioMetadataKeys.ScenarioId),
            RunId = Get(eventState, "runId"),
            BusinessUnitId = eventState.SourceBusinessUnitId.Value,
            AccountAddress = eventState.SourceAddress?.Value ?? Get(eventState, LedgerScenarioMetadataKeys.AccountAddress),
            CounterpartyId = Get(eventState, LedgerScenarioMetadataKeys.CounterpartyId),
            CustomerContractId = Get(eventState, LedgerScenarioMetadataKeys.CustomerContractId),
            ProductOrServiceId = Get(eventState, LedgerScenarioMetadataKeys.ProductOrServiceId),
            CategoryCode = eventState.CategoryCode,
            EventStatus = eventState.Status.ToString(),
            ImpactKind = eventState.ImpactKind.ToString(),
            EventKind = eventState.Kind.ToString(),
            FinancialStatementSection = section,
            PeriodStart = LedgerSimulatorPeriods.MonthStart(eventState.EconomicDate),
            PeriodEnd = LedgerSimulatorPeriods.MonthStart(eventState.EconomicDate).AddMonths(1).AddDays(-1),
            Interval = "monthly",
            PlannedInflow = plannedInflow,
            PlannedOutflow = plannedOutflow,
            CommittedInflow = committedInflow,
            CommittedOutflow = committedOutflow,
            ActualInflow = actualInflow,
            ActualOutflow = actualOutflow,
            SimulatedInflow = simulatedInflow,
            SimulatedOutflow = simulatedOutflow,
            NonCashIncome = pnlWithoutCashMovement && !outflow ? amount : 0m,
            NonCashExpense = pnlWithoutCashMovement && outflow ? amount : 0m,
            SourceEventId = eventState.EventId,
            SourceObjectId = eventState.SourceObjectId,
            PayloadJson = serializer.Serialize(eventState),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static (decimal Inflow, decimal Outflow) Split(decimal amount, bool outflow)
        => outflow ? (0m, amount) : (amount, 0m);

    private static bool IsOutflow(EconomicEventState eventState)
        => eventState.Kind is EconomicEventKind.Expense
            or EconomicEventKind.Payroll
            or EconomicEventKind.EmployerContribution
            or EconomicEventKind.EmployeeWithholding
            or EconomicEventKind.Tax
            or EconomicEventKind.Capex
            or EconomicEventKind.DebtRepayment
            or EconomicEventKind.DividendOrDistribution
            or EconomicEventKind.InventoryPurchase
            or EconomicEventKind.Cogs;

    private static string? Get(EconomicEventState eventState, string key)
        => eventState.Metadata.TryGetValue(key, out string? value) ? value : null;

    private static string MapSection(EconomicEventState eventState)
        => eventState.Kind switch
        {
            EconomicEventKind.Revenue when eventState.ImpactKind == EconomicImpactKind.CashOnly => FinancialStatementSection.CashReceipts.ToString(),
            EconomicEventKind.Revenue => FinancialStatementSection.Revenue.ToString(),
            EconomicEventKind.Cogs => FinancialStatementSection.Cogs.ToString(),
            EconomicEventKind.Payroll => FinancialStatementSection.Payroll.ToString(),
            EconomicEventKind.Capex => FinancialStatementSection.Capex.ToString(),
            EconomicEventKind.Depreciation => FinancialStatementSection.Depreciation.ToString(),
            EconomicEventKind.InventoryPurchase or EconomicEventKind.InventoryConsumption => FinancialStatementSection.Inventory.ToString(),
            _ => FinancialStatementSection.Opex.ToString()
        };
}

public sealed class LedgerSimulatorGraphQueryService(
    IDbContextFactory<LedgerSimulatorProjectionDbContext> dbContextFactory) : ILedgerSimulatorGraphQueryService
{
    public async Task<LedgerGraphData> QueryAsync(LedgerGraphQueryFilter filter, CancellationToken cancellationToken)
    {
        await using LedgerSimulatorProjectionDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IQueryable<LedgerSimulatorGraphPointRecord> query = db.GraphPoints.AsNoTracking()
            .Where(x => x.ProjectId == filter.ProjectId)
            .Where(x => x.PeriodStart >= filter.PeriodStart && x.PeriodStart <= filter.PeriodEnd);

        if (!string.IsNullOrWhiteSpace(filter.ScenarioId))
        {
            query = query.Where(x => x.ScenarioId == filter.ScenarioId);
        }

        if (!string.IsNullOrWhiteSpace(filter.BusinessUnitId))
        {
            query = query.Where(x => x.BusinessUnitId == filter.BusinessUnitId);
        }

        if (filter.AccountAddresses.Count > 0)
        {
            query = query.Where(x => x.AccountAddress != null && filter.AccountAddresses.Contains(x.AccountAddress));
        }

        if (filter.CategoryCodes.Count > 0)
        {
            query = query.Where(x => filter.CategoryCodes.Contains(x.CategoryCode));
        }

        query = filter.Metric switch
        {
            ScenarioGraphMetric.Revenue => query.Where(x => x.FinancialStatementSection == FinancialStatementSection.Revenue.ToString() || x.FinancialStatementSection == FinancialStatementSection.CashReceipts.ToString()),
            ScenarioGraphMetric.Expense => query.Where(x => x.FinancialStatementSection == FinancialStatementSection.Cogs.ToString() || x.FinancialStatementSection == FinancialStatementSection.Opex.ToString() || x.FinancialStatementSection == FinancialStatementSection.Payroll.ToString() || x.FinancialStatementSection == FinancialStatementSection.Capex.ToString() || x.FinancialStatementSection == FinancialStatementSection.Depreciation.ToString()),
            _ => query
        };

        int limit = Math.Clamp(filter.Limit, 1, 5000);
        List<LedgerGraphPoint> points = await query
            .OrderBy(x => x.PeriodStart)
            .ThenBy(x => x.BusinessUnitId)
            .ThenBy(x => x.CategoryCode)
            .Select(x => new LedgerGraphPoint(
                x.PeriodStart,
                x.BusinessUnitId,
                x.AccountAddress,
                x.CategoryCode,
                x.FinancialStatementSection,
                x.PlannedInflow,
                x.PlannedOutflow,
                x.CommittedInflow,
                x.CommittedOutflow,
                x.ActualInflow,
                x.ActualOutflow,
                x.SimulatedInflow,
                x.SimulatedOutflow,
                x.NonCashIncome,
                x.NonCashExpense,
                x.BalanceAfter))
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<LedgerGraphDrilldownRow> drilldown = await query
            .OrderBy(x => x.PeriodStart)
            .ThenBy(x => x.SourceEventId)
            .Select(x => new LedgerGraphDrilldownRow(
                x.PeriodStart,
                x.SourceEventId,
                x.BusinessUnitId,
                x.AccountAddress,
                x.CategoryCode,
                x.FinancialStatementSection,
                x.EventStatus,
                x.PlannedInflow + x.CommittedInflow + x.ActualInflow + x.SimulatedInflow + x.NonCashIncome - x.PlannedOutflow - x.CommittedOutflow - x.ActualOutflow - x.SimulatedOutflow - x.NonCashExpense,
                x.SourceObjectId ?? string.Empty,
                x.ProductOrServiceId ?? x.CounterpartyId ?? x.CategoryCode))
            .Take(Math.Min(limit, 250))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var series = points
            .GroupBy(x => x.CategoryCode, StringComparer.Ordinal)
            .Select(group => new LedgerGraphSeries(group.Key, group.Key, group.ToArray()))
            .ToArray();

        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["inflow"] = points.Sum(x => x.PlannedInflow + x.CommittedInflow + x.ActualInflow + x.SimulatedInflow),
            ["outflow"] = points.Sum(x => x.PlannedOutflow + x.CommittedOutflow + x.ActualOutflow + x.SimulatedOutflow),
            ["nonCashIncome"] = points.Sum(x => x.NonCashIncome),
            ["nonCashExpense"] = points.Sum(x => x.NonCashExpense),
            ["netCashFlow"] = points.Sum(x => x.PlannedInflow + x.CommittedInflow + x.ActualInflow + x.SimulatedInflow - x.PlannedOutflow - x.CommittedOutflow - x.ActualOutflow - x.SimulatedOutflow),
            ["rowCount"] = points.Count
        };

        return new LedgerGraphData(filter, series, drilldown, totals);
    }
}
