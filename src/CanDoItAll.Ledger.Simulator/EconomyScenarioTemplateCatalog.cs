namespace CanDoItAll.Economy.Simulator;

public interface IEconomyScenarioTemplateCatalog
{
    ScenarioBusinessModelState GetScenario(string scenarioCode);

    IReadOnlyList<ScenarioBusinessModelState> GetScenarios();
}

public sealed class EconomyScenarioTemplateCatalog : IEconomyScenarioTemplateCatalog
{
    private static readonly IReadOnlyList<ScenarioBusinessModelState> Scenarios =
    [
        EconomyScenarioTemplates.CreateOnlineServicesScenario(),
        EconomyScenarioTemplates.CreateBakeryScenario(),
        EconomyScenarioTemplates.CreateHardwareEngineeringScenario(),
        VefFinancialPlanScenarioData.CreateScenario()
    ];

    public ScenarioBusinessModelState GetScenario(string scenarioCode)
        => Scenarios.FirstOrDefault(x => string.Equals(x.ScenarioCode, scenarioCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Economy scenario '{scenarioCode}' was not found.");

    public IReadOnlyList<ScenarioBusinessModelState> GetScenarios() => Scenarios;
}

public static class EconomyScenarioTemplates
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2027, 12, 31);

    public static ScenarioBusinessModelState CreateOnlineServicesScenario()
    {
        const string project = "s1-online-services";
        BusinessUnitState management = Unit(project, "management-ga", "Management / G&A", BusinessUnitKind.CostCenter);
        BusinessUnitState engineering = Unit(project, "product-engineering", "Product Engineering", BusinessUnitKind.CostCenter);
        BusinessUnitState support = Unit(project, "customer-support", "Customer Support", BusinessUnitKind.CostCenter);
        BusinessUnitState sales = Unit(project, "sales-marketing", "Sales & Marketing", BusinessUnitKind.RevenueCenter);
        BusinessUnitState admin = Unit(project, "finance-hr-admin", "Finance / HR / Admin", BusinessUnitKind.CostCenter);
        BusinessUnitState cloud = Unit(project, "it-cloud-operations", "IT / Cloud Operations", BusinessUnitKind.CostCenter);
        BusinessUnitState treasury = Unit(project, "treasury", "Treasury", BusinessUnitKind.Project);

        PaymentTermState card = Payment("card-same-month", 0, 0.98m);
        PaymentTermState net30 = Payment("net-30", 30, 0m);
        PaymentTermState net45 = Payment("net-45", 45, 0m);

        var tiers = new[]
        {
            new PriceTierState("basic", "Basic subscription", Amount.FromDecimal(79m), Amount.FromDecimal(11m), "monthly", Meta("cashPattern", "same-month-card")),
            new PriceTierState("professional", "Professional subscription", Amount.FromDecimal(249m), Amount.FromDecimal(38m), "monthly", Meta("cashPattern", "90-percent-same-month")),
            new PriceTierState("enterprise", "Enterprise subscription", Amount.FromDecimal(899m), Amount.FromDecimal(160m), "monthly", Meta("cashPattern", "net-30-5-percent-late"))
        };

        var contracts = new[]
        {
            Contract(project, "basic-customers", "Basic subscriber cohort", card.PaymentTermId, sales, "basic-subscription"),
            Contract(project, "professional-customers", "Professional subscriber cohort", net30.PaymentTermId, sales, "professional-subscription"),
            Contract(project, "enterprise-customers", "Enterprise subscriber cohort", net30.PaymentTermId, sales, "enterprise-subscription"),
            Contract(project, "implementation-customers", "Implementation projects", net30.PaymentTermId, sales, "implementation-service"),
            Contract(project, "self-host-license-customers", "Self-hosted license customers", net45.PaymentTermId, sales, "self-hosted-license")
        };

        var schedules = new[]
        {
            Demand(project, "basic-demand", contracts[0], "basic", sales, 350m, 0.030m),
            Demand(project, "professional-demand", contracts[1], "professional", sales, 80m, 0.030m),
            Demand(project, "enterprise-demand", contracts[2], "enterprise", sales, 12m, 0.025m)
        };

        var projects = new[]
        {
            Implementation(project, "implementation-2026", contracts[3], Amount.FromDecimal(8m * 18000m), Amount.FromDecimal(8m * 18000m * 0.45m), new DateOnly(2026, 1, 1)),
            Implementation(project, "implementation-2027", contracts[3], Amount.FromDecimal(10m * 18000m), Amount.FromDecimal(10m * 18000m * 0.45m), new DateOnly(2027, 1, 1)),
            Implementation(project, "license-2026", contracts[4], Amount.FromDecimal((3m * 65000m) + (3m * 12000m)), Amount.FromDecimal((3m * 65000m * 0.08m) + (3m * 12000m * 0.25m)), new DateOnly(2026, 2, 1)),
            Implementation(project, "license-2027", contracts[4], Amount.FromDecimal((2m * 75000m) + (2m * 12000m)), Amount.FromDecimal((2m * 75000m * 0.08m) + (2m * 12000m * 0.25m)), new DateOnly(2027, 2, 1))
        };

        WorkforcePlanState workforce = new(
            project,
            "s1-workforce",
            [
                Staffing(project, management, "Management", 2m, 7000m, 0.25m),
                Staffing(project, engineering, "Product Engineering", 6m, 5500m, 0.25m, new HeadcountEventState(new DateOnly(2026, 4, 1), 1m, "roadmap scale-up", Empty()), new HeadcountEventState(new DateOnly(2026, 9, 1), 1m, "platform scale-up", Empty()), new HeadcountEventState(new DateOnly(2027, 3, 1), 1m, "platform scale-up", Empty())),
                Staffing(project, support, "Customer Support", 3m, 3200m, 0.25m, new HeadcountEventState(new DateOnly(2026, 6, 1), 1m, "support growth", Empty()), new HeadcountEventState(new DateOnly(2027, 6, 1), 1m, "support growth", Empty())),
                Staffing(project, sales, "Sales & Marketing", 2m, 4200m, 0.25m, new HeadcountEventState(new DateOnly(2027, 1, 1), 1m, "sales growth", Empty())),
                Staffing(project, admin, "Finance / HR / Admin", 1m, 3800m, 0.25m),
                Staffing(project, cloud, "IT / Cloud Operations", 1m, 4800m, 0.25m, new HeadcountEventState(new DateOnly(2027, 10, 1), 1m, "cloud operations coverage", Empty()))
            ],
            [new("new-hire-standard-kit", "*", Amount.FromDecimal(1800m), Amount.FromDecimal(720m), 36, Empty())],
            Empty());

        var workOrders = new[]
        {
            WorkOrder(project, "basic-support-sla", contracts[0], "basic", "Basic subscription support SLA", ScenarioWorkOrderKind.SupportSla, sales, support, "basic-demand", null, Start, null, ScenarioCadence.Monthly, 20m, 0.08m, "delivered-hours-to-monthly-invoice", [Allocation("support-primary", support, 1m, "support-queue")]),
            WorkOrder(project, "professional-support-sla", contracts[1], "professional", "Professional subscription support and success", ScenarioWorkOrderKind.SupportSla, sales, support, "professional-demand", null, Start, null, ScenarioCadence.Monthly, 35m, 0.22m, "delivered-hours-to-monthly-invoice", [Allocation("support-success", support, 0.8m, "support-queue"), Allocation("engineering-escalation", engineering, 0.2m, "engineering-escalation")]),
            WorkOrder(project, "enterprise-success-sla", contracts[2], "enterprise", "Enterprise success and engineering SLA", ScenarioWorkOrderKind.ExternalContract, sales, support, "enterprise-demand", null, Start, null, ScenarioCadence.Monthly, 40m, 3.5m, "delivered-hours-to-monthly-invoice", [Allocation("enterprise-support", support, 0.6m, "customer-success"), Allocation("enterprise-engineering", engineering, 0.4m, "product-specialist")]),
            WorkOrder(project, "implementation-delivery-2026", contracts[3], "implementation-service", "Implementation delivery 2026", ScenarioWorkOrderKind.Implementation, sales, engineering, null, "implementation-2026", new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31), ScenarioCadence.Monthly, 260m, 0m, "milestone-invoice-from-delivered-hours", [Allocation("implementation-engineering", engineering, 0.75m, "customer-project"), Allocation("implementation-support", support, 0.25m, "customer-onboarding")]),
            WorkOrder(project, "implementation-delivery-2027", contracts[3], "implementation-service", "Implementation delivery 2027", ScenarioWorkOrderKind.Implementation, sales, engineering, null, "implementation-2027", new DateOnly(2027, 1, 1), new DateOnly(2027, 7, 31), ScenarioCadence.Monthly, 310m, 0m, "milestone-invoice-from-delivered-hours", [Allocation("implementation-engineering", engineering, 0.75m, "customer-project"), Allocation("implementation-support", support, 0.25m, "customer-onboarding")]),
            WorkOrder(project, "self-host-license-2026", contracts[4], "self-hosted-license", "Self-host license delivery 2026", ScenarioWorkOrderKind.Implementation, sales, engineering, null, "license-2026", new DateOnly(2026, 2, 1), new DateOnly(2026, 8, 31), ScenarioCadence.Monthly, 180m, 0m, "license-delivery-invoice", [Allocation("license-engineering", engineering, 0.65m, "deployment-packaging"), Allocation("license-cloud", cloud, 0.35m, "environment-handoff")]),
            WorkOrder(project, "self-host-license-2027", contracts[4], "self-hosted-license", "Self-host license delivery 2027", ScenarioWorkOrderKind.Implementation, sales, engineering, null, "license-2027", new DateOnly(2027, 2, 1), new DateOnly(2027, 8, 31), ScenarioCadence.Monthly, 160m, 0m, "license-delivery-invoice", [Allocation("license-engineering", engineering, 0.65m, "deployment-packaging"), Allocation("license-cloud", cloud, 0.35m, "environment-handoff")]),
            InternalWorkOrder(project, "internal-product-roadmap", "Internal product roadmap", "internal-product-development", ScenarioWorkOrderKind.InternalProject, management, engineering, Start, null, ScenarioCadence.Monthly, 420m, [Allocation("roadmap-engineering", engineering, 0.85m, "internal-roadmap"), Allocation("roadmap-management", management, 0.15m, "portfolio-prioritization")])
        };

