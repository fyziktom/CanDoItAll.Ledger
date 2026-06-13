using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CanDoItAll.Economy.Simulator;

public static class EconomySimulatorApiEndpoints
{
    public static RouteGroupBuilder MapEconomySimulatorApi(this IEndpointRouteBuilder endpoints, string pattern = "/api/simulator")
    {
        RouteGroupBuilder api = endpoints.MapGroup(pattern).WithTags("Economy Simulator");
        MapMetadata(api.MapGroup("/metadata"));
        MapProjects(api.MapGroup("/projects"));
        MapTemplates(api.MapGroup("/templates"));
        MapScenarioTemplates(api.MapGroup("/scenario-templates"));
        MapScenarios(api.MapGroup("/scenarios"));
        MapRuns(api.MapGroup("/runs"));
        MapAnalysis(api.MapGroup("/analysis"));
        return api;
    }

    private static void MapMetadata(RouteGroupBuilder group)
    {
        group.MapGet("/function-map", (ISimulationFunctionCatalogService catalog) => Results.Ok(catalog.GetCatalog()));
        group.MapGet("/endpoints", (ISimulationFunctionCatalogService catalog) => Results.Ok(catalog.GetCatalog().Endpoints));
        group.MapGet("/cfo-coverage", (ISimulationFunctionCatalogService catalog) => Results.Ok(catalog.GetCatalog().CfoCoverage));
    }

    private static void MapTemplates(RouteGroupBuilder group)
    {
        group.MapGet("", (IEconomySimulatorTemplateProvider provider) => Results.Ok(provider.GetTemplates()));

        group.MapGet("/{templateId}", (string templateId, IEconomySimulatorTemplateProvider provider) =>
        {
            try
            {
                return Results.Ok(provider.GetTemplate(templateId));
            }
            catch (InvalidOperationException)
            {
                return NotFound("template", templateId);
            }
        });
    }

