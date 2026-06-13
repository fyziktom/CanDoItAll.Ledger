using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class SimulationApiEndpointTests
{
    [Fact]
    public void FunctionCatalog_CoversControlAnalysisAndCfoGroups()
    {
        SimulatorFunctionCatalog catalog = new SimulationFunctionCatalogService().GetCatalog();

        Assert.Contains(catalog.Functions, item => item.Group == "Run lifecycle" && item.FunctionName == "StartRun");
        Assert.Contains(catalog.Functions, item => item.Group == "Run lifecycle" && item.FunctionName == "StepRun");
        Assert.Contains(catalog.Functions, item => item.Group == "Run lifecycle" && item.FunctionName == "ApplyIntervention");
        Assert.Contains(catalog.Functions, item => item.Group == "Run observation" && item.FunctionName == "BuildObservation");
        Assert.Contains(catalog.Endpoints, item => item.Group == "Runs" && item.Route.EndsWith("/steps", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, item => item.Group == "Runs" && item.Route.EndsWith("/interventions", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, item => item.Group == "Runs" && item.Route.EndsWith("/analysis/finance", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, item => item.Group == "Runs" && item.Route.EndsWith("/analysis/period-summary", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, item => item.Route.EndsWith("/financial-schedules", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, item => item.Group == "Templates" && item.Route == "/api/simulator/templates");
        Assert.Contains(catalog.Endpoints, item => item.Group == "Projects" && item.Method == "PUT");
        Assert.Contains(catalog.CfoCoverage, item => item.Lens == "Liquidity and runway" && item.Status == "Implemented");
        Assert.Contains(catalog.CfoCoverage, item => item.Lens == "Treasury, debt, and cash pooling" && item.Status == "Partial");
    }

    [Fact]
    public void FinanceAnalysis_UsesRunStateAndCfoLenses()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService lifecycle = CreateLifecycleService();
        SimulationRunConsoleState run = lifecycle.EnsureProjectRuns(project)[0];

        SimulationFinanceAnalysisSnapshot analysis = new SimulationFinanceAnalysisService(new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T12:00:00Z")))
            .Build(run);

        Assert.Equal(project.Summary.ProjectId, analysis.ProjectId);
        Assert.Equal(run.Run.RunId, analysis.RunId);
        Assert.True(analysis.Summary.TotalCurrentCash > 0m);
        Assert.True(analysis.Summary.KpiCount >= 6);
        Assert.Contains(analysis.CfoLenses, lens => lens.Lens == "Liquidity and runway" && lens.DataFields.Contains("cashHeadroom"));
        Assert.Contains(analysis.CfoLenses, lens => lens.Lens == "Controls and governance");
        Assert.Contains(analysis.ProfessionalReviewWarnings, warning => warning.Contains("tax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PeriodSummary_UsesGraphOutputsForQuarterAndYearViews()
    {
        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        InMemorySimulationRunLifecycleService lifecycle = CreateLifecycleService();
        SimulationRunConsoleState run = lifecycle.EnsureProjectRuns(project)[0];
        var service = new SimulationPeriodSummaryService();

        SimulationPeriodSummarySnapshot quarter = service.Build(run, SimulationPeriodSummaryGranularity.Quarter);
        SimulationPeriodSummarySnapshot year = service.Build(run, SimulationPeriodSummaryGranularity.Year);

        Assert.Equal(SimulationPeriodSummaryGranularity.Quarter, quarter.Granularity);
        Assert.NotEmpty(quarter.Periods);
        Assert.Contains(quarter.Periods, period => period.PeriodCode.Contains("-Q", StringComparison.Ordinal));
        Assert.True(quarter.TotalIncome > 0m);
        Assert.True(quarter.TotalExpenses > 0m);
        Assert.NotEmpty(quarter.IncomeDistribution);
        Assert.NotEmpty(quarter.ExpenseDistribution);
        Assert.Equal(SimulationPeriodSummaryGranularity.Year, year.Granularity);
        Assert.Contains(year.Periods, period => period.PeriodCode == project.Summary.PlanningStartDate.Year.ToString());
        Assert.Equal(quarter.TotalIncome, year.TotalIncome);
        Assert.Equal(quarter.TotalExpenses, year.TotalExpenses);
    }

    [Fact]
    public async Task SimulatorApi_ExposesGroupedControlAndFinanceAnalysisRoutes()
    {
        int port = GetFreeTcpPort();
        WebApplicationBuilder builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddEconomySimulator();

        await using WebApplication app = builder.Build();
        app.MapEconomySimulatorApi();

        SimulationProjectState project = EconomySimulatorSampleData.CreateProjectState();
        app.Services.GetRequiredService<ISimulationProjectWorkspaceService>()
            .UpsertProject(project, DateTimeOffset.Parse("2026-05-14T12:00:00Z"));

        await app.StartAsync(CancellationToken.None);
        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

        SimulatorFunctionCatalog? catalog = await httpClient.GetFromJsonAsync<SimulatorFunctionCatalog>(
            "api/simulator/metadata/function-map",
            CancellationToken.None);
        Assert.NotNull(catalog);
        Assert.Contains(catalog!.Endpoints, endpoint => endpoint.Route.Contains("/runs/{runId}/analysis/finance", StringComparison.Ordinal));
        Assert.Contains(catalog.Endpoints, endpoint => endpoint.Route.Contains("/runs/{runId}/analysis/period-summary", StringComparison.Ordinal));

        SimulationProjectSummary[]? projects = await httpClient.GetFromJsonAsync<SimulationProjectSummary[]>(
            "api/simulator/projects",
            CancellationToken.None);
        Assert.NotNull(projects);
        Assert.Contains(projects!, item => item.ProjectId == project.Summary.ProjectId);
        Assert.Contains(projects!, item => item.ProjectId == "s1-online-services");
        Assert.Contains(projects!, item => item.ProjectId == "s2-bakery");
        Assert.Contains(projects!, item => item.ProjectId == "s3-hardware-engineering");
        Assert.Contains(projects!, item => item.ProjectId == "vef-financial-plan");

        ScenarioFinancialScheduleState[]? vefSchedules = await httpClient.GetFromJsonAsync<ScenarioFinancialScheduleState[]>(
            "api/simulator/scenario-templates/VEF/financial-schedules",
            CancellationToken.None);
        Assert.NotNull(vefSchedules);
        Assert.Contains(vefSchedules!, schedule => schedule.RequiresApproval);
        Assert.Contains(vefSchedules!, schedule => schedule.Metadata.TryGetValue("assemblySource", out string? source) && source == "vef-financial-plan-assembly");
        Assert.DoesNotContain(vefSchedules!, schedule => schedule.Metadata.ContainsKey("sourceWorkbook"));

        EconomySimulatorTemplateState[]? templates = await httpClient.GetFromJsonAsync<EconomySimulatorTemplateState[]>(
            "api/simulator/templates",
            CancellationToken.None);
        Assert.NotNull(templates);
        EconomySimulatorTemplateState plannedExpense = Assert.Single(templates!, template => template.TemplateId == "financial.planned-expense-request.v1");
        Assert.Contains("planned-expense", plannedExpense.Metadata["tags"], StringComparison.Ordinal);
        Assert.Contains("rule.purchase-approval.v1", plannedExpense.Metadata["requiresTemplates"], StringComparison.Ordinal);

        using HttpResponseMessage startResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/projects/{project.Summary.ProjectId}/runs",
            new SimulationRunStartApiRequest(project.RunSetups[0].SetupId, DateTimeOffset.Parse("2026-05-16T10:15:00Z"), SimulationRunMode.Manual),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        SimulationRunStartApiResponse? start = await startResponse.Content.ReadFromJsonAsync<SimulationRunStartApiResponse>(CancellationToken.None);
        Assert.NotNull(start);
        Assert.True(start!.Command.Succeeded);
        Assert.NotNull(start.Run);
        Assert.Equal(SimulationRunMode.Manual, start.Run!.Run.RunMode);
        Assert.Equal(1, start.Run.Run.SelectedStep);
        Assert.Equal(1, start.Run.Run.AvailableStep);

        SimulationFinanceAnalysisSnapshot? finance = await httpClient.GetFromJsonAsync<SimulationFinanceAnalysisSnapshot>(
            $"api/simulator/runs/{start.Command.RunId}/analysis/finance",
            CancellationToken.None);
        Assert.NotNull(finance);
        Assert.True(finance!.Summary.SourceMapCoveragePercent > 0m);
        Assert.Contains(finance.CfoLenses, lens => lens.Lens == "Management reporting");

        using HttpResponseMessage nextStepResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/runs/{start.Command.RunId}/steps",
            new SimulationRunStepApiRequest(SimulationRunStepRequestKind.Next),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, nextStepResponse.StatusCode);
        SimulationRunStepApiResponse? nextStep = await nextStepResponse.Content.ReadFromJsonAsync<SimulationRunStepApiResponse>(CancellationToken.None);
        Assert.Equal(2, nextStep?.Run?.Run.SelectedStep);
        Assert.Equal(2, nextStep?.Run?.Run.AvailableStep);

        using HttpResponseMessage previousStepResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/runs/{start.Command.RunId}/steps",
            new SimulationRunStepApiRequest(SimulationRunStepRequestKind.Previous),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, previousStepResponse.StatusCode);
        SimulationRunStepApiResponse? previousStep = await previousStepResponse.Content.ReadFromJsonAsync<SimulationRunStepApiResponse>(CancellationToken.None);
        Assert.Equal(1, previousStep?.Run?.Run.SelectedStep);
        Assert.Equal(2, previousStep?.Run?.Run.AvailableStep);

        SimulationProjectState? s1Project = await httpClient.GetFromJsonAsync<SimulationProjectState>(
            "api/simulator/projects/s1-online-services",
            CancellationToken.None);
        Assert.NotNull(s1Project);
        Assert.Equal("S1", s1Project!.Metadata["scenarioCode"]);

        using HttpResponseMessage s1StartResponse = await httpClient.PostAsJsonAsync(
            "api/simulator/projects/s1-online-services/runs",
            new SimulationRunStartApiRequest(s1Project.RunSetups[0].SetupId, DateTimeOffset.Parse("2026-05-16T10:30:00Z"), SimulationRunMode.Manual),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, s1StartResponse.StatusCode);
        SimulationRunStartApiResponse? s1Start = await s1StartResponse.Content.ReadFromJsonAsync<SimulationRunStartApiResponse>(CancellationToken.None);
        Assert.NotNull(s1Start?.Run);
        Assert.Contains(s1Start!.Run!.Kpis, kpi => kpi.Code == "revenue");
        Assert.Contains(s1Start.Run.Kpis, kpi => kpi.Code == "serviceHours");
        EconomyGraphData s1RevenueGraph = Assert.Single(s1Start.Run.Observation.Graphs, graph => graph.Filter.Metric == ScenarioGraphMetric.Revenue);
        Assert.True(s1RevenueGraph.Totals["nonCashIncome"] > 0m);
        Assert.DoesNotContain(s1Start.Run.Kpis, kpi => kpi.Code == "realGdp");

        using HttpResponseMessage s1NextResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/runs/{s1Start.Command.RunId}/steps",
            new SimulationRunStepApiRequest(SimulationRunStepRequestKind.Next),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, s1NextResponse.StatusCode);
        SimulationRunStepApiResponse? s1Next = await s1NextResponse.Content.ReadFromJsonAsync<SimulationRunStepApiResponse>(CancellationToken.None);
        Assert.NotNull(s1Next?.Run);
        Assert.NotEqual(s1Start.Run.Run.OutputHash, s1Next!.Run!.Run.OutputHash);
        EconomyGraphData s1NextCashGraph = Assert.Single(s1Next.Run.Observation.Graphs, graph => graph.Filter.Metric == ScenarioGraphMetric.CashFlow);
        Assert.True(s1NextCashGraph.Totals["rowCount"] > s1RevenueGraph.Totals["rowCount"]);

        using HttpResponseMessage s1InterventionResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/runs/{s1Start.Command.RunId}/interventions",
            new SimulationRunInterventionRequest(
                SimulationRunInterventionKind.DemandChange,
                "enterprise-demand",
                new DateOnly(2026, 3, 1),
                Multiplier: 1.5m,
                DisplayName: "Enterprise demand shock",
                Reason: "Management changed customer demand assumption"),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, s1InterventionResponse.StatusCode);
        SimulationRunInterventionApiResponse? s1Intervention = await s1InterventionResponse.Content.ReadFromJsonAsync<SimulationRunInterventionApiResponse>(CancellationToken.None);
        Assert.NotNull(s1Intervention?.Run);
        Assert.NotNull(s1Intervention!.Intervention);
        Assert.Contains(s1Intervention.Run!.Project.BusinessObjectDefinitions, definition => definition.ObjectType == EconomySimulatorObjectTypes.RuntimeIntervention);
        Assert.Equal(2, s1Intervention.Run.Run.AvailableStep);

        using HttpResponseMessage s1InterventionStepResponse = await httpClient.PostAsJsonAsync(
            $"api/simulator/runs/{s1Start.Command.RunId}/steps",
            new SimulationRunStepApiRequest(SimulationRunStepRequestKind.Next),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, s1InterventionStepResponse.StatusCode);
        SimulationRunStepApiResponse? s1InterventionStep = await s1InterventionStepResponse.Content.ReadFromJsonAsync<SimulationRunStepApiResponse>(CancellationToken.None);
        Assert.NotNull(s1InterventionStep?.Run);
        Assert.Contains(s1InterventionStep!.Run!.Kpis, kpi => kpi.Code == "runtimeInterventions" && kpi.Value == "1");

        SimulationOutputSourceRef[]? s1SourceMap = await httpClient.GetFromJsonAsync<SimulationOutputSourceRef[]>(
            $"api/simulator/runs/{s1Start.Command.RunId}/analysis/source-map",
            CancellationToken.None);
        Assert.NotNull(s1SourceMap);
        Assert.Contains(s1SourceMap!, source => source.OutputCode == "serviceHours");

        SimulationFinanceAnalysisSnapshot? s1Finance = await httpClient.GetFromJsonAsync<SimulationFinanceAnalysisSnapshot>(
            $"api/simulator/runs/{s1Start.Command.RunId}/analysis/finance",
            CancellationToken.None);
        Assert.NotNull(s1Finance);
        Assert.True(s1Finance!.Summary.Revenue > 0m);
        Assert.True(s1Finance.Summary.SourceMapCoveragePercent >= 80m);

        SimulationPeriodSummarySnapshot? s1Quarter = await httpClient.GetFromJsonAsync<SimulationPeriodSummarySnapshot>(
            $"api/simulator/runs/{s1Start.Command.RunId}/analysis/period-summary?granularity=Quarter",
            CancellationToken.None);
        Assert.NotNull(s1Quarter);
        Assert.Equal(SimulationPeriodSummaryGranularity.Quarter, s1Quarter!.Granularity);
        Assert.True(s1Quarter.TotalIncome > 0m);
        Assert.NotEmpty(s1Quarter.IncomeDistribution);

        SimulationPeriodSummarySnapshot? s1Year = await httpClient.GetFromJsonAsync<SimulationPeriodSummarySnapshot>(
            $"api/simulator/runs/{s1Start.Command.RunId}/analysis/period-summary?granularity=Year",
            CancellationToken.None);
        Assert.NotNull(s1Year);
        Assert.Equal(SimulationPeriodSummaryGranularity.Year, s1Year!.Granularity);
        Assert.True(s1Year.Periods.Count <= s1Quarter.Periods.Count);

        await app.StopAsync(CancellationToken.None);
    }

    private static InMemorySimulationRunLifecycleService CreateLifecycleService()
    {
        var serializer = new EconomySimulatorJsonSerializer();
        var validation = new SimulationProjectValidationService();
        var importExport = new SimulationProjectImportExportService(serializer, validation);
        var observation = new SimulationRunObservationService(serializer);
        return new InMemorySimulationRunLifecycleService(validation, importExport, observation, serializer, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-14T12:00:00Z")));
    }

    private static int GetFreeTcpPort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : ITimeProvider
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
