using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Components;
using CanDoItAll.Economy.Simulator.Components.Models;
using CanDoItAll.Economy.Simulator.Components.Samples;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomySimulatorComponentTests : TestContext
{
    [Fact]
    public void EconomySimulatorShell_RendersProjectManagementSurfaces()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<EconomySimulatorShell> component = RenderComponent<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement()));
        string markup = component.Markup;

        Assert.Contains("Economy Simulator", markup, StringComparison.Ordinal);
        Assert.Contains("Simulation project management", markup, StringComparison.Ordinal);
        Assert.Contains("Project Alpha", markup, StringComparison.Ordinal);
        Assert.Contains("Project Beta", markup, StringComparison.Ordinal);
        Assert.Contains("Basic info", markup, StringComparison.Ordinal);
        Assert.Contains("BUs and accounts", markup, StringComparison.Ordinal);
        Assert.Contains("BOs and rules", markup, StringComparison.Ordinal);
        Assert.Contains("Financial inputs", markup, StringComparison.Ordinal);
        Assert.Contains("Planned expenses", markup, StringComparison.Ordinal);
        Assert.Contains("Simulation setup", markup, StringComparison.Ordinal);
        Assert.Contains("Export/import", markup, StringComparison.Ordinal);
        Assert.Contains("Project identity", markup, StringComparison.Ordinal);
        Assert.Contains("Project treasury", markup, StringComparison.Ordinal);
        Assert.Contains("Cashpool sweep rule", markup, StringComparison.Ordinal);
        Assert.Contains("Sales tax rate", markup, StringComparison.Ordinal);
        Assert.Contains("Baseline monthly run", markup, StringComparison.Ordinal);
        Assert.Contains("Export manifest", markup, StringComparison.Ordinal);
        Assert.Contains("reserve-threshold", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EconomySimulatorShell_RunSelectedInvokesRunRequest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationProjectRunRequest? captured = null;
        IRenderedComponent<EconomySimulatorShell> component = RenderComponent<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement())
            .Add(x => x.RunRequested, EventCallback.Factory.Create<SimulationProjectRunRequest>(this, request => captured = request)));

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-run-selected']").Click());
        Assert.Contains("Start simulation", component.Markup, StringComparison.Ordinal);
        await component.InvokeAsync(() => component.Find("[data-testid='simulation-start-automatic']").Click());
        await component.InvokeAsync(() => component.Find("[data-testid='simulation-start-confirm']").Click());

        Assert.NotNull(captured);
        Assert.Equal("project-alpha", captured!.Project.Summary.ProjectId);
        Assert.Equal("setup:baseline-monthly", captured.SetupId);
        Assert.Equal(SimulationRunMode.Automatic, captured.Mode);
    }

    [Fact]
    public async Task PlannedFinancialSchedulePanel_RendersVefApprovalTreeAndTimeline()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var factory = new SimulationScenarioProjectFactory(new EconomyScenarioTemplateCatalog(), new EconomySimulatorJsonSerializer());
        SimulationProjectState project = factory
            .CreateScenarioProjects(DateTimeOffset.Parse("2026-05-15T12:00:00Z"))
            .Single(project => project.Summary.ProjectId == "vef-financial-plan");

        IRenderedComponent<PlannedFinancialSchedulePanel> component = RenderComponent<PlannedFinancialSchedulePanel>(parameters => parameters
            .Add(x => x.Project, project));
        string markup = component.Markup;

        Assert.Contains("Planned schedule tree", markup, StringComparison.Ordinal);
        Assert.Contains("Approval timeline", markup, StringComparison.Ordinal);
        Assert.Contains("Selected planned expense", markup, StringComparison.Ordinal);
        Assert.Contains("BusinessUnitApproved", markup, StringComparison.Ordinal);
        Assert.Contains("Rent of offices", markup, StringComparison.Ordinal);
        Assert.Contains("simulation-planned-expense-timeline", markup, StringComparison.Ordinal);

        await component.InvokeAsync(() => component.Find("[data-testid^='simulation-planned-expense-row-']").Click());

        Assert.Contains("Scheduled amounts", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Plan source", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Period", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(">Cell<", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulationBoRuleDesigner_OpensTemplateCatalogueDialogLazily()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var factory = new SimulationScenarioProjectFactory(new EconomyScenarioTemplateCatalog(), new EconomySimulatorJsonSerializer());
        SimulationProjectState project = factory
            .CreateScenarioProjects(DateTimeOffset.Parse("2026-05-15T12:00:00Z"))
            .Single(project => project.Summary.ProjectId == "vef-financial-plan");

        IRenderedComponent<SimulationBoRuleDesigner> component = RenderComponent<SimulationBoRuleDesigner>(parameters => parameters
            .Add(x => x.Project, project));
        string markup = component.Markup;

        Assert.Contains("Rule process settings", markup, StringComparison.Ordinal);
        Assert.Contains("Payload preview", markup, StringComparison.Ordinal);
        Assert.Contains("simulation-template-catalogue-open", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Simulation Templates Catalogue", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Search tags, rules, BOs", markup, StringComparison.Ordinal);

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-template-catalogue-open']").Click());

        component.WaitForAssertion(() =>
        {
            Assert.Contains("Simulation Templates Catalogue", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Search tags, rules, BOs", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Dependencies", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Cashpool sweep", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Planned expense request", component.Markup, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task TimelineStepper_InvokesStepCallbacks()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var nextCount = 0;
        var previousCount = 0;
        var selectedStep = 0;
        IRenderedComponent<TimelineStepper> component = RenderComponent<TimelineStepper>(parameters => parameters
            .Add(x => x.CurrentStep, 2)
            .Add(x => x.TotalSteps, 8)
            .Add(x => x.AvailableStep, 3)
            .Add(x => x.Mode, TimelineStepperMode.Manual)
            .Add(x => x.CanStepBack, true)
            .Add(x => x.CanStepForward, true)
            .Add(x => x.PreviousClicked, EventCallback.Factory.Create(this, () => previousCount++))
            .Add(x => x.NextClicked, EventCallback.Factory.Create(this, () => nextCount++))
            .Add(x => x.StepSelected, EventCallback.Factory.Create<int>(this, step => selectedStep = step))
            .Add(x => x.Ticks, [
                new TimelineStepperTick { Step = 1, Label = "Start", Major = true },
                new TimelineStepperTick { Step = 4, Label = "4" },
                new TimelineStepperTick { Step = 8, Label = "End", Major = true, Disabled = true }
            ]));

        Assert.Contains("Step 2 of 8", component.Markup, StringComparison.Ordinal);

        await component.InvokeAsync(() => component.Find("[data-testid='timeline-stepper-next']").Click());
        await component.InvokeAsync(() => component.Find("[data-testid='timeline-stepper-previous']").Click());
        await component.InvokeAsync(() => component.Find(".cda-timeline-stepper__tick").Click());

        Assert.Equal(1, nextCount);
        Assert.Equal(1, previousCount);
        Assert.Equal(1, selectedStep);
    }

    [Fact]
    public void EconomySimulatorAppShell_RendersProjectsAndRunsNavigation()
    {
        Services.AddCanDoItAllBaseLib();
        Services.AddSingleton<IJSRuntime>(new SideMenuTestJsRuntime());

        IRenderedComponent<EconomySimulatorAppShell> component = RenderComponent<EconomySimulatorAppShell>(parameters => parameters
            .Add(x => x.ActivePage, "runs")
            .AddChildContent("<span>Runs content</span>"));

        Assert.Contains("CanDoItAll", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Projects", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Runs", component.Markup, StringComparison.Ordinal);
        Assert.Contains("simulation-app-shell", component.Markup, StringComparison.Ordinal);
        component.WaitForAssertion(() =>
        {
            var runsItem = component.Find("[data-testid='side-menu-economy-simulator-primary-item-runs']");
            Assert.True(runsItem.ClassList.Contains("is-selected"));
        });
    }

    [Fact]
    public void BusinessUnitTree_SelectsAccountBindingDetail()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<BusinessUnitTree> component = RenderComponent<BusinessUnitTree>(parameters => parameters
            .Add(x => x.Project, EconomySimulatorSampleData.CreateProjectState()));

        component.Find("[data-testid='simulation-binding-node-Revenue']").Click();

        Assert.Contains("Account binding", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Sales revenue", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Revenue", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulationRunObservationPage_RendersObservationTabsAndEvidence()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<SimulationRunObservationPage> component = RenderComponent<SimulationRunObservationPage>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateRunObservation()));
        string markup = component.Markup;

        Assert.Contains("Simulation observation", markup, StringComparison.Ordinal);
        Assert.Contains("Run Observation", markup, StringComparison.Ordinal);
        Assert.Contains("State", markup, StringComparison.Ordinal);
        Assert.Contains("Summary", markup, StringComparison.Ordinal);
        Assert.Contains("Graphs", markup, StringComparison.Ordinal);
        Assert.Contains("BUs", markup, StringComparison.Ordinal);
        Assert.Contains("BO Evidence", markup, StringComparison.Ordinal);
        Assert.Contains("Issues", markup, StringComparison.Ordinal);
        Assert.Contains("Artifacts", markup, StringComparison.Ordinal);
        Assert.Contains("Recent events", markup, StringComparison.Ordinal);
        Assert.Contains("Resource usage", markup, StringComparison.Ordinal);
        Assert.Contains("Simulation pipeline", markup, StringComparison.Ordinal);
        Assert.Contains("simulation-run-timeline-stepper", markup, StringComparison.Ordinal);
        Assert.Contains("Management summary", markup, StringComparison.Ordinal);
        Assert.Contains("Income distribution", markup, StringComparison.Ordinal);
        Assert.Contains("Expense distribution", markup, StringComparison.Ordinal);
        Assert.Contains("Data available", markup, StringComparison.Ordinal);
        Assert.Contains("Output source map", markup, StringComparison.Ordinal);
        Assert.Contains("Rule execution evidence", markup, StringComparison.Ordinal);
        Assert.Contains("alpha.cashpool-sweep.v1", markup, StringComparison.Ordinal);
        Assert.Contains("cash-receipts", markup, StringComparison.Ordinal);
        Assert.Contains("BO-FX-02", markup, StringComparison.Ordinal);
        Assert.Contains("Generated run outputs", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulationPeriodSummaryPanel_TogglesQuarterAndYearViews()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<SimulationPeriodSummaryPanel> component = RenderComponent<SimulationPeriodSummaryPanel>(parameters => parameters
            .Add(x => x.Run, EconomySimulatorSampleData.CreateRunConsole().SelectedRun));

        Assert.Contains("Quarterly view", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Q1", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Income distribution", component.Markup, StringComparison.Ordinal);

        IRenderedComponent<Button> yearButton = component.FindComponents<Button>().Single(button => string.Equals(button.Instance.Text, "Year", StringComparison.Ordinal));
        await component.InvokeAsync(() => yearButton.Instance.Click.InvokeAsync());

        Assert.Contains("Yearly view", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulationRunObservationPage_TimelineRequestsStepChanges()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationRunStepRequest? captured = null;
        IRenderedComponent<SimulationRunObservationPage> component = RenderComponent<SimulationRunObservationPage>(parameters => parameters
            .Add(x => x.ConsoleViewModel, EconomySimulatorSampleData.CreateRunConsole())
            .Add(x => x.StepRequested, EventCallback.Factory.Create<SimulationRunStepRequest>(this, request => captured = request)));

        await component.InvokeAsync(() => component.Find("[data-testid='timeline-stepper-next']").Click());

        Assert.NotNull(captured);
        Assert.Equal(SimulationRunStepRequestKind.Next, captured!.Step);
    }

    [Fact]
    public async Task SimulationRunObservationPage_ManualInterventionPanelEmitsRequest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationRunConsoleViewModel console = EconomySimulatorSampleData.CreateRunConsole();
        SimulationRunConsoleState manualRun = console.SelectedRun with
        {
            Run = console.SelectedRun.Run with
            {
                Status = SimulationRunLifecycleStatus.Paused,
                RunMode = SimulationRunMode.Manual,
                SelectedStep = 1,
                AvailableStep = 1
            }
        };
        console = console with { Runs = [manualRun], SelectedRun = manualRun };
        SimulationRunInterventionRequest? captured = null;
        IRenderedComponent<SimulationRunObservationPage> component = RenderComponent<SimulationRunObservationPage>(parameters => parameters
            .Add(x => x.ConsoleViewModel, console)
            .Add(x => x.InterventionRequested, EventCallback.Factory.Create<SimulationRunInterventionRequest>(this, request => captured = request)));

        Assert.Contains("Manual interventions", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Demand", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Production line", component.Markup, StringComparison.Ordinal);

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-apply-intervention']").Click());

        Assert.NotNull(captured);
        Assert.Equal(SimulationRunInterventionKind.DemandChange, captured!.Kind);
        Assert.False(string.IsNullOrWhiteSpace(captured.TargetId));
        Assert.Equal(1.25m, captured.Multiplier);
    }

    [Fact]
    public async Task EconomyScenarioGraphPanel_SwitchesMetricViews()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var model = EconomySimulatorSampleData.Create();
        IRenderedComponent<EconomyScenarioGraphPanel> component = RenderComponent<EconomyScenarioGraphPanel>(parameters => parameters
            .Add(x => x.Graphs, model.Graphs)
            .Add(x => x.BusinessUnits, model.BusinessUnits)
            .Add(x => x.AddressBindings, model.AddressBindings));

        IRenderedComponent<Button> revenueButton = component.FindComponents<Button>().Single(button => string.Equals(button.Instance.Text, "Revenue", StringComparison.Ordinal));
        await component.InvokeAsync(() => revenueButton.Instance.Click.InvokeAsync());

        Assert.Contains("Revenue by category", component.Markup, StringComparison.Ordinal);
        Assert.Contains("revenue-subscription", component.Markup, StringComparison.Ordinal);
        Assert.Contains("EUR 116,000", component.Markup, StringComparison.Ordinal);
    }

    private sealed class SideMenuTestJsRuntime : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (string.Equals(identifier, "import", StringComparison.Ordinal))
            {
                return ValueTask.FromResult((TValue)(object)(IJSObjectReference)this);
            }

            if (string.Equals(identifier, "initialize", StringComparison.Ordinal))
            {
                object initialization = Activator.CreateInstance(typeof(TValue), nonPublic: true)
                    ?? throw new InvalidOperationException($"Cannot create {typeof(TValue).FullName}.");
                typeof(TValue).GetProperty("Metrics")?.SetValue(initialization, new SideMenuLayoutMetrics());
                return ValueTask.FromResult((TValue)initialization);
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
