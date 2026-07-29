using CanDoItAll.Ledger.Simulator;
using CanDoItAll.Ledger.Simulator.Persistence.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class LedgerSimulatorTests
{
    private static readonly Address TreasuryAddress = Address.FromPublicKey("treasury-public"u8.ToArray());
    private static readonly BusinessUnitId TreasuryBu = LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "treasury");
    private static readonly IReadOnlyDictionary<string, GoldenScenarioExpectation> GoldenScenarioExpectations =
        new Dictionary<string, GoldenScenarioExpectation>(StringComparer.Ordinal)
        {
            ["S1"] = new(
                "S1",
                EventCount: 893,
                GraphRows: 887,
                ScheduleHash: "98e98a824848c64a7c230a2e5763da0bfd7cf09be3a0dd44bf35385f0444e7ec",
                Revenue: 2715648.69m,
                CashReceiptsInHorizon: 2656206.87m,
                EndingAccountsReceivable: 59441.82m,
                EndingAccountsPayableProxy: 0m,
                Cogs: 607708.52m,
                Opex: 529560m,
                Payroll: 2716125m,
                Capex: 12600m,
                Depreciation: 350m,
                InventoryPurchases: 0m,
                WipMovement: 0m,
                ClosingCash: -1012800.59m,
                MinimumCash: -1069541.73m,
                DeficitCount: 441,
                BaselineRevenue: 2686955.60m,
                BaselineGrossMargin: 0.757974m,
                BaselineEndingAccountsReceivable: 70311.18m,
                RevenueTolerancePercent: 0.05m,
                GrossMarginTolerancePoints: 0.075m,
                WorkingCapitalTolerancePercent: 0.20m,
                DsoMinimumDays: 6m,
                DsoMaximumDays: 11m,
                ClosingCashShouldBeNegative: true,
                EbitdaShouldBeNegative: true),
            ["S2"] = new(
                "S2",
                EventCount: 1248,
                GraphRows: 1238,
                ScheduleHash: "f034c91c57359de6417cad45fa86b8f7f139fafac3656f8ef6d15467b696c517",
                Revenue: 1019848.57m,
                CashReceiptsInHorizon: 1010224.35m,
                EndingAccountsReceivable: 9624.22m,
                EndingAccountsPayableProxy: 7322.45m,
                Cogs: 172799.07m,
                Opex: 135600m,
                Payroll: 288600m,
                Capex: 0m,
                Depreciation: 0m,
                InventoryPurchases: 172799.07m,
                WipMovement: 0m,
                ClosingCash: 445547.73m,
                MinimumCash: 19350m,
                DeficitCount: 0,
                BaselineRevenue: 1028002.94m,
                BaselineGrossMargin: 0.899658m,
                BaselineEndingAccountsReceivable: 10724.69m,
                RevenueTolerancePercent: 0.05m,
                GrossMarginTolerancePoints: 0.075m,
                WorkingCapitalTolerancePercent: 0.65m,
                DsoMinimumDays: 2m,
                DsoMaximumDays: 5m,
                ClosingCashShouldBeNegative: false,
                EbitdaShouldBeNegative: false),
            ["S3"] = new(
                "S3",
                EventCount: 1561,
                GraphRows: 1541,
                ScheduleHash: "6a85a8b58a396fa0a25e74186f349ca998e6b64e2b8ac5cc7b7078a0d1f4a607",
                Revenue: 16176696.25m,
                CashReceiptsInHorizon: 14256117.75m,
                EndingAccountsReceivable: 1920578.50m,
                EndingAccountsPayableProxy: 1150785.50m,
                Cogs: 10036598.98m,
                Opex: 3880000m,
                Payroll: 15808320m,
                Capex: 990000m,
                Depreciation: 16500m,
                InventoryPurchases: 8976598.98m,
                WipMovement: 4039469.50m,
                ClosingCash: -14108015.73m,
                MinimumCash: -14853774.98m,
                DeficitCount: 552,
                BaselineRevenue: 16306375.50m,
                BaselineGrossMargin: 0.381188m,
                BaselineEndingAccountsReceivable: 2019821.70m,
                RevenueTolerancePercent: 0.05m,
                GrossMarginTolerancePoints: 0.01m,
                WorkingCapitalTolerancePercent: 0.10m,
                DsoMinimumDays: 35m,
                DsoMaximumDays: 50m,
                ClosingCashShouldBeNegative: true,
                EbitdaShouldBeNegative: true)
        };

    [Fact]
    public void Serializer_CanonicalizesObjectTypesAndDictionaries()
    {
        var serializer = new LedgerSimulatorJsonSerializer();
        var state = new LedgerSimulatorModelState(
            "project-alpha",
            "Project Alpha",
            LedgerSimulatorSchema.Version,
            "USD",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            "gregorian",
            null,
            null,
            null,
            TreasuryBu,
            LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "external-clearing"),
            new[] { TreasuryBu },
            new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" },
            new Dictionary<string, string>());

        string json = serializer.Serialize(state);
        LedgerSimulatorBusinessObjectCommandDraft draft = serializer.CreateDraft(LedgerSimulatorObjectTypes.Model, state);

        Assert.Contains(LedgerSimulatorObjectTypes.Model, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Equal(LedgerSimulatorObjectTypes.Model, draft.ObjectType);
        Assert.True(json.IndexOf("\"a\"", StringComparison.Ordinal) < json.IndexOf("\"z\"", StringComparison.Ordinal));
        Assert.Equal(serializer.HashJson(state), serializer.HashJson(state));
    }

    [Fact]
    public void TokenFactory_NormalizesAndRejectsInvalidIds()
    {
        TokenId budget = LedgerSimulatorTokenIdFactory.Budget("Project Alpha", "Sales / Enterprise", "2026-Q1", "Travel & Events");

        Assert.Equal("es:project-alpha:budget:sales-enterprise:2026-q1:travel-events", budget.Value);
        Assert.Throws<ArgumentException>(() => new BusinessUnitId("not-a-bu"));
    }

    [Fact]
    public async Task TemplateMaterializer_ProducesBusinessUnitAndAddressBindingDrafts()
    {
        var provider = new LedgerSimulatorTemplateProvider();
        var serializer = new LedgerSimulatorJsonSerializer();
        var materializer = new LedgerSimulatorTemplateMaterializer(provider, serializer);

        IReadOnlyList<LedgerSimulatorTemplateState> templates = provider.GetTemplates();
        Assert.Contains(templates, x => x.TemplateId == "common.project-treasury.v1");
        Assert.Contains(templates, x => x.TemplateId == "dept.marketing.v1");
        Assert.Contains(templates, x => x.TemplateId == "model.hybrid-service-product.v1");

        LedgerSimulatorMaterializationPlan plan = await materializer.BuildPlanAsync(
            new LedgerSimulatorMaterializationRequest(
                "project-alpha",
                "dept.marketing.v1",
                TreasuryAddress,
                new Dictionary<string, string> { ["path"] = "marketing", ["displayName"] = "Marketing" }),
            CancellationToken.None);

        Assert.Equal(2, plan.CommandDrafts.Count);
        Assert.Contains(plan.CommandDrafts, x => x.ObjectType == LedgerSimulatorObjectTypes.BusinessUnit);
        Assert.Contains(plan.CommandDrafts, x => x.ObjectType == LedgerSimulatorObjectTypes.BusinessUnitAddressBinding);
    }

    [Fact]
    public void Generators_CreateRevenueCostPayrollTaxAndDepreciationEvents()
    {
        var generator = new EconomicEventGenerator();
        BusinessUnitId sales = LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "sales");
        var revenue = new RevenueStreamState("project-alpha", "sku-sales", RevenueStreamKind.Product, sales, "SKU sales", 100m, 20m, 10, "monthly", 15, 0.9m, new Dictionary<string, string>());
        var cost = new CostPolicyState("project-alpha", "cloud", sales, "cloud", Amount.FromInt64(250), "monthly", EconomicImpactKind.CashOnly, 5, new Dictionary<string, string>());

        IReadOnlyList<EconomicEventState> events = generator.Generate(new EconomicEventGenerationRequest("project-alpha", revenue, cost, new DateOnly(2026, 1, 1), 2, "USD"));
        IReadOnlyList<EconomicEventState> payroll = new GenericPayrollRuleProvider().GeneratePayrollEvents(
            new PayrollPlanState("project-alpha", "payroll-default", sales, "generic-payroll.v1", Amount.FromInt64(1000), 0.2m, 0.12m, "monthly", 25, new Dictionary<string, string>()),
            new DateOnly(2026, 1, 1));
        IReadOnlyList<EconomicEventState> tax = new GenericTaxRuleProvider().GenerateTaxEvents(
            new TaxRuleSetState("project-alpha", "sales-tax", "US", "sales-tax", 0.1m, 30, LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "tax"), new Dictionary<string, string>()),
            Amount.FromInt64(1000),
            sales,
            new DateOnly(2026, 1, 31));
        var asset = new AssetState("project-alpha", "laptop-fleet", sales, "Laptop fleet", Amount.FromInt64(1200), new DateOnly(2026, 1, 1), 12, Amount.Zero, new Dictionary<string, string>());
        IDepreciationRuleProvider depreciation = new GenericDepreciationRuleProvider();
        DepreciationScheduleState schedule = depreciation.BuildSchedule(asset, DepreciationMethod.StraightLine);

        Assert.Contains(events, x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly);
        Assert.Contains(events, x => x.Kind == EconomicEventKind.Cogs);
        Assert.Contains(payroll, x => x.Kind == EconomicEventKind.EmployeeWithholding);
        Assert.Contains(tax, x => x.Kind == EconomicEventKind.Tax && x.ImpactKind == EconomicImpactKind.CashOnly);
        Assert.Equal(Amount.FromInt64(100), schedule.PeriodAmount);
        Assert.All(depreciation.GenerateDepreciationEvents(schedule, asset), x => Assert.Equal(EconomicImpactKind.NonCashPnl, x.ImpactKind));
    }

    [Fact]
    public void ScenarioTemplateCatalog_BuildsIndustryModelsAndRegistersScenarioObjectTypes()
    {
        var catalog = new LedgerScenarioTemplateCatalog();
        IReadOnlyList<ScenarioBusinessModelState> scenarios = catalog.GetScenarios();

        Assert.Collection(
            scenarios.OrderBy(x => x.ScenarioCode, StringComparer.Ordinal),
            s1 =>
            {
                Assert.Equal("S1", s1.ScenarioCode);
                Assert.NotEmpty(s1.Offerings);
                Assert.NotEmpty(s1.ImplementationProjects);
                Assert.NotEmpty(s1.WorkforcePlans);
                Assert.NotEmpty(s1.WorkOrders);
            },
            s2 =>
            {
                Assert.Equal("S2", s2.ScenarioCode);
                Assert.NotEmpty(s2.ProductSkus);
                Assert.NotEmpty(s2.Recipes);
                Assert.NotEmpty(s2.InventoryLocations);
                Assert.NotEmpty(s2.ProductFlowPlans);
            },
            s3 =>
            {
                Assert.Equal("S3", s3.ScenarioCode);
                Assert.NotEmpty(s3.BillsOfMaterial);
                Assert.NotEmpty(s3.ProductionRoutes);
                Assert.NotEmpty(s3.WorkOrders);
                Assert.NotEmpty(s3.ProductFlowPlans);
                Assert.NotEmpty(s3.CapexPlans);
                Assert.NotEmpty(s3.QualityGates);
            },
            vef =>
            {
                Assert.Equal("VEF", vef.ScenarioCode);
                Assert.NotEmpty(vef.ProductSkus);
                Assert.NotEmpty(vef.FinancialSchedules);
                Assert.Contains(vef.FinancialSchedules, schedule => schedule.RequiresApproval);
                Assert.Contains(vef.FinancialSchedules, schedule => schedule.Metadata.TryGetValue("assemblySource", out string? source) && source == "vef-financial-plan-assembly");
                Assert.DoesNotContain(vef.FinancialSchedules, schedule => schedule.Metadata.ContainsKey("sourceWorkbook"));
            });
        Assert.Contains(LedgerSimulatorObjectTypes.PaymentTerm, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.CustomerContract, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.ProductSku, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.WorkforcePlan, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.ProductionRoute, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.WorkOrder, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.WorkAllocationRule, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.WorkDelivery, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.ProductFlowPlan, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.ProductFlowStep, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
        Assert.Contains(LedgerSimulatorObjectTypes.PlannedFinancialSchedule, LedgerSimulatorObjectTypeCatalog.Instance.ObjectTypes);
    }

    [Fact]
    public void ScenarioEventCompiler_GeneratesDeterministicTypedSchedulesForAllScenarios()
    {
        var serializer = new LedgerSimulatorJsonSerializer();
        var compiler = new LedgerScenarioEventCompiler(serializer);
        var catalog = new LedgerScenarioTemplateCatalog();

        Dictionary<string, LedgerScenarioEventCompileResult> compiled = catalog.GetScenarios()
            .ToDictionary(
                x => x.ScenarioCode,
                x => compiler.Compile(new LedgerScenarioEventCompileRequest(x, new ScenarioId($"baseline-{x.ScenarioCode.ToLowerInvariant()}"), x.PlanningStartDate, x.PlanningEndDate)),
                StringComparer.Ordinal);

        Assert.All(compiled.Values, result =>
        {
            LedgerScenarioEventCompileResult second = compiler.Compile(new LedgerScenarioEventCompileRequest(result.Model, new ScenarioId($"baseline-{result.Model.ScenarioCode.ToLowerInvariant()}"), result.Model.PlanningStartDate, result.Model.PlanningEndDate));
            Assert.Equal(result.ScheduleHash, second.ScheduleHash);
            Assert.NotEmpty(result.ScheduleHash);
            Assert.True(result.ExpectedTotals["eventCount"] > 0m);
            Assert.True(result.ExpectedTotals["revenue"] > 0m);
            Assert.True(result.ExpectedTotals["cashReceipts"] > 0m);
            Assert.True(result.Events.All(x => x.SourceAddress is not null));
            Assert.True(result.Events.All(x => x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.AccountAddress)));
        });

        Assert.Contains(compiled["S1"].Events, x => x.Kind == EconomicEventKind.Revenue && x.CategoryCode == "revenue-subscription");
        Assert.Contains(compiled["S1"].Events, x => x.Kind == EconomicEventKind.Payroll);
        Assert.Contains(compiled["S1"].Events, x => x.Kind == EconomicEventKind.Capex && x.CategoryCode == "capex-equipment");
        Assert.Contains(compiled["S1"].Events, x =>
            x.CategoryCode == "workdone-service-hours" &&
            x.TokenEffect?.TokenId.Value.Contains(":service-hour:", StringComparison.Ordinal) == true &&
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.WorkOrderId) &&
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.InvoiceId));
        Assert.Contains(compiled["S2"].Events, x => x.Kind == EconomicEventKind.InventoryPurchase);
        Assert.Contains(compiled["S2"].Events, x => x.Kind == EconomicEventKind.InventoryConsumption);
        Assert.Contains(compiled["S2"].Events, x =>
            x.CategoryCode.StartsWith("product-flow-", StringComparison.Ordinal) &&
            x.TokenEffect?.TokenId.Value.Contains(":product-unit:", StringComparison.Ordinal) == true &&
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.ProductFlowPlanId) &&
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.ProductFlowStepId));
        Assert.Contains(compiled["S3"].Events, x => x.Kind == EconomicEventKind.Transfer && x.CategoryCode == "wip-movement");
        Assert.Contains(compiled["S3"].Events, x =>
            x.Metadata.TryGetValue(LedgerScenarioMetadataKeys.FlowStepKind, out string? kind) &&
            string.Equals(kind, ScenarioProductFlowStepKind.Quality.ToString(), StringComparison.Ordinal));
        Assert.Contains(compiled["S3"].Events, x => x.Kind == EconomicEventKind.Depreciation);
        Assert.Contains(compiled["VEF"].Events, x => x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.FinancialScheduleId));
        Assert.Equal(133549100m, Round2(compiled["VEF"].ExpectedTotals["revenue"]));
        Assert.Equal(135141490m, Round2(compiled["VEF"].ExpectedTotals["cashReceipts"]));
    }

    [Fact]
    public void VefFinancialPlanScenario_ReconcilesWorkbookYearSummaries()
    {
        var serializer = new LedgerSimulatorJsonSerializer();
        var compiler = new LedgerScenarioEventCompiler(serializer);
        var projectionService = new LedgerProjectionService();
        ScenarioBusinessModelState model = new LedgerScenarioTemplateCatalog().GetScenario("VEF");
        LedgerScenarioEventCompileResult compiled = compiler.Compile(new LedgerScenarioEventCompileRequest(model, new ScenarioId("vef-reconciliation"), model.PlanningStartDate, model.PlanningEndDate));
        LedgerProjection projection = projectionService.BuildProjection(new ProjectionRequest(model.ProjectId, model.OpeningCash, compiled.Events));
        var expectations = new Dictionary<int, (decimal ProductIncome, decimal CashIncome, decimal Expenses, decimal ClosingCash)>
        {
            [2021] = (0m, 0m, 73700m, 76300m),
            [2022] = (146050m, 148190m, 2921370m, -2696880m),
            [2023] = (21828050m, 22072300m, 9779500m, 9595920m),
            [2024] = (46050000m, 46599000m, 12105500m, 44089420m),
            [2025] = (65525000m, 66322000m, 17436500m, 92974920m)
        };

        foreach (KeyValuePair<int, (decimal ProductIncome, decimal CashIncome, decimal Expenses, decimal ClosingCash)> item in expectations)
        {
            int year = item.Key;
            EconomicEventState[] events = compiled.Events.Where(x => x.EconomicDate.Year == year).ToArray();
            decimal productIncome = events
                .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly)
                .Sum(x => x.Amount.Value);
            decimal cashIncome = events
                .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.CashOnly)
                .Sum(x => x.Amount.Value);
            decimal cashExpenses = events
                .Where(IsCashOutflow)
                .Where(x => x.ImpactKind is EconomicImpactKind.CashOnly or EconomicImpactKind.CashAndAccrual)
                .Sum(x => x.Amount.Value);
            CashFlowProjectionPoint closingPoint = projection.CashFlow.Last(x => x.Date.Year == year);

            Assert.Equal(item.Value.ProductIncome, Round2(productIncome));
            Assert.Equal(item.Value.CashIncome, Round2(cashIncome));
            Assert.Equal(item.Value.Expenses, Round2(cashExpenses));
            Assert.Equal(item.Value.ClosingCash, Round2(closingPoint.BalanceAfter.Value));
        }

        Assert.Equal(expectations.Values.Sum(x => x.Expenses), Round2(compiled.ExpectedTotals["financialScheduleOutflows"]));
        Assert.Equal(expectations.Values.Sum(x => x.CashIncome) + 150000m, Round2(compiled.ExpectedTotals["financialScheduleCashInflows"]));
    }

    [Fact]
    public void VefFinancialPlanScenario_PreservesEmployeesProductsAndApprovalSchedules()
    {
        var serializer = new LedgerSimulatorJsonSerializer();
        var compiler = new LedgerScenarioEventCompiler(serializer);
        ScenarioBusinessModelState model = new LedgerScenarioTemplateCatalog().GetScenario("VEF");
        LedgerScenarioEventCompileResult compiled = compiler.Compile(new LedgerScenarioEventCompileRequest(model, new ScenarioId("vef-controls"), model.PlanningStartDate, model.PlanningEndDate));

        Assert.Contains(model.ProductSkus, product => product.DisplayName == "HomeNode");
        Assert.Contains(model.ProductSkus, product => product.DisplayName == "PL Robot with B+R HW");
        Assert.Contains(model.FinancialSchedules, schedule => schedule.DisplayName.Contains("Brian Thomas", StringComparison.Ordinal));
        Assert.Contains(model.FinancialSchedules, schedule => schedule.DisplayName.Contains("Programmer .NET Senior", StringComparison.Ordinal));
        Assert.Contains(model.FinancialSchedules, schedule => schedule.DisplayName.Contains("External freelancers", StringComparison.Ordinal));
        Assert.Contains(model.FinancialSchedules, schedule => !schedule.PostsToSimulation && schedule.CategoryCode == "product-unit-plan");
        Assert.Contains(model.FinancialSchedules, schedule => schedule.RequiresApproval && schedule.Approval?.Status == ScenarioApprovalStatus.ManagementApproved);
        Assert.Contains(model.FinancialSchedules, schedule => schedule.RequiresApproval && schedule.Approval?.Status == ScenarioApprovalStatus.BusinessUnitApproved);
        Assert.All(model.FinancialSchedules.SelectMany(x => x.Amounts), amount => Assert.Equal(LedgerSimulatorPeriods.MonthKey(amount.EconomicDate), amount.PeriodCode));
        Assert.DoesNotContain(model.FinancialSchedules, schedule => schedule.Metadata.ContainsKey("sourceWorkbook"));
        Assert.DoesNotContain(compiled.Events, ev => ev.CategoryCode == "product-unit-plan");

        EconomicEventState approvedExpense = compiled.Events.First(x =>
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.FinancialScheduleId) &&
            x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.ApprovalStatus));
        Assert.True(approvedExpense.Metadata.ContainsKey(LedgerScenarioMetadataKeys.FinancialSchedulePeriod));
        Assert.False(approvedExpense.Metadata.ContainsKey("sourceCell"));
        Assert.True(approvedExpense.Metadata.ContainsKey(LedgerScenarioMetadataKeys.ParentFinancialScheduleId));
        Assert.Contains(
            LedgerSimulatorScenarioRulePackCatalog.CreateRulePack(model).Templates,
            template => template.Process.Kind == LedgerSimulatorRuleProcessKind.CapexApproval);
    }

    [Fact]
    public void AddLedgerSimulator_PassesScopeValidationForAppStartup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        services.AddLedgerSimulator();

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider.GetRequiredService<ISimulationScenarioRunMaterializer>());
        Assert.NotNull(provider.GetRequiredService<ISimulationRunLifecycleService>());
    }

    [Fact]
    public async Task ScenarioRunner_RequiresScenarioAndReportsDeficit()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        services.AddLedgerSimulator();
        using ServiceProvider provider = services.BuildServiceProvider();
        ILedgerScenarioRunner runner = provider.GetRequiredService<ILedgerScenarioRunner>();
        BusinessUnitId ops = LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "operations");
        EconomicEventState eventState = EconomicEventGenerator.CreateEvent(
            "project-alpha",
            "ops-rent",
            1,
            EconomicEventKind.Expense,
            EconomicEventStatus.Planned,
            EconomicImpactKind.CashOnly,
            ops,
            null,
            "rent",
            Amount.FromInt64(150),
            "USD",
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            1m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(
            new ScenarioRunRequest(new SimulationAccountContext(new SimulationBookId("default")), "project-alpha", "Real is not a scenario", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Amount.FromInt64(100), new[] { eventState }),
            CancellationToken.None).AsTask());

        ScenarioRunResult result = await runner.RunAsync(
            new ScenarioRunRequest(new SimulationAccountContext(new SimulationBookId("default"), new ScenarioId("downside")), "project-alpha", "Downside", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Amount.FromInt64(100), new[] { eventState }),
            CancellationToken.None);

        Assert.Equal(new ScenarioId("downside"), result.Run.ScenarioId);
        Assert.Equal(new DateOnly(2026, 1, 15), result.Result.FirstDeficitDate);
        Assert.Equal(2, result.ResultDrafts.Count);
    }

    [Fact]
    public async Task ScenarioRunner_RunsCompiledEventsInScenarioContextAndKeepsActualTrackingSeparate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        services.AddLedgerSimulator();
        await using ServiceProvider provider = services.BuildServiceProvider();
        ILedgerScenarioTemplateCatalog catalog = provider.GetRequiredService<ILedgerScenarioTemplateCatalog>();
        ILedgerScenarioEventCompiler compiler = provider.GetRequiredService<ILedgerScenarioEventCompiler>();
        ILedgerScenarioRunner runner = provider.GetRequiredService<ILedgerScenarioRunner>();
        ScenarioBusinessModelState model = catalog.GetScenario("S1");
        LedgerScenarioEventCompileResult compiled = compiler.Compile(new LedgerScenarioEventCompileRequest(model, new ScenarioId("s1-draft"), model.PlanningStartDate, model.PlanningEndDate));

        Task<ScenarioRunResult[]> runs = Task.WhenAll(
            runner.RunAsync(new ScenarioRunRequest(new SimulationAccountContext(new SimulationBookId("default"), new ScenarioId("s1-upside")), model.ProjectId, "S1 upside", model.PlanningStartDate, model.PlanningEndDate, model.OpeningCash, compiled.Events, RunMode: ScenarioRunMode.DraftOnly), CancellationToken.None).AsTask(),
            runner.RunAsync(new ScenarioRunRequest(new SimulationAccountContext(new SimulationBookId("default"), new ScenarioId("s1-downside")), model.ProjectId, "S1 downside", model.PlanningStartDate, model.PlanningEndDate, model.OpeningCash, compiled.Events, RunMode: ScenarioRunMode.DraftAndScenarioPosting), CancellationToken.None).AsTask());

        ScenarioRunResult[] results = await runs;

        Assert.Equal(new[] { new ScenarioId("s1-upside"), new ScenarioId("s1-downside") }, results.Select(x => x.Run.ScenarioId).ToArray());
        Assert.Contains(results, x => x.Run.SimulationMode == ScenarioRunMode.DraftOnly.ToString());
        Assert.Contains(results, x => x.Run.SimulationMode == ScenarioRunMode.DraftAndScenarioPosting.ToString());
        Assert.All(results, result => Assert.Equal(2, result.ResultDrafts.Count));

        IActualTrackingService actualTracking = provider.GetRequiredService<IActualTrackingService>();
        ActualEconomicEntryState invalid = actualTracking.ValidateActualEntry(new ActualEntryRequest(
            new SimulationAccountContext(new SimulationBookId("default"), results[0].Run.ScenarioId),
            model.ProjectId,
            "actual-in-scenario",
            "test",
            compiled.Events.First(x => x.Status == EconomicEventStatus.Invoiced)));
        ActualEconomicEntryState valid = actualTracking.ValidateActualEntry(new ActualEntryRequest(
            new SimulationAccountContext(new SimulationBookId("default")),
            model.ProjectId,
            "actual-real-ledger",
            "test",
            compiled.Events.First(x => x.Status == EconomicEventStatus.Invoiced)));

        Assert.Equal("invalid", invalid.ValidationStatus);
        Assert.Equal("valid", valid.ValidationStatus);
    }

    [Fact]
    public void ActualTracking_RejectsScenarioContextAndExternalSettlementUsesClearingPolicy()
    {
        var actualService = new ActualTrackingService();
        var serializer = new LedgerSimulatorJsonSerializer();
        var settlementService = new ExternalBoundaryPostingService(serializer);
        BusinessUnitId ops = LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "operations");
        BusinessUnitId clearing = LedgerSimulatorIdentifiers.BusinessUnitId("project-alpha", "external-clearing");
        EconomicEventState eventState = EconomicEventGenerator.CreateEvent(
            "project-alpha",
            "actual-revenue",
            1,
            EconomicEventKind.Revenue,
            EconomicEventStatus.Settled,
            EconomicImpactKind.CashOnly,
            ops,
            null,
            "actual-revenue",
            Amount.FromInt64(500),
            "USD",
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 1),
            1m);

        ActualEconomicEntryState invalid = actualService.ValidateActualEntry(new ActualEntryRequest(
            new SimulationAccountContext(new SimulationBookId("default"), new ScenarioId("scenario")),
            "project-alpha",
            "entry-1",
            "user",
            eventState));
        ExternalSettlementPlan outflow = settlementService.BuildSettlementPlan(new ExternalSettlementRequest(
            "project-alpha",
            "wire-1",
            clearing,
            TreasuryAddress,
            ops,
            Address.FromPublicKey("ops-public"u8.ToArray()),
            Amount.FromInt64(250),
            "USD",
            IsInflow: false,
            "bank-wire-1",
            new DateOnly(2026, 2, 5)));

        Assert.Equal("invalid", invalid.ValidationStatus);
        Assert.Equal(ExternalBoundaryPostingService.ExternalClearingTransferPolicy, outflow.Policy);
        Assert.NotNull(outflow.ClearingTransfer);
        Assert.Equal(TreasuryAddress, outflow.ClearingTransfer!.ReceiverAddress);
    }

    [Fact]
    public async Task SimulatorPersistence_RegistersContextFactoriesForInMemoryAndPostgreSql()
    {
        var inMemoryServices = new ServiceCollection();
        inMemoryServices.AddLedgerSimulator();
        inMemoryServices.AddLedgerSimulatorInMemoryProjectionPersistence(Guid.NewGuid().ToString("N"));
        await using (ServiceProvider provider = inMemoryServices.BuildServiceProvider())
        {
            IDbContextFactory<LedgerSimulatorProjectionDbContext> factory = provider.GetRequiredService<IDbContextFactory<LedgerSimulatorProjectionDbContext>>();
            await using LedgerSimulatorProjectionDbContext db = await factory.CreateDbContextAsync();
            db.Projections.Add(new LedgerSimulatorProjectionRecord
            {
                ProjectId = "project-alpha",
                Kind = "cashflow",
                AsOfDate = new DateOnly(2026, 1, 1),
                PayloadJson = "{}",
                UpdatedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
            });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Projections.CountAsync());
            Assert.NotNull(db.Model.FindEntityType(typeof(LedgerSimulatorGraphPointRecord)));
        }

        var postgresServices = new ServiceCollection();
        postgresServices.AddLedgerSimulatorPostgreSqlProjectionPersistence("Host=localhost;Database=candoitall_ledger_simulator_test;Username=test;Password=test");
        using ServiceProvider postgresProvider = postgresServices.BuildServiceProvider();
        Assert.NotNull(postgresProvider.GetRequiredService<IDbContextFactory<LedgerSimulatorProjectionDbContext>>());
    }

    [Fact]
    public async Task GraphProjection_WritesCompiledEventsAndSupportsScenarioGraphFilters()
    {
        var services = new ServiceCollection();
        services.AddLedgerSimulator();
        services.AddLedgerSimulatorInMemoryProjectionPersistence(Guid.NewGuid().ToString("N"));
        await using ServiceProvider provider = services.BuildServiceProvider();
        ILedgerScenarioTemplateCatalog catalog = provider.GetRequiredService<ILedgerScenarioTemplateCatalog>();
        ILedgerScenarioEventCompiler compiler = provider.GetRequiredService<ILedgerScenarioEventCompiler>();
        ILedgerSimulatorGraphProjectionWriter writer = provider.GetRequiredService<ILedgerSimulatorGraphProjectionWriter>();
        ILedgerSimulatorGraphQueryService queryService = provider.GetRequiredService<ILedgerSimulatorGraphQueryService>();
        ScenarioBusinessModelState model = catalog.GetScenario("S3");
        var scenarioId = new ScenarioId("s3-graph");
        LedgerScenarioEventCompileResult compiled = compiler.Compile(new LedgerScenarioEventCompileRequest(model, scenarioId, model.PlanningStartDate, model.PlanningEndDate));

        await writer.ReplaceScenarioGraphPointsAsync(model.ProjectId, scenarioId.Value, compiled.Events, CancellationToken.None);

        LedgerGraphData cashFlow = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.CashFlow, null, [], [], 1000),
            CancellationToken.None);
        LedgerGraphData revenue = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.Revenue, null, [], ["revenue-hardware"], 1000),
            CancellationToken.None);
        BusinessUnitState salesUnit = model.BusinessUnits.Single(x => x.Path == "sales-account-management");
        LedgerGraphData accountFiltered = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.CashFlow, salesUnit.BusinessUnitId.Value, [salesUnit.PrimaryAddress.Value], [], 1000),
            CancellationToken.None);

        Assert.True(cashFlow.Totals["rowCount"] > 0m);
        Assert.NotEqual(0m, cashFlow.Totals["netCashFlow"]);
        Assert.NotEmpty(cashFlow.Series);
        Assert.True(revenue.Totals["nonCashIncome"] > 0m);
        Assert.All(revenue.DrilldownRows, row => Assert.Equal("revenue-hardware", row.CategoryCode));
        Assert.True(accountFiltered.Totals["rowCount"] > 0m);
        Assert.All(accountFiltered.DrilldownRows, row => Assert.Equal(salesUnit.BusinessUnitId.Value, row.BusinessUnitId));
    }

    [Fact]
    public async Task GoldenScenarios_RunCompilerRunnerProjectionAndGraphDataForAllScenarios()
    {
        await using ServiceProvider provider = BuildGoldenScenarioServiceProvider();

        foreach (GoldenScenarioExpectation expectation in GoldenScenarioExpectations.Values.OrderBy(x => x.ScenarioCode, StringComparer.Ordinal))
        {
            GoldenScenarioProof proof = await BuildGoldenScenarioProofAsync(provider, expectation.ScenarioCode);

            Assert.Equal(expectation.EventCount, proof.Compiled.Events.Count);
            Assert.Equal(expectation.ScheduleHash, proof.Compiled.ScheduleHash);
            Assert.Equal(expectation.Revenue, Round2(proof.Compiled.ExpectedTotals["revenue"]));
            Assert.Equal(expectation.CashReceiptsInHorizon, Round2(proof.CashReceiptsInHorizon));
            Assert.Equal(expectation.EndingAccountsReceivable, Round2(proof.EndingAccountsReceivable));
            Assert.Equal(expectation.EndingAccountsPayableProxy, Round2(proof.EndingAccountsPayableProxy));
            Assert.Equal(expectation.Cogs, Round2(proof.Compiled.ExpectedTotals["cogs"]));
            Assert.Equal(expectation.Opex, Round2(proof.Opex));
            Assert.Equal(expectation.Payroll, Round2(proof.Compiled.ExpectedTotals["payroll"]));
            Assert.Equal(expectation.Capex, Round2(proof.Compiled.ExpectedTotals["capex"]));
            Assert.Equal(expectation.Depreciation, Round2(proof.Compiled.ExpectedTotals["depreciation"]));
            Assert.Equal(expectation.InventoryPurchases, Round2(proof.InventoryPurchases));
            Assert.Equal(expectation.WipMovement, Round2(proof.WipMovement));
            Assert.Equal(expectation.ClosingCash, Round2(proof.Run.Result.ClosingCash.Value));
            Assert.Equal(expectation.MinimumCash, Round2(proof.Run.Result.MinimumCash.Value));
            Assert.Equal(expectation.DeficitCount, (int)proof.Run.Result.Kpis["deficitCount"]);
            Assert.Equal(2, proof.Run.ResultDrafts.Count);

            Assert.Equal(expectation.GraphRows, (int)proof.CashFlowGraph.Totals["rowCount"]);
            Assert.Equal(expectation.Revenue, Round2(proof.RevenueGraph.Totals["nonCashIncome"]));
            Assert.Equal(expectation.CashReceiptsInHorizon, Round2(proof.RevenueGraph.Totals["inflow"]));
            Assert.Equal(expectation.Revenue - expectation.CashReceiptsInHorizon, Round2(proof.EndingAccountsReceivable));
            Assert.NotEmpty(proof.CashFlowGraph.Series);
            Assert.NotEmpty(proof.RevenueGraph.DrilldownRows);
            Assert.NotEmpty(proof.ExpenseGraph.Series);
            Assert.True(proof.AccountGraph.Totals["rowCount"] > 0m);
            Assert.All(proof.AccountGraph.DrilldownRows, row => Assert.Equal(proof.RevenueUnit.BusinessUnitId.Value, row.BusinessUnitId));
        }
    }

    [Fact]
    public async Task GoldenScenarios_ReconcileAgainstClassicBaselinesAndCfoKpis()
    {
        await using ServiceProvider provider = BuildGoldenScenarioServiceProvider();

        foreach (GoldenScenarioExpectation expectation in GoldenScenarioExpectations.Values.OrderBy(x => x.ScenarioCode, StringComparer.Ordinal))
        {
            GoldenScenarioProof proof = await BuildGoldenScenarioProofAsync(provider, expectation.ScenarioCode);
            decimal revenueVariance = VariancePercent(proof.Compiled.ExpectedTotals["revenue"], expectation.BaselineRevenue);
            decimal grossMargin = (proof.Compiled.ExpectedTotals["revenue"] - proof.Compiled.ExpectedTotals["cogs"]) / proof.Compiled.ExpectedTotals["revenue"];
            decimal grossMarginVariance = Math.Abs(grossMargin - expectation.BaselineGrossMargin);
            decimal arVariance = VariancePercent(proof.EndingAccountsReceivable, expectation.BaselineEndingAccountsReceivable);
            decimal dso = proof.EndingAccountsReceivable / proof.Compiled.ExpectedTotals["revenue"] * 365m;
            decimal ebitda = proof.Compiled.ExpectedTotals["revenue"] - proof.Compiled.ExpectedTotals["cogs"] - proof.Opex - proof.Compiled.ExpectedTotals["payroll"];
            bool cashStress = proof.Run.Result.MinimumCash.Value < proof.Model.MinimumCash.Value;

            Assert.InRange(revenueVariance, 0m, expectation.RevenueTolerancePercent);
            Assert.InRange(grossMarginVariance, 0m, expectation.GrossMarginTolerancePoints);
            Assert.InRange(arVariance, 0m, expectation.WorkingCapitalTolerancePercent);
            Assert.InRange(dso, expectation.DsoMinimumDays, expectation.DsoMaximumDays);
            Assert.Equal(expectation.ClosingCashShouldBeNegative, proof.Run.Result.ClosingCash.Value < 0m);
            Assert.Equal(expectation.ClosingCashShouldBeNegative, cashStress);
            Assert.Equal(expectation.EbitdaShouldBeNegative, ebitda < 0m);
            Assert.DoesNotContain(proof.Compiled.Events, x => x.CategoryCode.Contains("income", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(proof.Compiled.Events, x => x.CategoryCode.Contains("sall", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly);
            Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.CashOnly);

            if (expectation.ScenarioCode is "S1" or "S3")
            {
                Assert.True(proof.Compiled.ExpectedTotals["serviceHours"] > 0m);
                Assert.Contains(proof.Compiled.Events, x =>
                    x.CategoryCode == "workdone-service-hours" &&
                    x.ImpactKind == EconomicImpactKind.ControlTokenOnly &&
                    x.TokenEffect?.TokenId.Value.Contains(":service-hour:", StringComparison.Ordinal) == true);
            }

            if (expectation.ScenarioCode is "S2" or "S3")
            {
                Assert.True(proof.Compiled.ExpectedTotals["productFlowUnits"] > 0m);
                Assert.Contains(proof.Compiled.Events, x =>
                    x.CategoryCode.StartsWith("product-flow-", StringComparison.Ordinal) &&
                    x.ImpactKind == EconomicImpactKind.ControlTokenOnly &&
                    x.TokenEffect?.TokenId.Value.Contains(":product-unit:", StringComparison.Ordinal) == true);
            }

            if (expectation.InventoryPurchases > 0m)
            {
                decimal dpoProxy = proof.EndingAccountsPayableProxy / proof.InventoryPurchases * 365m;
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.InventoryPurchase && x.ImpactKind == EconomicImpactKind.CashOnly);
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.InventoryConsumption && x.ImpactKind == EconomicImpactKind.ControlTokenOnly);
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Cogs && x.ImpactKind == EconomicImpactKind.NonCashPnl);
                Assert.InRange(dpoProxy, expectation.ScenarioCode == "S2" ? 8m : 35m, expectation.ScenarioCode == "S2" ? 25m : 55m);
            }

            if (expectation.Capex > 0m)
            {
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Capex && x.ImpactKind == EconomicImpactKind.CashOnly);
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Depreciation && x.ImpactKind == EconomicImpactKind.NonCashPnl);
            }

            if (expectation.WipMovement > 0m)
            {
                Assert.Contains(proof.Compiled.Events, x => x.Kind == EconomicEventKind.Transfer && x.Metadata[LedgerScenarioMetadataKeys.FinancialStatementSection] == FinancialStatementSection.Wip.ToString());
            }

            if (expectation.ScenarioCode == "S3")
            {
                decimal topCustomerShare = proof.Compiled.Events
                    .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly && x.Metadata.ContainsKey(LedgerScenarioMetadataKeys.CounterpartyId))
                    .GroupBy(x => x.Metadata[LedgerScenarioMetadataKeys.CounterpartyId], StringComparer.Ordinal)
                    .Select(x => x.Sum(e => e.Amount.Value))
                    .Max() / proof.Compiled.ExpectedTotals["revenue"];
                Assert.InRange(topCustomerShare, 0.20m, 0.50m);
            }
        }
    }

    private static ServiceProvider BuildGoldenScenarioServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITimeProvider>(new ManualTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z")));
        services.AddLedgerSimulator();
        services.AddLedgerSimulatorInMemoryProjectionPersistence(Guid.NewGuid().ToString("N"));
        return services.BuildServiceProvider();
    }

    private static async Task<GoldenScenarioProof> BuildGoldenScenarioProofAsync(ServiceProvider provider, string scenarioCode)
    {
        ILedgerScenarioTemplateCatalog catalog = provider.GetRequiredService<ILedgerScenarioTemplateCatalog>();
        ILedgerScenarioEventCompiler compiler = provider.GetRequiredService<ILedgerScenarioEventCompiler>();
        ILedgerScenarioRunner runner = provider.GetRequiredService<ILedgerScenarioRunner>();
        ILedgerSimulatorGraphProjectionWriter writer = provider.GetRequiredService<ILedgerSimulatorGraphProjectionWriter>();
        ILedgerSimulatorGraphQueryService queryService = provider.GetRequiredService<ILedgerSimulatorGraphQueryService>();

        ScenarioBusinessModelState model = catalog.GetScenario(scenarioCode);
        var scenarioId = new ScenarioId($"golden-{scenarioCode.ToLowerInvariant()}");
        LedgerScenarioEventCompileResult compiled = compiler.Compile(new LedgerScenarioEventCompileRequest(model, scenarioId, model.PlanningStartDate, model.PlanningEndDate));
        ScenarioRunResult run = await runner.RunAsync(
            new ScenarioRunRequest(
                new SimulationAccountContext(new SimulationBookId("default"), scenarioId),
                model.ProjectId,
                $"{scenarioCode} golden scenario",
                model.PlanningStartDate,
                model.PlanningEndDate,
                model.OpeningCash,
                compiled.Events,
                RunMode: ScenarioRunMode.DraftAndScenarioPosting),
            CancellationToken.None);

        await writer.ReplaceScenarioGraphPointsAsync(model.ProjectId, scenarioId.Value, compiled.Events, CancellationToken.None);

        BusinessUnitState revenueUnit = model.BusinessUnits.First(x => x.Kind == BusinessUnitKind.RevenueCenter);
        LedgerGraphData cashFlow = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.CashFlow, null, [], [], 5000),
            CancellationToken.None);
        LedgerGraphData revenue = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.Revenue, null, [], [], 5000),
            CancellationToken.None);
        LedgerGraphData expense = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.Expense, null, [], [], 5000),
            CancellationToken.None);
        LedgerGraphData account = await queryService.QueryAsync(
            new LedgerGraphQueryFilter(model.ProjectId, scenarioId.Value, model.PlanningStartDate, model.PlanningEndDate, ScenarioGraphMetric.CashFlow, revenueUnit.BusinessUnitId.Value, [revenueUnit.PrimaryAddress.Value], [], 5000),
            CancellationToken.None);

        decimal revenueInHorizon = compiled.Events
            .Where(x => x.EconomicDate >= model.PlanningStartDate && x.EconomicDate <= model.PlanningEndDate)
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.AccrualOnly)
            .Sum(x => x.Amount.Value);
        decimal cashReceiptsInHorizon = compiled.Events
            .Where(x => x.EconomicDate >= model.PlanningStartDate && x.EconomicDate <= model.PlanningEndDate)
            .Where(x => x.Kind == EconomicEventKind.Revenue && x.ImpactKind == EconomicImpactKind.CashOnly)
            .Sum(x => x.Amount.Value);
        decimal inventoryPurchases = compiled.Events
            .Where(x => x.Kind == EconomicEventKind.InventoryPurchase)
            .Sum(x => x.Amount.Value);
        decimal inventoryPaymentsInHorizon = compiled.Events
            .Where(x => x.EconomicDate >= model.PlanningStartDate && x.EconomicDate <= model.PlanningEndDate)
            .Where(x => x.Kind == EconomicEventKind.InventoryPurchase)
            .Sum(x => x.Amount.Value);

        return new GoldenScenarioProof(
            model,
            revenueUnit,
            compiled,
            run,
            cashFlow,
            revenue,
            expense,
            account,
            cashReceiptsInHorizon,
            revenueInHorizon - cashReceiptsInHorizon,
            inventoryPurchases,
            inventoryPurchases - inventoryPaymentsInHorizon,
            compiled.Events.Where(x => x.Kind == EconomicEventKind.Transfer && x.CategoryCode == "wip-movement").Sum(x => x.Amount.Value),
            compiled.ExpectedTotals["opex"]);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2);

    private static bool IsCashOutflow(EconomicEventState eventState)
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

    private static decimal VariancePercent(decimal actual, decimal baseline)
        => baseline == 0m ? Math.Abs(actual) : Math.Abs(actual - baseline) / Math.Abs(baseline);

    private sealed record GoldenScenarioExpectation(
        string ScenarioCode,
        int EventCount,
        int GraphRows,
        string ScheduleHash,
        decimal Revenue,
        decimal CashReceiptsInHorizon,
        decimal EndingAccountsReceivable,
        decimal EndingAccountsPayableProxy,
        decimal Cogs,
        decimal Opex,
        decimal Payroll,
        decimal Capex,
        decimal Depreciation,
        decimal InventoryPurchases,
        decimal WipMovement,
        decimal ClosingCash,
        decimal MinimumCash,
        int DeficitCount,
        decimal BaselineRevenue,
        decimal BaselineGrossMargin,
        decimal BaselineEndingAccountsReceivable,
        decimal RevenueTolerancePercent,
        decimal GrossMarginTolerancePoints,
        decimal WorkingCapitalTolerancePercent,
        decimal DsoMinimumDays,
        decimal DsoMaximumDays,
        bool ClosingCashShouldBeNegative,
        bool EbitdaShouldBeNegative);

    private sealed record GoldenScenarioProof(
        ScenarioBusinessModelState Model,
        BusinessUnitState RevenueUnit,
        LedgerScenarioEventCompileResult Compiled,
        ScenarioRunResult Run,
        LedgerGraphData CashFlowGraph,
        LedgerGraphData RevenueGraph,
        LedgerGraphData ExpenseGraph,
        LedgerGraphData AccountGraph,
        decimal CashReceiptsInHorizon,
        decimal EndingAccountsReceivable,
        decimal InventoryPurchases,
        decimal EndingAccountsPayableProxy,
        decimal WipMovement,
        decimal Opex);
}