    private static void MapProjects(RouteGroupBuilder group)
    {
        group.MapGet("", (ISimulationProjectWorkspaceService workspace) => Results.Ok(workspace.ListProjects()));

        group.MapGet("/{projectId}", (string projectId, ISimulationProjectWorkspaceService workspace) =>
            workspace.GetProject(projectId) is { } project ? Results.Ok(project) : NotFound("project", projectId));

        group.MapPut("/{projectId}", (
            string projectId,
            SimulationProjectState project,
            ISimulationProjectWorkspaceService workspace,
            ISimulationProjectValidationService validation) =>
        {
            if (!string.Equals(projectId, project.Summary.ProjectId, StringComparison.Ordinal))
            {
                return Results.Problem(
                    title: "Simulation project request failed",
                    detail: "Route project id must match the project summary id.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            SimulationProjectValidationResult validationResult = validation.Validate(project);
            SimulationProjectState stored = workspace.UpsertProject(project);
            return Results.Ok(new SimulationProjectUpsertResponse(stored, validationResult, true, stored.Summary.StorageMode.ToString()));
        });

        group.MapPost("/validate", (SimulationProjectState project, ISimulationProjectValidationService validation) =>
            Results.Ok(validation.Validate(project)));

        group.MapGet("/{projectId}/export", (
            string projectId,
            ISimulationProjectWorkspaceService workspace,
            ISimulationProjectImportExportService importExport,
            ITimeProvider timeProvider) =>
            workspace.GetProject(projectId) is { } project
                ? Results.Ok(importExport.CreateManifest(project, timeProvider.UtcNow))
                : NotFound("project", projectId));

        group.MapPost("/import/preview", (SimulationProjectImportApiRequest request, ISimulationProjectImportExportService importExport) =>
            Results.Ok(importExport.PreviewImport(request.ManifestJson)));

        group.MapPost("/import", (
            SimulationProjectImportApiRequest request,
            ISimulationProjectImportExportService importExport,
            ISimulationProjectWorkspaceService workspace) =>
        {
            SimulationProjectImportPreview preview = importExport.PreviewImport(request.ManifestJson);
            if (!preview.CanImport || preview.Manifest is null)
            {
                return Results.BadRequest(new SimulationProjectImportApiResponse(preview, null));
            }

            SimulationProjectState stored = workspace.UpsertProject(preview.Manifest.Project);
            return Results.Ok(new SimulationProjectImportApiResponse(preview, stored));
        });

        group.MapGet("/{projectId}/run-setups/{setupId}/hash", (
            string projectId,
            string setupId,
            ISimulationProjectWorkspaceService workspace,
            ISimulationProjectImportExportService importExport) =>
        {
            SimulationProjectState? project = workspace.GetProject(projectId);
            if (project is null)
            {
                return NotFound("project", projectId);
            }

            SimulationRunSetupState? setup = project.RunSetups.FirstOrDefault(candidate => string.Equals(candidate.SetupId, setupId, StringComparison.Ordinal));
            return setup is null
                ? NotFound("run setup", setupId)
                : Results.Ok(new { setupHash = importExport.BuildDeterministicRunSetupHash(project, setup) });
        });

        group.MapPost("/{projectId}/runs", (
            string projectId,
            SimulationRunStartApiRequest request,
            ISimulationProjectWorkspaceService workspace,
            ISimulationRunLifecycleService lifecycle) =>
        {
            SimulationProjectState? project = workspace.GetProject(projectId);
            if (project is null)
            {
                return NotFound("project", projectId);
            }

            SimulationRunCommandResult command = lifecycle.StartRun(project, request.SetupId, request.StartedAtUtc, request.Mode);
            SimulationRunConsoleState? run = command.RunId is null ? null : lifecycle.GetRun(command.RunId);
            var response = new SimulationRunStartApiResponse(command, run);
            return command.Succeeded ? Results.Ok(response) : Results.BadRequest(response);
        });
    }

    private static void MapScenarioTemplates(RouteGroupBuilder group)
    {
        group.MapGet("", (IEconomyScenarioTemplateCatalog catalog) =>
            Results.Ok(catalog.GetScenarios().Select(ToTemplateSummary).ToArray()));

        group.MapGet("/{scenarioCode}", (string scenarioCode, IEconomyScenarioTemplateCatalog catalog) =>
            TryGetScenario(catalog, scenarioCode, out ScenarioBusinessModelState? model)
                ? Results.Ok(model)
                : NotFound("scenario template", scenarioCode));

        group.MapGet("/{scenarioCode}/rule-pack", (string scenarioCode, IEconomyScenarioTemplateCatalog catalog) =>
        {
            if (!TryGetScenario(catalog, scenarioCode, out ScenarioBusinessModelState? model))
            {
                return NotFound("scenario template", scenarioCode);
            }

            try
            {
                return Results.Ok(EconomySimulatorScenarioRulePackCatalog.CreateRulePack(model));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Simulation rule-pack request failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        group.MapGet("/{scenarioCode}/financial-schedules", (string scenarioCode, IEconomyScenarioTemplateCatalog catalog) =>
            TryGetScenario(catalog, scenarioCode, out ScenarioBusinessModelState? model)
                ? Results.Ok(model.FinancialSchedules)
                : NotFound("scenario template", scenarioCode));
    }

    private static void MapScenarios(RouteGroupBuilder group)
    {
        group.MapPost("/compile", (
            SimulationScenarioCompileApiRequest request,
            IEconomyScenarioTemplateCatalog catalog,
            IEconomyScenarioEventCompiler compiler) =>
        {
            if (!TryGetScenario(catalog, request.ScenarioCode, out ScenarioBusinessModelState? model))
            {
                return NotFound("scenario template", request.ScenarioCode);
            }

            var compileRequest = new EconomyScenarioEventCompileRequest(
                model,
                new ScenarioId(string.IsNullOrWhiteSpace(request.ScenarioId) ? model.ScenarioCode : request.ScenarioId),
                request.HorizonStartDate ?? model.PlanningStartDate,
                request.HorizonEndDate ?? model.PlanningEndDate);
            return Results.Ok(compiler.Compile(compileRequest));
        });

        group.MapPost("/run", async (ScenarioRunRequest request, IEconomyScenarioRunner runner, CancellationToken cancellationToken) =>
            Results.Ok(await runner.RunAsync(request, cancellationToken).ConfigureAwait(false)));
    }

    private static void MapRuns(RouteGroupBuilder group)
    {
        group.MapGet("", (
            string? projectId,
            ISimulationProjectWorkspaceService workspace,
            ISimulationRunLifecycleService lifecycle) =>
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                foreach (SimulationProjectSummary summary in workspace.ListProjects())
                {
                    SimulationProjectState? project = workspace.GetProject(summary.ProjectId);
                    if (project is not null)
                    {
                        lifecycle.EnsureProjectRuns(project);
                    }
                }
            }
            else if (workspace.GetProject(projectId) is { } project)
            {
                lifecycle.EnsureProjectRuns(project);
            }

            return Results.Ok(lifecycle.ListRuns(projectId));
        });

        group.MapGet("/{runId}", (string runId, ISimulationRunLifecycleService lifecycle) =>
            lifecycle.GetRun(runId) is { } run ? Results.Ok(run) : NotFound("run", runId));

        group.MapPost("/{runId}/commands", (
            string runId,
            SimulationRunCommandApiRequest request,
            ISimulationRunLifecycleService lifecycle) =>
        {
            SimulationRunCommandResult command = lifecycle.ApplyCommand(runId, request.Command);
            SimulationRunConsoleState? run = command.RunId is null ? null : lifecycle.GetRun(command.RunId);
            var response = new SimulationRunCommandApiResponse(command, run);
            return command.Succeeded ? Results.Ok(response) : Results.BadRequest(response);
        });

        group.MapPost("/{runId}/steps", (
            string runId,
            SimulationRunStepApiRequest request,
            ISimulationRunLifecycleService lifecycle) =>
        {
            SimulationRunCommandResult command = lifecycle.StepRun(runId, request.Step, request.TargetStep);
            SimulationRunConsoleState? run = command.RunId is null ? null : lifecycle.GetRun(command.RunId);
            var response = new SimulationRunStepApiResponse(command, run);
            return command.Succeeded ? Results.Ok(response) : Results.BadRequest(response);
        });

        group.MapPost("/{runId}/interventions", (
            string runId,
            SimulationRunInterventionRequest request,
            ISimulationRunLifecycleService lifecycle) =>
        {
            SimulationRunInterventionCommandResult result = lifecycle.ApplyIntervention(runId, request);
            SimulationRunConsoleState? run = result.Command.RunId is null ? null : lifecycle.GetRun(result.Command.RunId);
            var response = new SimulationRunInterventionApiResponse(result.Command, run, result.Intervention);
            return result.Command.Succeeded ? Results.Ok(response) : Results.BadRequest(response);
        });

        group.MapGet("/{runId}/observation", (string runId, ISimulationRunLifecycleService lifecycle) =>
            lifecycle.GetRun(runId) is { } run ? Results.Ok(run.Observation) : NotFound("run", runId));

        group.MapGet("/{runId}/analysis/finance", (
            string runId,
            ISimulationRunLifecycleService lifecycle,
            ISimulationFinanceAnalysisService finance) =>
            lifecycle.GetRun(runId) is { } run ? Results.Ok(finance.Build(run)) : NotFound("run", runId));

        group.MapGet("/{runId}/analysis/period-summary", (
            string runId,
            string? granularity,
            ISimulationRunLifecycleService lifecycle,
            ISimulationPeriodSummaryService summaries) =>
            lifecycle.GetRun(runId) is { } run
                ? Results.Ok(summaries.Build(run, ParseSummaryGranularity(granularity)))
                : NotFound("run", runId));

        group.MapGet("/{runId}/analysis/source-map", (string runId, ISimulationRunLifecycleService lifecycle) =>
            lifecycle.GetRun(runId) is { } run ? Results.Ok(run.OutputSources) : NotFound("run", runId));

        group.MapGet("/{runId}/graphs", (string runId, ISimulationRunLifecycleService lifecycle) =>
            lifecycle.GetRun(runId) is { } run ? Results.Ok(run.Observation.Graphs) : NotFound("run", runId));
    }

    private static void MapAnalysis(RouteGroupBuilder group)
    {
        group.MapPost("/projection", (ProjectionRequest request, IEconomyProjectionService projectionService) =>
            Results.Ok(projectionService.BuildProjection(request)));

        group.MapPost("/actuals/validate", (ActualEntryRequest request, IActualTrackingService actuals) =>
            Results.Ok(actuals.ValidateActualEntry(request)));

        group.MapPost("/calibration/recommend", (
            SimulationCalibrationRecommendationApiRequest request,
            IEconomyCalibrationService calibration) =>
            Results.Ok(calibration.RecommendAssumptionUpdate(
                request.ProjectId,
                request.MetricCode,
                request.PeriodStart,
                request.PeriodEnd,
                request.PlannedValue,
                request.ActualValue,
                request.EvidenceObjectIds)));
    }

    private static SimulationScenarioTemplateSummary ToTemplateSummary(ScenarioBusinessModelState model)
        => new(
            model.ProjectId,
            model.ScenarioCode,
            model.DisplayName,
            model.BaseCurrencyCode,
            model.PlanningStartDate,
            model.PlanningEndDate,
            model.OpeningCash.Value,
            model.MinimumCash.Value,
            model.BusinessUnits.Count,
            model.CustomerContracts.Count,
            model.ProductSkus.Count + model.Offerings.Count,
            model.WorkforcePlans.Count,
            model.CapexPlans.Count,
            model.CostPlans.Count);

    private static bool TryGetScenario(
        IEconomyScenarioTemplateCatalog catalog,
        string scenarioCode,
        out ScenarioBusinessModelState model)
    {
        try
        {
            model = catalog.GetScenario(scenarioCode);
            return true;
        }
        catch (InvalidOperationException)
        {
            model = default!;
            return false;
        }
    }

    private static SimulationPeriodSummaryGranularity ParseSummaryGranularity(string? granularity)
        => Enum.TryParse(granularity, ignoreCase: true, out SimulationPeriodSummaryGranularity parsed)
            ? parsed
            : SimulationPeriodSummaryGranularity.Quarter;

    private static IResult NotFound(string resource, string id)
        => Results.NotFound(new { message = $"Simulation {resource} '{id}' was not found." });
}