        return new ScenarioBusinessModelState(
            project,
            "S1",
            "S1 Online Services Company",
            "EUR",
            Start,
            End,
            Amount.FromDecimal(180000m),
            Amount.FromDecimal(60000m),
            [treasury, management, engineering, support, sales, admin, cloud],
            [card, net30, net45],
            [Counterparty(project, "subscriber-cohorts", "Subscriber cohorts"), Counterparty(project, "implementation-customers", "Implementation customers"), Counterparty(project, "license-customers", "Self-host license customers")],
            contracts,
            [
                new(project, "subscription-catalog", "Subscription tiers", RevenueStreamKind.Subscription, sales.Path, "revenue-subscription", "cogs-hosting", tiers, Empty()),
                new(project, "implementation-service", "Tailored software implementation", RevenueStreamKind.Service, sales.Path, "revenue-implementation-license", "cogs-delivery", [new("implementation-service-pack", "Implementation project package", Amount.FromDecimal(18000m), Amount.FromDecimal(8100m), "project", Meta("billing", "milestone"))], Empty()),
                new(project, "self-hosted-license", "Self-hosted software license", RevenueStreamKind.Custom, sales.Path, "revenue-implementation-license", "cogs-delivery", [new("self-host-license-pack", "Self-hosted license and deployment package", Amount.FromDecimal(65000m), Amount.FromDecimal(5200m), "project", Meta("billing", "milestone-and-deployment"))], Empty())
            ],
            [],
            schedules,
            projects,
            [workforce],
            [],
            [],
            [],
            [],
            [],
            workOrders,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [
                Cost(project, "base-cloud", cloud, "cogs-hosting", FinancialStatementSection.Cogs, 5000m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "development-tools", engineering, "opex-development-tools", FinancialStatementSection.Opex, 95m * 8m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "crm-support-tools", sales, "opex-crm-support", FinancialStatementSection.Opex, 85m * 7m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "office-rent-utilities", management, "opex-office", FinancialStatementSection.Opex, 6500m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "marketing-2026", sales, "opex-marketing", FinancialStatementSection.Opex, 8000m, EconomicImpactKind.CashAndAccrual, end: new DateOnly(2026, 12, 31)),
                Cost(project, "marketing-2027", sales, "opex-marketing", FinancialStatementSection.Opex, 11000m, EconomicImpactKind.CashAndAccrual, start: new DateOnly(2027, 1, 1)),
                Cost(project, "insurance-legal-accounting", management, "opex-professional-fees", FinancialStatementSection.Opex, 4500m, EconomicImpactKind.CashAndAccrual)
            ],
            [],
            Meta("baselineWorkbook", "scenario-baseline-financial-models.xlsx"));
    }

    public static ScenarioBusinessModelState CreateBakeryScenario()
    {
        const string project = "s2-bakery";
        BusinessUnitState production = Unit(project, "production", "Production", BusinessUnitKind.CostCenter);
        BusinessUnitState warehouse = Unit(project, "warehouse-procurement", "Warehouse / Procurement", BusinessUnitKind.CostCenter);
        BusinessUnitState sales = Unit(project, "sales-management", "Sales & Management", BusinessUnitKind.RevenueCenter);
        BusinessUnitState treasury = Unit(project, "treasury", "Treasury", BusinessUnitKind.Project);
        PaymentTermState retail = Payment("retail-immediate", 0, 0.78m);
        PaymentTermState wholesale14 = Payment("wholesale-net-14", 14, 0m);
        PaymentTermState wholesale21 = Payment("wholesale-net-21", 21, 0m);

        var products = new[]
        {
            Product(project, "BRD-001", "Everyday bread loaf", 2.20m, 0.3395m, "revenue-bakery", "cogs-ingredients"),
            Product(project, "ROL-001", "Roll / bun", 0.75m, 0.112m, "revenue-bakery", "cogs-ingredients"),
            Product(project, "CRS-001", "Butter croissant", 1.60m, 0.4635m, "revenue-bakery", "cogs-ingredients"),
            Product(project, "CK-001", "Cake slice / pastry", 3.80m, 0.464m, "revenue-bakery", "cogs-ingredients"),
            Product(project, "SDF-001", "Premium sourdough loaf", 4.50m, 0.42m, "revenue-bakery", "cogs-ingredients")
        };

        var contract = Contract(project, "bakery-customers", "Retail and wholesale customers", retail.PaymentTermId, sales, products.Select(x => x.ProductSkuId).ToArray());
        var schedules = new[]
        {
            Demand(project, "BRD-001-demand", contract, "BRD-001", sales, 5500m),
            Demand(project, "ROL-001-demand", contract, "ROL-001", sales, 18000m),
            Demand(project, "CRS-001-demand", contract, "CRS-001", sales, 4500m),
            Demand(project, "CK-001-demand", contract, "CK-001", sales, 1200m),
            Demand(project, "SDF-001-demand", contract, "SDF-001", sales, 900m, 0.02m)
        };

        WorkforcePlanState workforce = new(
            project,
            "s2-workforce",
            [
                Staffing(project, production, "Production baker", 3m, 1450m, 0.30m),
                Staffing(project, warehouse, "Warehouse", 1m, 1300m, 0.30m),
                Staffing(project, sales, "Sales & Management", 2m, 1800m, 0.30m)
            ],
            [],
            Empty());

        var productFlows = products
            .Select(product => BakeryProductFlow(project, product, contract, $"flow-{product.ProductSkuId}", production, warehouse, sales))
            .ToArray();

        return new ScenarioBusinessModelState(
            project,
            "S2",
            "S2 Small Production Bakery",
            "EUR",
            Start,
            End,
            Amount.FromDecimal(25000m),
            Amount.FromDecimal(10000m),
            [treasury, production, warehouse, sales],
            [retail, wholesale14, wholesale21],
            [Counterparty(project, "retail-wholesale-customers", "Retail and wholesale customers")],
            [contract],
            [],
            products,
            schedules,
            [],
            [workforce],
            Ingredients(project),
            Recipes(project, products),
            [],
            [new(project, "raw-material-warehouse", "Raw material warehouse", warehouse.BusinessUnitId, "warehouse", Empty()), new(project, "bakery-production", "Bakery production", production.BusinessUnitId, "production", Empty())],
            [new(project, "bakery-route", "all-products", [new("mix-bake-pack", production.BusinessUnitId, "Mix, bake, and pack", 90m, "raw-material-warehouse", "bakery-production", Empty())], Empty())],
            [],
            productFlows,
            [new(project, "bakery-facility", production.BusinessUnitId, "Bakery lease and utilities", Amount.FromDecimal(3500m), Empty())],
            [new(project, "bakery-repairs", production.BusinessUnitId, Amount.FromDecimal(450m), null, null, Empty())],
            [],
            [],
            [],
            [],
            [],
            [
                Cost(project, "electricity", production, "opex-electricity", FinancialStatementSection.Opex, 1250m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "rent", production, "opex-rent", FinancialStatementSection.Opex, 3500m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "small-tools-repairs", production, "opex-repairs-tools", FinancialStatementSection.Opex, 450m, EconomicImpactKind.CashAndAccrual)
            ],
            [],
            Meta("baselineWorkbook", "scenario-baseline-financial-models.xlsx"));
    }

    public static ScenarioBusinessModelState CreateHardwareEngineeringScenario()
    {
        const string project = "s3-hardware-engineering";
        BusinessUnitState molding = Unit(project, "plastic-molding", "Plastic Molding", BusinessUnitKind.CostCenter);
        BusinessUnitState electronics = Unit(project, "electronics-assembly-testing", "Electronics Assembly & Testing", BusinessUnitKind.CostCenter);
        BusinessUnitState warehouse = Unit(project, "logistics-warehouse", "Logistics / Warehouse", BusinessUnitKind.CostCenter);
        BusinessUnitState quality = Unit(project, "quality", "Quality", BusinessUnitKind.CostCenter);
        BusinessUnitState industrialization = Unit(project, "production-engineering", "Production Engineering / Industrialization", BusinessUnitKind.CostCenter);
        BusinessUnitState maintenance = Unit(project, "maintenance", "Maintenance", BusinessUnitKind.CostCenter);
        BusinessUnitState lab = Unit(project, "test-laboratory", "Test Laboratory", BusinessUnitKind.CostCenter);
        BusinessUnitState rd = Unit(project, "product-development-rd", "Product Development / R&D", BusinessUnitKind.CostCenter);
        BusinessUnitState sales = Unit(project, "sales-account-management", "Sales & Account Management", BusinessUnitKind.RevenueCenter);
        BusinessUnitState admin = Unit(project, "hr-finance-admin", "HR / Finance / Admin", BusinessUnitKind.CostCenter);
        BusinessUnitState management = Unit(project, "management", "Management", BusinessUnitKind.CostCenter);
        BusinessUnitState treasury = Unit(project, "treasury", "Treasury", BusinessUnitKind.Project);

        PaymentTermState net30 = Payment("net-30", 30, 0m);
        PaymentTermState net45 = Payment("net-45", 45, 0m);
        PaymentTermState net60 = Payment("net-60", 60, 0m);
        PaymentTermState net90 = Payment("net-90", 90, 0m);

        var products = new[]
        {
            Product(project, "SCU-A", "Shared Control Unit SCU-A", 145m, 81.5m, "revenue-hardware", "cogs-hardware"),
            Product(project, "GSH-G", "Custom Sensor Hub GSH-G", 210m, 129m, "revenue-hardware", "cogs-hardware"),
            Product(project, "DBI-D", "Battery Interface DBI-D", 260m, 166m, "revenue-hardware", "cogs-hardware"),
            Product(project, "EGM-E", "Gateway Module EGM-E", 235m, 145m, "revenue-hardware", "cogs-hardware")
        };

        var counterparties = new[]
        {
            Counterparty(project, "alpha-mobility", "Alpha Mobility"),
            Counterparty(project, "beta-robotics", "Beta Robotics"),
            Counterparty(project, "gamma-appliances", "Gamma Appliances"),
            Counterparty(project, "delta-energy", "Delta Energy"),
            Counterparty(project, "epsilon-machines", "Epsilon Machines")
        };

        var contracts = new[]
        {
            Contract(project, "alpha-scu-a", "Alpha SCU-A", net60.PaymentTermId, sales, "SCU-A", counterparties[0].CounterpartyId),
            Contract(project, "beta-scu-a", "Beta SCU-A", net30.PaymentTermId, sales, "SCU-A", counterparties[1].CounterpartyId),
            Contract(project, "gamma-gsh-g", "Gamma GSH-G", net90.PaymentTermId, sales, "GSH-G", counterparties[2].CounterpartyId),
            Contract(project, "delta-dbi-d", "Delta DBI-D", net60.PaymentTermId, sales, "DBI-D", counterparties[3].CounterpartyId),
            Contract(project, "epsilon-egm-e", "Epsilon EGM-E", net45.PaymentTermId, sales, "EGM-E", counterparties[4].CounterpartyId)
        };

        var schedules = new[]
        {
            Demand(project, "alpha-scu-a-demand", contracts[0], "SCU-A", sales, 1200m, 0m, [new(new DateOnly(2026, 4, 1), 0m, 1.05m, "quarterly growth", Empty()), new(new DateOnly(2026, 7, 1), 0m, 1.05m, "quarterly growth", Empty()), new(new DateOnly(2026, 10, 1), 0m, 1.05m, "quarterly growth", Empty())]),
            Demand(project, "beta-scu-a-demand", contracts[1], "SCU-A", sales, 900m, 0m, [new(new DateOnly(2026, 7, 1), 0m, 0.95m, "H2 2026 decline", Empty()), new(new DateOnly(2027, 1, 1), 0m, 1.10m, "H1 2027 growth", Empty())]),
            Demand(project, "gamma-gsh-g-demand", contracts[2], "GSH-G", sales, 0m, 0m, [new(new DateOnly(2026, 7, 1), 400m, 1m, "production start", Empty()), new(new DateOnly(2026, 8, 1), 100m, 1m, "ramp", Empty()), new(new DateOnly(2026, 9, 1), 100m, 1m, "ramp", Empty()), new(new DateOnly(2026, 10, 1), 100m, 1m, "ramp", Empty()), new(new DateOnly(2026, 11, 1), 100m, 1m, "ramp", Empty()), new(new DateOnly(2026, 12, 1), 100m, 1m, "ramp", Empty()), new(new DateOnly(2027, 4, 1), 0m, 1.08m, "quarterly growth", Empty())]),
            Demand(project, "delta-dbi-d-demand", contracts[3], "DBI-D", sales, 0m, 0m, [new(new DateOnly(2026, 10, 1), 250m, 1m, "production start", Empty()), new(new DateOnly(2026, 11, 1), 90m, 1m, "ramp", Empty()), new(new DateOnly(2026, 12, 1), 90m, 1m, "ramp", Empty()), new(new DateOnly(2027, 1, 1), 90m, 1m, "ramp", Empty()), new(new DateOnly(2027, 2, 1), 90m, 1m, "ramp", Empty()), new(new DateOnly(2027, 3, 1), 90m, 1m, "ramp", Empty()), new(new DateOnly(2027, 7, 1), 0m, 1.12m, "half-year growth", Empty())]),
            Demand(project, "epsilon-egm-e-demand", contracts[4], "EGM-E", sales, 0m, 0m, [new(new DateOnly(2027, 4, 1), 300m, 1m, "production start", Empty()), new(new DateOnly(2027, 5, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 6, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 7, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 8, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 9, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 10, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 11, 1), 62.5m, 1m, "ramp", Empty()), new(new DateOnly(2027, 12, 1), 62.5m, 1m, "ramp", Empty())])
        };

        WorkforcePlanState workforce = new(project, "s3-workforce",
        [
            Staffing(project, molding, "Plastic Molding", 30m, 2450m, 0.32m),
            Staffing(project, electronics, "Electronics Assembly & Testing", 30m, 2650m, 0.32m),
            Staffing(project, warehouse, "Logistics / Warehouse", 12m, 2300m, 0.32m),
            Staffing(project, quality, "Quality", 8m, 3250m, 0.32m),
            Staffing(project, industrialization, "Production Engineering / Industrialization", 10m, 4600m, 0.32m),
            Staffing(project, maintenance, "Maintenance", 6m, 3100m, 0.32m),
            Staffing(project, lab, "Test Laboratory", 8m, 4100m, 0.32m),
            Staffing(project, rd, "Product Development / R&D", 18m, 5200m, 0.32m),
            Staffing(project, sales, "Sales & Account Management", 6m, 4500m, 0.32m),
            Staffing(project, admin, "HR / Finance / Admin", 4m, 3600m, 0.32m),
            Staffing(project, management, "Management", 8m, 7500m, 0.32m)
        ], [], Empty());

        var implementationProjects = new[]
        {
            Implementation(project, "gamma-development", contracts[2], Amount.FromDecimal(480000m), Amount.FromDecimal(340000m), new DateOnly(2026, 1, 1)),
            Implementation(project, "delta-development", contracts[3], Amount.FromDecimal(350000m), Amount.FromDecimal(260000m), new DateOnly(2026, 3, 1)),
            Implementation(project, "epsilon-development", contracts[4], Amount.FromDecimal(620000m), Amount.FromDecimal(460000m), new DateOnly(2026, 9, 1))
        };

        var workOrders = new[]
        {
            WorkOrder(project, "gamma-paid-development", contracts[2], "GSH-G", "Gamma paid product development", ScenarioWorkOrderKind.ProductionEngineering, sales, rd, null, "gamma-development", new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31), ScenarioCadence.Monthly, 1250m, 0m, "milestone-invoice-from-development-hours", [Allocation("gamma-rd", rd, 0.55m, "customer-development"), Allocation("gamma-industrialization", industrialization, 0.30m, "production-preparation"), Allocation("gamma-lab-quality", lab, 0.15m, "validation-testing")]),
            WorkOrder(project, "delta-paid-development", contracts[3], "DBI-D", "Delta paid product development", ScenarioWorkOrderKind.ProductionEngineering, sales, rd, null, "delta-development", new DateOnly(2026, 3, 1), new DateOnly(2026, 9, 30), ScenarioCadence.Monthly, 980m, 0m, "milestone-invoice-from-development-hours", [Allocation("delta-rd", rd, 0.50m, "customer-development"), Allocation("delta-industrialization", industrialization, 0.35m, "production-preparation"), Allocation("delta-quality", quality, 0.15m, "quality-planning")]),
            WorkOrder(project, "epsilon-paid-development", contracts[4], "EGM-E", "Epsilon paid product development", ScenarioWorkOrderKind.ProductionEngineering, sales, rd, null, "epsilon-development", new DateOnly(2026, 9, 1), new DateOnly(2027, 3, 31), ScenarioCadence.Monthly, 1450m, 0m, "milestone-invoice-from-development-hours", [Allocation("epsilon-rd", rd, 0.55m, "customer-development"), Allocation("epsilon-industrialization", industrialization, 0.30m, "production-preparation"), Allocation("epsilon-lab-quality", lab, 0.15m, "validation-testing")]),
            InternalWorkOrder(project, "continuous-production-improvement", "Continuous production improvement", "internal-production-engineering", ScenarioWorkOrderKind.InternalProject, management, industrialization, Start, null, ScenarioCadence.Monthly, 640m, [Allocation("industrialization", industrialization, 0.55m, "capacity-balancing"), Allocation("maintenance", maintenance, 0.25m, "machine-uptime"), Allocation("quality", quality, 0.20m, "process-capability")])
        };

        var productFlows = products
            .Select(product => HardwareProductFlow(project, product, contracts.First(contract => contract.ProductOrServiceIds.Contains(product.ProductSkuId, StringComparer.Ordinal)), $"flow-{product.ProductSkuId}", molding, electronics, warehouse, quality))
            .ToArray();

        return new ScenarioBusinessModelState(
            project,
            "S3",
            "S3 Tier-2 Hardware And Engineering Supplier",
            "EUR",
            Start,
            End,
            Amount.FromDecimal(1200000m),
            Amount.FromDecimal(300000m),
            [treasury, molding, electronics, warehouse, quality, industrialization, maintenance, lab, rd, sales, admin, management],
            [net30, net45, net60, net90],
            counterparties,
            contracts,
            [],
            products,
            schedules,
            implementationProjects,
            [workforce],
            [],
            [],
            [
                Bom(project, "SCU-A", 0.975m, 11.50m, 48.00m, 22.00m),
                Bom(project, "GSH-G", 0.965m, 16.00m, 82.00m, 31.00m),
                Bom(project, "DBI-D", 0.960m, 18.00m, 110.00m, 38.00m),
                Bom(project, "EGM-E", 0.968m, 15.00m, 95.00m, 35.00m)
            ],
            [new(project, "materials-warehouse", "Materials warehouse", warehouse.BusinessUnitId, "warehouse", Empty()), new(project, "wip-hardware", "Hardware WIP", electronics.BusinessUnitId, "wip", Empty())],
            [new(project, "hardware-route", "hardware-products", [new("plastic-molding", molding.BusinessUnitId, "Plastic Molding", 30m, "materials-warehouse", "wip-hardware", Empty()), new("electronics-test", electronics.BusinessUnitId, "Electronics Assembly/Test", 45m, "wip-hardware", "finished-goods", Empty()), new("quality-release", quality.BusinessUnitId, "Quality release", 15m, "wip-hardware", "finished-goods", Empty())], Empty())],
            workOrders,
            productFlows,
            [new(project, "factory-lease", molding.BusinessUnitId, "Factory lease and utilities", Amount.FromDecimal(90000m), Empty())],
            [
                new(project, "plastic-maintenance", maintenance.BusinessUnitId, Amount.FromDecimal(18000m), Amount.FromDecimal(25000m), null, Empty()),
                new(project, "electronics-maintenance", maintenance.BusinessUnitId, Amount.FromDecimal(12000m), null, Amount.FromDecimal(40000m), Empty())
            ],
            products.Select(x => new QualityGateState(project, $"quality-{x.ProductSkuId}", x.ProductSkuId, quality.BusinessUnitId, 0.015m, x.ProductSkuId switch { "SCU-A" => 0.02m, "GSH-G" => 0.03m, "DBI-D" => 0.035m, _ => 0.028m }, Empty())).ToArray(),
            [new(project, "test-lab", lab.BusinessUnitId, Amount.FromDecimal(0m), Empty())],
            [],
            [],
            [
                new(project, "gamma-tooling", industrialization.BusinessUnitId, "Gamma tooling", Amount.FromDecimal(320000m), new DateOnly(2026, 3, 1), 60, Empty()),
                new(project, "delta-tooling", industrialization.BusinessUnitId, "Delta tooling", Amount.FromDecimal(220000m), new DateOnly(2026, 8, 1), 60, Empty()),
                new(project, "epsilon-tooling", industrialization.BusinessUnitId, "Epsilon tooling", Amount.FromDecimal(450000m), new DateOnly(2027, 1, 1), 60, Empty())
            ],
            [
                Cost(project, "facility-lease", molding, "opex-factory-lease", FinancialStatementSection.Opex, 90000m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "plastic-maintenance", maintenance, "opex-maintenance", FinancialStatementSection.Opex, 18000m, EconomicImpactKind.CashAndAccrual),
                Cost(project, "electronics-maintenance", maintenance, "opex-maintenance", FinancialStatementSection.Opex, 12000m, EconomicImpactKind.CashAndAccrual)
            ],
            [],
            Meta("baselineWorkbook", "scenario-baseline-financial-models.xlsx"));
    }

    private static BusinessUnitState Unit(string project, string path, string name, BusinessUnitKind kind)
    {
        Address address = Address.FromPublicKey(System.Text.Encoding.UTF8.GetBytes($"{project}:{path}"));
        return new BusinessUnitState(project, EconomySimulatorIdentifiers.BusinessUnitId(project, path), name, kind, null, address, path, kind is BusinessUnitKind.CostCenter ? path : null, kind is BusinessUnitKind.RevenueCenter ? path : null, null, "EUR", false, false, BusinessUnitStatus.Active, [address], Empty(), Empty());
    }

    private static PaymentTermState Payment(string id, int dueDays, decimal sameMonthRate)
        => new(id, dueDays == 0 ? PaymentTermKind.Immediate : PaymentTermKind.NetDays, dueDays, sameMonthRate, 0, Empty());

    private static CounterpartyState Counterparty(string project, string id, string name)
        => new(project, id, name, "customer", null, null, "EUR", Empty());

    private static CustomerContractState Contract(string project, string id, string name, string paymentTermId, BusinessUnitState revenueUnit, params string[] productIds)
        => Contract(project, id, name, paymentTermId, revenueUnit, productIds, id);

    private static CustomerContractState Contract(string project, string id, string name, string paymentTermId, BusinessUnitState revenueUnit, IReadOnlyList<string> productIds)
        => Contract(project, id, name, paymentTermId, revenueUnit, productIds, id);

    private static CustomerContractState Contract(string project, string id, string name, string paymentTermId, BusinessUnitState revenueUnit, string productId, string counterpartyId)
        => Contract(project, id, name, paymentTermId, revenueUnit, [productId], counterpartyId);

    private static CustomerContractState Contract(string project, string id, string name, string paymentTermId, BusinessUnitState revenueUnit, IReadOnlyList<string> productIds, string counterpartyId)
        => new(project, id, counterpartyId, name, paymentTermId, Start, null, revenueUnit.BusinessUnitId, "EUR", productIds, Empty());

    private static DemandScheduleState Demand(string project, string id, CustomerContractState contract, string productId, BusinessUnitState unit, decimal initialQuantity, decimal monthlyGrowth = 0m, IReadOnlyList<DemandChangeState>? changes = null)
        => new(project, id, contract.CustomerContractId, productId, unit.BusinessUnitId, Start, null, initialQuantity, ScenarioCadence.Monthly, monthlyGrowth, changes ?? [], Empty());

    private static ImplementationProjectState Implementation(string project, string id, CustomerContractState contract, Amount value, Amount cost, DateOnly start)
        => new(project, id, contract.CustomerContractId, id, value, cost, start, [new("kickoff", start, 0.4m, 0.4m, "Kickoff"), new("acceptance", start.AddMonths(3), 0.4m, 0.4m, "Acceptance"), new("release", start.AddMonths(6), 0.2m, 0.2m, "Release")], Empty());

    private static DepartmentStaffingState Staffing(string project, BusinessUnitState unit, string role, decimal fte, decimal salary, decimal burden, params HeadcountEventState[] events)
        => new($"{project}:{EconomySimulatorIdentifiers.NormalizeSegment(role)}", unit.BusinessUnitId, role, fte, Amount.FromDecimal(salary), burden, events, Empty());

    private static ProductSkuState Product(string project, string id, string name, decimal price, decimal unitCost, string revenueCategory, string cogsCategory)
        => new(project, id, name, "unit", Amount.FromDecimal(price), Amount.FromDecimal(unitCost), revenueCategory, cogsCategory, Empty());

    private static ScenarioCostPlanState Cost(string project, string id, BusinessUnitState unit, string category, FinancialStatementSection section, decimal amount, EconomicImpactKind impact, DateOnly? start = null, DateOnly? end = null)
        => new(project, id, unit.BusinessUnitId, category, section, Amount.FromDecimal(amount), impact, 0, start ?? Start, end, Empty());

    private static WorkAllocationRuleState Allocation(string id, BusinessUnitState workSource, decimal percent, string strategy)
        => new(id, workSource.BusinessUnitId, percent, strategy, Empty());

    private static WorkOrderState WorkOrder(
        string project,
        string id,
        CustomerContractState contract,
        string productOrServiceId,
        string name,
        ScenarioWorkOrderKind kind,
        BusinessUnitState requestingUnit,
        BusinessUnitState deliveryUnit,
        string? demandScheduleId,
        string? implementationProjectId,
        DateOnly start,
        DateOnly? end,
        ScenarioCadence cadence,
        decimal fixedHours,
        decimal hoursPerDemandUnit,
        string invoicePolicy,
        IReadOnlyList<WorkAllocationRuleState> allocations)
        => new(
            project,
            id,
            contract.CustomerContractId,
            productOrServiceId,
            name,
            kind,
            requestingUnit.BusinessUnitId,
            deliveryUnit.BusinessUnitId,
            demandScheduleId,
            implementationProjectId,
            start,
            end,
            cadence,
            fixedHours,
            hoursPerDemandUnit,
            invoicePolicy,
            allocations,
            [],
            Empty());

    private static WorkOrderState InternalWorkOrder(
        string project,
        string id,
        string name,
        string productOrServiceId,
        ScenarioWorkOrderKind kind,
        BusinessUnitState requestingUnit,
        BusinessUnitState deliveryUnit,
        DateOnly start,
        DateOnly? end,
        ScenarioCadence cadence,
        decimal fixedHours,
        IReadOnlyList<WorkAllocationRuleState> allocations)
        => new(
            project,
            id,
            null,
            productOrServiceId,
            name,
            kind,
            requestingUnit.BusinessUnitId,
            deliveryUnit.BusinessUnitId,
            null,
            null,
            start,
            end,
            cadence,
            fixedHours,
            0m,
            "internal-capacity-allocation",
            allocations,
            [],
            Empty());

    private static ProductFlowPlanState BakeryProductFlow(
        string project,
        ProductSkuState product,
        CustomerContractState contract,
        string id,
        BusinessUnitState production,
        BusinessUnitState warehouse,
        BusinessUnitState sales)
        => new(
            project,
            id,
            product.ProductSkuId,
            $"{product.DisplayName} bakery flow",
            $"{product.ProductSkuId}-demand",
            contract.CustomerContractId,
            1m,
            [
                ProductStep("issue-ingredients", ScenarioProductFlowStepKind.MaterialIssue, warehouse, "raw-material-warehouse", "bakery-production", 1m, 0.35m, "Warehouse issues ingredients and packaging"),
                ProductStep("mix-bake-pack", ScenarioProductFlowStepKind.Production, production, "raw-material-warehouse", "bakery-production", 1m, 0.45m, "Production mixes, bakes, cools, and packs"),
                ProductStep("customer-handoff", ScenarioProductFlowStepKind.CustomerDelivery, sales, "bakery-production", "customer-delivery", 1m, 0.20m, "Sales hands finished bakery products to customer")
            ],
            Empty());

    private static ProductFlowPlanState HardwareProductFlow(
        string project,
        ProductSkuState product,
        CustomerContractState contract,
        string id,
        BusinessUnitState molding,
        BusinessUnitState electronics,
        BusinessUnitState warehouse,
        BusinessUnitState quality)
        => new(
            project,
            id,
            product.ProductSkuId,
            $"{product.DisplayName} hardware flow",
            null,
            contract.CustomerContractId,
            1m,
            [
                ProductStep("material-kitting", ScenarioProductFlowStepKind.MaterialIssue, warehouse, "materials-warehouse", "plastic-molding", 1m, 0.20m, "Logistics kits plastic granules and electronic components"),
                ProductStep("plastic-molding", ScenarioProductFlowStepKind.Production, molding, "materials-warehouse", "wip-hardware", 1m, 0.20m, "Plastic case casting"),
                ProductStep("electronics-assembly-test", ScenarioProductFlowStepKind.Assembly, electronics, "wip-hardware", "wip-hardware", 1m, 0.35m, "Electronics assembly and functional testing"),
                ProductStep("quality-release", ScenarioProductFlowStepKind.Quality, quality, "wip-hardware", "finished-goods", 1m, 0.15m, "Quality release"),
                ProductStep("customer-delivery", ScenarioProductFlowStepKind.CustomerDelivery, warehouse, "finished-goods", "customer-delivery", 1m, 0.10m, "Finished goods delivery to customer")
            ],
            Empty());

    private static ProductFlowStepState ProductStep(string id, ScenarioProductFlowStepKind kind, BusinessUnitState unit, string fromLocation, string toLocation, decimal quantityMultiplier, decimal valuePercent, string description)
        => new(id, kind, unit.BusinessUnitId, fromLocation, toLocation, quantityMultiplier, valuePercent, description, Empty());

    private static IReadOnlyList<IngredientState> Ingredients(string project)
        =>
        [
            new(project, "flour", "Flour kg", "kg", Amount.FromDecimal(0.55m), 14, 10, Empty()),
            new(project, "yeast", "Yeast kg", "kg", Amount.FromDecimal(4.00m), 14, 10, Empty()),
            new(project, "sugar", "Sugar kg", "kg", Amount.FromDecimal(0.90m), 14, 10, Empty()),
            new(project, "butter", "Butter kg", "kg", Amount.FromDecimal(6.50m), 7, 5, Empty()),
            new(project, "milk", "Milk liter", "liter", Amount.FromDecimal(1.10m), 7, 5, Empty()),
            new(project, "eggs", "Eggs each", "each", Amount.FromDecimal(0.18m), 7, 5, Empty()),
            new(project, "seeds-extras", "Seeds / extras kg", "kg", Amount.FromDecimal(3.50m), 14, 10, Empty()),
            new(project, "packaging", "Packaging", "unit", Amount.FromDecimal(0.06m), 30, 20, Empty())
        ];

    private static IReadOnlyList<RecipeState> Recipes(string project, IReadOnlyList<ProductSkuState> products)
        => products.Select((x, index) => new RecipeState(project, $"recipe-{x.ProductSkuId}", x.ProductSkuId, index switch { 0 => 0.03m, 1 => 0.02m, 2 => 0.05m, 3 => 0.06m, _ => 0.04m }, [new("packaging", 1m)], Empty())).ToArray();

    private static BillOfMaterialState Bom(string project, string productId, decimal yield, decimal plastic, decimal electronics, decimal labor)
        => new(project, $"bom-{productId}", productId, yield, [new("plastic-granules-inserts", Amount.FromDecimal(plastic), 30, 20, Empty()), new("electronics-pcb-components", Amount.FromDecimal(electronics), 45, 45, Empty()), new("direct-labor-overhead", Amount.FromDecimal(labor), 0, 0, Empty())], Empty());

    private static IReadOnlyDictionary<string, string> Empty() => new Dictionary<string, string>();

    private static IReadOnlyDictionary<string, string> Meta(string key, string value)
        => new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value };
}
