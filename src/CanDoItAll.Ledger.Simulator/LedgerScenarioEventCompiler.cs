using System.Globalization;

namespace CanDoItAll.Ledger.Simulator;

public interface ILedgerScenarioEventCompiler
{
    LedgerScenarioEventCompileResult Compile(LedgerScenarioEventCompileRequest request);
}

public sealed class LedgerScenarioEventCompiler(ILedgerSimulatorJsonSerializer serializer) : ILedgerScenarioEventCompiler
{
    public LedgerScenarioEventCompileResult Compile(LedgerScenarioEventCompileRequest request)
    {
        if (request.HorizonEndDate < request.HorizonStartDate)
        {
            throw new ArgumentException("Scenario event horizon end date cannot be before start date.", nameof(request));
        }

        var events = new List<EconomicEventState>();
        var model = request.Model;
        var products = model.ProductSkus.ToDictionary(x => x.ProductSkuId, StringComparer.Ordinal);
        var priceTiers = model.Offerings.SelectMany(x => x.PriceTiers.Select(tier => (Offering: x, Tier: tier))).ToDictionary(x => x.Tier.PriceTierId, StringComparer.Ordinal);
        var contracts = model.CustomerContracts.ToDictionary(x => x.CustomerContractId, StringComparer.Ordinal);
        var paymentTerms = model.PaymentTerms.ToDictionary(x => x.PaymentTermId, StringComparer.Ordinal);
        var boms = model.BillsOfMaterial.ToDictionary(x => x.ProductSkuId, StringComparer.Ordinal);
        var recipes = model.Recipes.ToDictionary(x => x.ProductSkuId, StringComparer.Ordinal);
        var demandSchedules = model.DemandSchedules.ToDictionary(x => x.DemandScheduleId, StringComparer.Ordinal);

        foreach (DateOnly month in LedgerSimulatorPeriods.Months(request.HorizonStartDate, request.HorizonEndDate))
        {
            foreach (DemandScheduleState schedule in model.DemandSchedules)
            {
                decimal quantity = LedgerScenarioQuantities.DemandForMonth(schedule, month);
                if (quantity <= 0m || !contracts.TryGetValue(schedule.CustomerContractId, out CustomerContractState? contract))
                {
                    continue;
                }

                if (products.TryGetValue(schedule.ProductOrServiceId, out ProductSkuState? product))
                {
                    AddProductRevenueAndCost(events, request, contract, paymentTerms[contract.PaymentTermId], product, boms.GetValueOrDefault(product.ProductSkuId), recipes.GetValueOrDefault(product.ProductSkuId), quantity, month);
                    AddProductFlowEvents(events, request, schedule, contract, product, boms.GetValueOrDefault(product.ProductSkuId), recipes.GetValueOrDefault(product.ProductSkuId), quantity, month);
                }
                else if (priceTiers.TryGetValue(schedule.ProductOrServiceId, out var tier))
                {
                    AddOfferingRevenueAndCost(events, request, contract, paymentTerms[contract.PaymentTermId], tier.Offering, tier.Tier, quantity, month);
                }
            }

            foreach (ImplementationProjectState project in model.ImplementationProjects)
            {
                if (!contracts.TryGetValue(project.CustomerContractId, out CustomerContractState? contract))
                {
                    continue;
                }

                PaymentTermState term = paymentTerms[contract.PaymentTermId];
                foreach (MilestoneBillingState milestone in project.Milestones.Where(x => LedgerSimulatorPeriods.MonthStart(x.MilestoneDate) == month))
                {
                    Amount revenue = Amount.FromDecimal(decimal.Round(project.ContractValue.Value * milestone.RevenuePercent, 2));
                    Amount cost = Amount.FromDecimal(decimal.Round(project.DirectCost.Value * milestone.CostPercent, 2));
                    string correlation = $"{project.ImplementationProjectId}:{milestone.MilestoneId}:{LedgerSimulatorPeriods.MonthKey(month)}";
                    AddRevenuePair(events, request, contract, term, contract.RevenueBusinessUnitId, "revenue-implementation-license", revenue, month, correlation, project.ImplementationProjectId, project.ImplementationProjectId, project.DisplayName);
                    events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 3, EconomicEventKind.Cogs, EconomicEventStatus.Planned, EconomicImpactKind.NonCashPnl, contract.RevenueBusinessUnitId, "cogs-delivery", cost, request.Model.BaseCurrencyCode, month, FinancialStatementSection.Cogs, contract.CounterpartyId, contract.CustomerContractId, project.ImplementationProjectId, project.ImplementationProjectId, "Implementation or license direct cost"));
                    AddDirectCostCashPayment(events, request, contract, contract.RevenueBusinessUnitId, correlation, 23, "cogs-delivery", cost, month, project.ImplementationProjectId, project.ImplementationProjectId, "Implementation or license direct cost payment");
                }
            }

            foreach (WorkOrderState workOrder in model.WorkOrders)
            {
                AddWorkOrderEvents(events, request, workOrder, demandSchedules, month);
            }

            foreach (WorkforcePlanState plan in model.WorkforcePlans)
            {
                foreach (DepartmentStaffingState staffing in plan.Departments)
                {
                    decimal fte = LedgerScenarioQuantities.FteForMonth(staffing, month);
                    Amount payroll = Amount.FromDecimal(decimal.Round(fte * staffing.MonthlyGrossSalaryPerFte.Value * (1m + staffing.EmployerBurdenRate), 2));
                    events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{staffing.DepartmentStaffingId}:payroll:{LedgerSimulatorPeriods.MonthKey(month)}", 1, EconomicEventKind.Payroll, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, staffing.BusinessUnitId, "payroll-expense", payroll, model.BaseCurrencyCode, month.AddDays(24), FinancialStatementSection.Payroll, productOrServiceId: staffing.RoleGroup, sourceObjectId: staffing.DepartmentStaffingId, description: $"{staffing.RoleGroup} payroll"));

                    decimal newHires = LedgerScenarioQuantities.NewHireFteForMonth(staffing, month);
                    if (newHires > 0m)
                    {
                        foreach (EquipmentPolicyState policy in plan.EquipmentPolicies.Where(x => x.RoleGroup == "*" || string.Equals(x.RoleGroup, staffing.RoleGroup, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (policy.CapexPerNewHire.Value > 0m)
                            {
                                Amount capex = Amount.FromDecimal(decimal.Round(policy.CapexPerNewHire.Value * newHires, 2));
                                events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{staffing.DepartmentStaffingId}:hire-capex:{LedgerSimulatorPeriods.MonthKey(month)}", 2, EconomicEventKind.Capex, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, staffing.BusinessUnitId, "capex-equipment", capex, model.BaseCurrencyCode, month, FinancialStatementSection.Capex, productOrServiceId: staffing.RoleGroup, sourceObjectId: policy.EquipmentPolicyId, description: "Hiring-triggered equipment CapEx"));
                                if (policy.DepreciationMonths > 0)
                                {
                                    Amount depreciation = Amount.FromDecimal(decimal.Round(capex.Value / policy.DepreciationMonths, 2));
                                    events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{staffing.DepartmentStaffingId}:hire-depreciation:{LedgerSimulatorPeriods.MonthKey(month)}", 3, EconomicEventKind.Depreciation, EconomicEventStatus.Planned, EconomicImpactKind.NonCashPnl, staffing.BusinessUnitId, "depreciation-equipment", depreciation, model.BaseCurrencyCode, month, FinancialStatementSection.Depreciation, productOrServiceId: staffing.RoleGroup, sourceObjectId: policy.EquipmentPolicyId, description: "Hiring equipment depreciation"));
                                }
                            }

                            if (policy.OpexPerNewHire.Value > 0m)
                            {
                                Amount opex = Amount.FromDecimal(decimal.Round(policy.OpexPerNewHire.Value * newHires, 2));
                                events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{staffing.DepartmentStaffingId}:hire-opex:{LedgerSimulatorPeriods.MonthKey(month)}", 4, EconomicEventKind.Expense, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, staffing.BusinessUnitId, "opex-equipment-setup", opex, model.BaseCurrencyCode, month, FinancialStatementSection.Opex, productOrServiceId: staffing.RoleGroup, sourceObjectId: policy.EquipmentPolicyId, description: "Hiring equipment setup OPEX"));
                            }
                        }
                    }
                }
            }

            foreach (ScenarioCostPlanState cost in model.CostPlans.Where(x => LedgerSimulatorPeriods.IsActive(month, x.StartDate, x.EndDate)))
            {
                events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{cost.CostPlanId}:{LedgerSimulatorPeriods.MonthKey(month)}", 1, EconomicEventKind.Expense, EconomicEventStatus.Planned, cost.ImpactKind, cost.BusinessUnitId, cost.CategoryCode, cost.MonthlyAmount, model.BaseCurrencyCode, month.AddDays(cost.PaymentDelayDays), cost.Section, sourceObjectId: cost.CostPlanId, description: cost.CategoryCode));
            }

            foreach (MaintenancePlanState maintenance in model.MaintenancePlans)
            {
                events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{maintenance.MaintenancePlanId}:monthly:{LedgerSimulatorPeriods.MonthKey(month)}", 1, EconomicEventKind.Expense, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, maintenance.BusinessUnitId, "opex-maintenance", maintenance.MonthlyAmount, model.BaseCurrencyCode, month, FinancialStatementSection.Opex, sourceObjectId: maintenance.MaintenancePlanId, description: "Maintenance"));
                if (maintenance.QuarterlyServiceAmount is { Value: > 0m } quarterly && month.Month is 3 or 6 or 9 or 12)
                {
                    events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{maintenance.MaintenancePlanId}:quarterly:{LedgerSimulatorPeriods.MonthKey(month)}", 2, EconomicEventKind.Expense, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, maintenance.BusinessUnitId, "opex-maintenance-service", quarterly, model.BaseCurrencyCode, month, FinancialStatementSection.Opex, sourceObjectId: maintenance.MaintenancePlanId, description: "Quarterly planned service"));
                }

                if (maintenance.AnnualCalibrationAmount is { Value: > 0m } annual && month.Month == 12)
                {
                    events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{maintenance.MaintenancePlanId}:annual:{LedgerSimulatorPeriods.MonthKey(month)}", 3, EconomicEventKind.Expense, EconomicEventStatus.Planned, EconomicImpactKind.CashAndAccrual, maintenance.BusinessUnitId, "opex-calibration", annual, model.BaseCurrencyCode, month, FinancialStatementSection.Opex, sourceObjectId: maintenance.MaintenancePlanId, description: "Annual calibration"));
                }
            }

            foreach (CapexPlanState capex in model.CapexPlans.Where(x => LedgerSimulatorPeriods.MonthStart(x.CashOutflowDate) == month))
            {
                events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{capex.CapexPlanId}:cash:{LedgerSimulatorPeriods.MonthKey(month)}", 1, EconomicEventKind.Capex, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, capex.BusinessUnitId, "capex-production-prep", capex.Amount, model.BaseCurrencyCode, month, FinancialStatementSection.Capex, sourceObjectId: capex.CapexPlanId, description: capex.DisplayName));
                if (capex.DepreciationMonths > 0)
                {
                    events.Add(LedgerScenarioEventFactory.Create(model.ProjectId, request.ScenarioId, $"{capex.CapexPlanId}:depreciation:{LedgerSimulatorPeriods.MonthKey(month)}", 2, EconomicEventKind.Depreciation, EconomicEventStatus.Planned, EconomicImpactKind.NonCashPnl, capex.BusinessUnitId, "depreciation-production-prep", Amount.FromDecimal(decimal.Round(capex.Amount.Value / capex.DepreciationMonths, 2)), model.BaseCurrencyCode, month, FinancialStatementSection.Depreciation, sourceObjectId: capex.CapexPlanId, description: $"{capex.DisplayName} depreciation"));
                }
            }

            foreach (ScenarioFinancialScheduleState schedule in model.FinancialSchedules)
            {
                AddFinancialScheduleEvents(events, request, schedule, month);
            }
        }

        IReadOnlyList<EconomicEventState> ordered = LedgerProjectionService.SortEvents(AttachBusinessUnitAddresses(events, model));
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["eventCount"] = ordered.Count,
            ["revenue"] = ordered.Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly).Sum(x => x.Amount.Value),
            ["cashReceipts"] = ordered.Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.CashOnly).Sum(x => x.Amount.Value),
            ["cogs"] = SumProfitAndLossSection(ordered, FinancialStatementSection.Cogs),
            ["opex"] = SumProfitAndLossSection(ordered, FinancialStatementSection.Opex),
            ["payroll"] = ordered.Where(x => x.Kind == EconomicEventKind.Payroll).Sum(x => x.Amount.Value),
            ["capex"] = ordered.Where(x => x.Kind == EconomicEventKind.Capex).Sum(x => x.Amount.Value),
            ["depreciation"] = ordered.Where(x => x.Kind == EconomicEventKind.Depreciation).Sum(x => x.Amount.Value),
            ["serviceHours"] = ordered.Where(x => x.TokenEffect?.TokenId.Value.Contains(":service-hour:", StringComparison.Ordinal) == true).Sum(x => x.TokenEffect!.Amount.Value),
            ["productFlowUnits"] = ordered.Where(x => x.TokenEffect?.TokenId.Value.Contains(":product-unit:", StringComparison.Ordinal) == true).Sum(x => x.TokenEffect!.Amount.Value),
            ["financialScheduleOutflows"] = ordered
                .Where(x => x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.FinancialScheduleId) && IsOutflow(x))
                .Where(x => x.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual)
                .Sum(x => x.Amount.Value),
            ["financialScheduleCashInflows"] = ordered
                .Where(x => x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.FinancialScheduleId) && !IsOutflow(x))
                .Where(x => x.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual)
                .Sum(x => x.Amount.Value)
        };

        return new LedgerScenarioEventCompileResult(model, ordered, totals, serializer.HashJson(ordered));
    }

    private static void AddOfferingRevenueAndCost(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, CustomerContractState contract, PaymentTermState term, OfferingState offering, PriceTierState tier, decimal quantity, DateOnly month)
    {
        Amount revenue = Amount.FromDecimal(decimal.Round(quantity * tier.UnitPrice.Value, 2));
        Amount directCost = Amount.FromDecimal(decimal.Round(quantity * tier.UnitCost.Value, 2));
        string correlation = $"{contract.CustomerContractId}:{tier.PriceTierId}:{LedgerSimulatorPeriods.MonthKey(month)}";
        AddRevenuePair(events, request, contract, term, contract.RevenueBusinessUnitId, offering.RevenueCategoryCode, revenue, month, correlation, tier.PriceTierId, offering.OfferingId, offering.DisplayName);
        if (directCost.Value > 0m)
        {
            events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 3, EconomicEventKind.Cogs, EconomicEventStatus.Planned, EconomicImpactKind.NonCashPnl, contract.RevenueBusinessUnitId, offering.DirectCostCategoryCode, directCost, request.Model.BaseCurrencyCode, month, FinancialStatementSection.Cogs, contract.CounterpartyId, contract.CustomerContractId, tier.PriceTierId, offering.OfferingId, $"{offering.DisplayName} direct cost"));
            AddDirectCostCashPayment(events, request, contract, contract.RevenueBusinessUnitId, correlation, 23, offering.DirectCostCategoryCode, directCost, month, tier.PriceTierId, offering.OfferingId, $"{offering.DisplayName} direct cost payment");
        }
    }

    private static void AddFinancialScheduleEvents(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, ScenarioFinancialScheduleState schedule, DateOnly month)
    {
        if (!schedule.PostsToSimulation || (schedule.RequiresApproval && !IsApproved(schedule.Approval)))
        {
            return;
        }

        ScenarioFinancialScheduleAmountState[] amounts = schedule.Amounts
            .Where(x => LedgerSimulatorPeriods.MonthStart(x.EconomicDate) == month)
            .Where(x => x.Amount.Value != 0m)
            .OrderBy(x => x.EconomicDate)
            .ThenBy(PeriodCodeFor, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < amounts.Length; index++)
        {
            ScenarioFinancialScheduleAmountState amount = amounts[index];
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LedgerScenarioMetadataKeys.FinancialScheduleId] = schedule.ScheduleId,
                [LedgerScenarioMetadataKeys.FinancialSchedulePeriod] = PeriodCodeFor(amount),
                ["financialScheduleKind"] = schedule.Kind.ToString()
            };
            if (!string.IsNullOrWhiteSpace(schedule.ParentScheduleId))
            {
                metadata[LedgerScenarioMetadataKeys.ParentFinancialScheduleId] = schedule.ParentScheduleId!;
            }

            if (schedule.Approval is not null)
            {
                metadata[LedgerScenarioMetadataKeys.ApprovalStatus] = schedule.Approval.Status.ToString();
                metadata[LedgerScenarioMetadataKeys.ApprovalScope] = schedule.Approval.RequiredScope.ToString();
                metadata["approvalLimit"] = schedule.Approval.SelfApprovalLimit.Value.ToString("0.##", CultureInfo.InvariantCulture);
                metadata["approvingBusinessUnitId"] = schedule.Approval.ApprovingBusinessUnitId.Value;
            }

            foreach (KeyValuePair<string, string> item in schedule.Metadata)
            {
                metadata[$"schedule.{item.Key}"] = item.Value;
            }

            foreach (KeyValuePair<string, string> item in amount.Metadata)
            {
                metadata[$"amount.{item.Key}"] = item.Value;
            }

            events.Add(LedgerScenarioEventFactory.Create(
                request.Model.ProjectId,
                request.ScenarioId,
                $"{schedule.ScheduleId}:{LedgerSimulatorPeriods.MonthKey(amount.EconomicDate)}",
                200 + index,
                schedule.EventKind,
                StatusForSchedule(schedule),
                schedule.ImpactKind,
                schedule.BusinessUnitId,
                schedule.CategoryCode,
                amount.Amount,
                request.Model.BaseCurrencyCode,
                amount.EconomicDate,
                schedule.Section,
                sourceObjectId: schedule.ScheduleId,
                description: schedule.DisplayName,
                additionalMetadata: metadata));
        }
    }

    private static string PeriodCodeFor(ScenarioFinancialScheduleAmountState amount)
        => string.IsNullOrWhiteSpace(amount.PeriodCode)
            ? LedgerSimulatorPeriods.MonthKey(amount.EconomicDate)
            : amount.PeriodCode;

    private static bool IsApproved(ScenarioApprovalState? approval)
        => approval is null ||
            approval.Status is ScenarioApprovalStatus.NotRequired
                or ScenarioApprovalStatus.BusinessUnitApproved
                or ScenarioApprovalStatus.ManagementApproved
                or ScenarioApprovalStatus.Dispatched;

    private static EconomicEventStatus StatusForSchedule(ScenarioFinancialScheduleState schedule)
    {
        if (schedule.EventKind == EconomicEventKind.Revenue && schedule.ImpactKind == EconomicImpactKind.AccrualOnly)
        {
            return EconomicEventStatus.Invoiced;
        }

        return schedule.RequiresApproval ? EconomicEventStatus.Committed : EconomicEventStatus.Planned;
    }

    private static void AddProductRevenueAndCost(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, CustomerContractState contract, PaymentTermState term, ProductSkuState product, BillOfMaterialState? bom, RecipeState? recipe, decimal quantity, DateOnly month)
    {
        Amount revenue = Amount.FromDecimal(decimal.Round(quantity * product.UnitPrice.Value, 2));
        Amount unitCost = ResolveProductUnitCost(product, bom, recipe);
        Amount cogs = Amount.FromDecimal(decimal.Round(quantity * unitCost.Value, 2));
        DateOnly supplierPaymentDate = month.AddMonths(1).AddDays(GetSupplierPaymentDelayDays(bom, recipe));
        string correlation = $"{contract.CustomerContractId}:{product.ProductSkuId}:{LedgerSimulatorPeriods.MonthKey(month)}";
        AddRevenuePair(events, request, contract, term, contract.RevenueBusinessUnitId, product.RevenueCategoryCode, revenue, month, correlation, product.ProductSkuId, product.ProductSkuId, product.DisplayName);
        events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 3, EconomicEventKind.InventoryPurchase, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, contract.RevenueBusinessUnitId, "inventory-purchase", cogs, request.Model.BaseCurrencyCode, supplierPaymentDate, FinancialStatementSection.Inventory, contract.CounterpartyId, contract.CustomerContractId, product.ProductSkuId, product.ProductSkuId, $"{product.DisplayName} inventory supplier payment"));
        events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 4, EconomicEventKind.InventoryConsumption, EconomicEventStatus.Planned, EconomicImpactKind.ControlTokenOnly, contract.RevenueBusinessUnitId, "inventory-consumption", cogs, request.Model.BaseCurrencyCode, month, FinancialStatementSection.Inventory, contract.CounterpartyId, contract.CustomerContractId, product.ProductSkuId, product.ProductSkuId, $"{product.DisplayName} inventory consumption"));
        events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 5, EconomicEventKind.Cogs, EconomicEventStatus.Planned, EconomicImpactKind.NonCashPnl, contract.RevenueBusinessUnitId, product.CogsCategoryCode, cogs, request.Model.BaseCurrencyCode, month, FinancialStatementSection.Cogs, contract.CounterpartyId, contract.CustomerContractId, product.ProductSkuId, product.ProductSkuId, $"{product.DisplayName} COGS"));
        if (bom is not null)
        {
            events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 6, EconomicEventKind.Transfer, EconomicEventStatus.Planned, EconomicImpactKind.ControlTokenOnly, contract.RevenueBusinessUnitId, "wip-movement", Amount.FromDecimal(decimal.Round(cogs.Value * 0.45m, 2)), request.Model.BaseCurrencyCode, month, FinancialStatementSection.Wip, contract.CounterpartyId, contract.CustomerContractId, product.ProductSkuId, product.ProductSkuId, $"{product.DisplayName} WIP movement"));
        }
    }

    private static void AddWorkOrderEvents(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, WorkOrderState workOrder, IReadOnlyDictionary<string, DemandScheduleState> demandSchedules, DateOnly month)
    {
        if (!LedgerSimulatorPeriods.IsActive(month, workOrder.StartDate, workOrder.EndDate) ||
            !LedgerScenarioQuantities.IsCadenceMonth(workOrder.Cadence, workOrder.StartDate, month))
        {
            return;
        }

        decimal demandQuantity = 0m;
        if (!string.IsNullOrWhiteSpace(workOrder.DemandScheduleId) && demandSchedules.TryGetValue(workOrder.DemandScheduleId, out DemandScheduleState? demandSchedule))
        {
            demandQuantity = LedgerScenarioQuantities.DemandForMonth(demandSchedule, month);
        }

        decimal overrideHours = workOrder.DeliveryOverrides
            .Where(x => LedgerSimulatorPeriods.MonthStart(x.DeliveryDate) == month)
            .Sum(x => x.Hours);
        decimal totalHours = decimal.Round(workOrder.FixedHoursPerCadence + (demandQuantity * workOrder.HoursPerDemandUnit) + overrideHours, 4);
        if (totalHours <= 0m)
        {
            return;
        }

        IReadOnlyList<WorkAllocationRuleState> allocations = workOrder.AllocationRules.Count == 0
            ? [new WorkAllocationRuleState("default-delivery", workOrder.DeliveryBusinessUnitId, 1m, "default-delivery", new Dictionary<string, string>())]
            : workOrder.AllocationRules;
        string period = LedgerSimulatorPeriods.MonthKey(month);
        string deliveryId = $"workdel:{LedgerSimulatorIdentifiers.NormalizeSegment(workOrder.WorkOrderId)}:{period}";
        string invoiceId = string.Equals(workOrder.InvoicePolicyCode, "internal-capacity-allocation", StringComparison.Ordinal)
            ? string.Empty
            : $"invoice:{LedgerSimulatorIdentifiers.NormalizeSegment(workOrder.ProjectId)}:{LedgerSimulatorIdentifiers.NormalizeSegment(workOrder.WorkOrderId)}:{period}";

        for (int index = 0; index < allocations.Count; index++)
        {
            WorkAllocationRuleState allocation = allocations[index];
            decimal hours = decimal.Round(totalHours * allocation.AllocationPercent, 4);
            if (hours <= 0m)
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LedgerScenarioMetadataKeys.WorkOrderId] = workOrder.WorkOrderId,
                [LedgerScenarioMetadataKeys.WorkAllocationRuleId] = allocation.WorkAllocationRuleId,
                [LedgerScenarioMetadataKeys.WorkDeliveryId] = deliveryId,
                [LedgerScenarioMetadataKeys.StrategyCode] = allocation.StrategyCode,
                [LedgerScenarioMetadataKeys.Quantity] = hours.ToString("0.####", CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(workOrder.DemandScheduleId))
            {
                metadata[LedgerScenarioMetadataKeys.DemandScheduleId] = workOrder.DemandScheduleId;
            }

            if (!string.IsNullOrWhiteSpace(invoiceId))
            {
                metadata[LedgerScenarioMetadataKeys.InvoiceId] = invoiceId;
            }

            events.Add(LedgerScenarioEventFactory.Create(
                request.Model.ProjectId,
                request.ScenarioId,
                $"{workOrder.WorkOrderId}:workdone:{period}",
                100 + index,
                EconomicEventKind.Transfer,
                EconomicEventStatus.Planned,
                EconomicImpactKind.ControlTokenOnly,
                allocation.WorkSourceBusinessUnitId,
                "workdone-service-hours",
                Amount.FromDecimal(hours),
                request.Model.BaseCurrencyCode,
                month.AddDays(20),
                FinancialStatementSection.Transfer,
                null,
                workOrder.CustomerContractId,
                workOrder.ProductOrServiceId,
                workOrder.WorkOrderId,
                $"{workOrder.DisplayName} delivered work hours",
                tokenEffect: new TokenTransfer(LedgerSimulatorTokenIdFactory.ServiceHour(request.Model.ProjectId, workOrder.WorkOrderId, period), Amount.FromDecimal(hours)),
                destinationBusinessUnitId: workOrder.DeliveryBusinessUnitId,
                additionalMetadata: metadata));
        }
    }

    private static void AddProductFlowEvents(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, DemandScheduleState schedule, CustomerContractState contract, ProductSkuState product, BillOfMaterialState? bom, RecipeState? recipe, decimal quantity, DateOnly month)
    {
        Amount unitCost = ResolveProductUnitCost(product, bom, recipe);
        ProductFlowPlanState[] plans = request.Model.ProductFlowPlans
            .Where(x => string.Equals(x.ProductSkuId, product.ProductSkuId, StringComparison.Ordinal))
            .Where(x => string.IsNullOrWhiteSpace(x.DemandScheduleId) || string.Equals(x.DemandScheduleId, schedule.DemandScheduleId, StringComparison.Ordinal))
            .ToArray();
        if (plans.Length == 0)
        {
            return;
        }

        string period = LedgerSimulatorPeriods.MonthKey(month);
        foreach (ProductFlowPlanState plan in plans)
        {
            for (int index = 0; index < plan.Steps.Count; index++)
            {
                ProductFlowStepState step = plan.Steps[index];
                decimal units = decimal.Round(quantity * plan.GoodUnitsPerDemandUnit * step.QuantityMultiplier, 4);
                if (units <= 0m)
                {
                    continue;
                }

                Amount stepValue = Amount.FromDecimal(decimal.Round(quantity * unitCost.Value * step.ValuePercent, 2));
                FinancialStatementSection section = step.Kind switch
                {
                    ScenarioProductFlowStepKind.MaterialIssue => FinancialStatementSection.Inventory,
                    ScenarioProductFlowStepKind.CustomerDelivery => FinancialStatementSection.Inventory,
                    _ => FinancialStatementSection.Wip
                };
                var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [LedgerScenarioMetadataKeys.ProductFlowPlanId] = plan.ProductFlowPlanId,
                    [LedgerScenarioMetadataKeys.ProductFlowStepId] = step.ProductFlowStepId,
                    [LedgerScenarioMetadataKeys.DemandScheduleId] = schedule.DemandScheduleId,
                    [LedgerScenarioMetadataKeys.FlowStepKind] = step.Kind.ToString(),
                    [LedgerScenarioMetadataKeys.Quantity] = units.ToString("0.####", CultureInfo.InvariantCulture),
                    ["fromLocationId"] = step.FromLocationId,
                    ["toLocationId"] = step.ToLocationId
                };

                events.Add(LedgerScenarioEventFactory.Create(
                    request.Model.ProjectId,
                    request.ScenarioId,
                    $"{plan.ProductFlowPlanId}:{schedule.DemandScheduleId}:flow:{period}",
                    140 + index,
                    EconomicEventKind.Transfer,
                    EconomicEventStatus.Planned,
                    EconomicImpactKind.ControlTokenOnly,
                    step.BusinessUnitId,
                    $"product-flow-{LedgerSimulatorIdentifiers.NormalizeSegment(step.Kind.ToString())}",
                    stepValue,
                    request.Model.BaseCurrencyCode,
                    month.AddDays(Math.Min(24, 3 + index)),
                    section,
                    contract.CounterpartyId,
                    contract.CustomerContractId,
                    product.ProductSkuId,
                    plan.ProductFlowPlanId,
                    step.Description,
                    tokenEffect: new TokenTransfer(LedgerSimulatorTokenIdFactory.ProductUnit(request.Model.ProjectId, product.ProductSkuId, plan.ProductFlowPlanId, period), Amount.FromDecimal(units)),
                    additionalMetadata: metadata));
            }
        }
    }

    private static Amount ResolveProductUnitCost(ProductSkuState product, BillOfMaterialState? bom, RecipeState? recipe)
    {
        if (bom is not null && bom.StandardYieldRate > 0m)
        {
            return Amount.FromDecimal(decimal.Round(bom.Lines.Sum(x => x.UnitCost.Value) / bom.StandardYieldRate, 2));
        }

        if (recipe is not null && recipe.SpoilageRate > 0m)
        {
            return Amount.FromDecimal(decimal.Round(product.StandardUnitCost.Value / (1m - recipe.SpoilageRate), 2));
        }

        return product.StandardUnitCost;
    }

    private static int GetSupplierPaymentDelayDays(BillOfMaterialState? bom, RecipeState? recipe)
    {
        if (bom is not null && bom.Lines.Count > 0)
        {
            decimal totalCost = bom.Lines.Sum(x => x.UnitCost.Value);
            if (totalCost > 0m)
            {
                decimal weightedDays = bom.Lines.Sum(x => x.UnitCost.Value * x.SupplierPaymentTermDays) / totalCost;
                return (int)Math.Round(weightedDays, MidpointRounding.AwayFromZero);
            }
        }

        return recipe is null ? 0 : 14;
    }

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

    private static void AddDirectCostCashPayment(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, CustomerContractState contract, BusinessUnitId unit, string correlation, int sequence, string category, Amount amount, DateOnly month, string productOrServiceId, string sourceObjectId, string description)
    {
        if (amount.Value <= 0m)
        {
            return;
        }

        DateOnly paymentDate = month.AddMonths(1).AddDays(14);
        events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, sequence, EconomicEventKind.Expense, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, unit, category, amount, request.Model.BaseCurrencyCode, paymentDate, FinancialStatementSection.Cogs, contract.CounterpartyId, contract.CustomerContractId, productOrServiceId, sourceObjectId, description));
    }

    private static decimal SumProfitAndLossSection(IReadOnlyList<EconomicEventState> events, FinancialStatementSection section)
        => events
            .Where(x => x.Metadata.TryGetValue(LedgerScenarioMetadataKeys.FinancialStatementSection, out string? value) && string.Equals(value, section.ToString(), StringComparison.Ordinal))
            .Where(x => x.ImpactKind is EconomicImpactKind.CashAndAccrual or EconomicImpactKind.AccrualOnly or EconomicImpactKind.NonCashPnl)
            .Sum(x => x.Amount.Value);

    private static void AddRevenuePair(List<EconomicEventState> events, LedgerScenarioEventCompileRequest request, CustomerContractState contract, PaymentTermState term, BusinessUnitId unit, string category, Amount revenue, DateOnly month, string correlation, string productOrServiceId, string sourceObjectId, string description)
    {
        events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 1, EconomicEventKind.Revenue, EconomicEventStatus.Invoiced, EconomicImpactKind.AccrualOnly, unit, category, revenue, request.Model.BaseCurrencyCode, month, FinancialStatementSection.Revenue, contract.CounterpartyId, contract.CustomerContractId, productOrServiceId, sourceObjectId, description));
        decimal samePeriodRate = Math.Clamp(term.SamePeriodCollectionRate, 0m, 1m);
        Amount samePeriodReceipt = Amount.FromDecimal(decimal.Round(revenue.Value * samePeriodRate, 2));
        Amount deferredReceipt = Amount.FromDecimal(decimal.Round(revenue.Value - samePeriodReceipt.Value, 2));

        if (samePeriodReceipt.Value > 0m)
        {
            events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 2, EconomicEventKind.Revenue, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, unit, "cash-receipts", samePeriodReceipt, request.Model.BaseCurrencyCode, month, FinancialStatementSection.CashReceipts, contract.CounterpartyId, contract.CustomerContractId, productOrServiceId, sourceObjectId, $"{description} same-period cash receipt"));
        }

        if (deferredReceipt.Value > 0m)
        {
            DateOnly deferredCollectionDate = month.AddMonths(1).AddDays(Math.Max(0, term.DueDays) + Math.Max(0, term.ExtraCollectionDelayDays));
            events.Add(LedgerScenarioEventFactory.Create(request.Model.ProjectId, request.ScenarioId, correlation, 22, EconomicEventKind.Revenue, EconomicEventStatus.Planned, EconomicImpactKind.CashOnly, unit, "cash-receipts", deferredReceipt, request.Model.BaseCurrencyCode, deferredCollectionDate, FinancialStatementSection.CashReceipts, contract.CounterpartyId, contract.CustomerContractId, productOrServiceId, sourceObjectId, $"{description} deferred cash receipt"));
        }
    }

    private static IReadOnlyList<EconomicEventState> AttachBusinessUnitAddresses(IReadOnlyList<EconomicEventState> events, ScenarioBusinessModelState model)
    {
        Dictionary<BusinessUnitId, Address> addresses = model.BusinessUnits.ToDictionary(x => x.BusinessUnitId, x => x.PrimaryAddress);
        return events
            .Select(eventState =>
            {
                if (!addresses.TryGetValue(eventState.SourceBusinessUnitId, out Address address))
                {
                    return eventState;
                }

                var metadata = new Dictionary<string, string>(eventState.Metadata, StringComparer.Ordinal)
                {
                    [LedgerScenarioMetadataKeys.AccountAddress] = address.Value
                };

                return eventState with { SourceAddress = eventState.SourceAddress ?? address, Metadata = metadata };
            })
            .ToArray();
    }
}
