using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Simulator;
using CanDoItAll.Economy.Simulator.Components.Components;
using CanDoItAll.Economy.Simulator.Components.Models;
using CanDoItAll.Economy.Simulator.Components.Samples;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class EconomySimulatorComponentTests : BunitContext
{
    public EconomySimulatorComponentTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void SimulationRunRegistryRevision_ChangesOnlyWhenObservableRunStateChanges()
    {
        SimulationRunRegistryRecord baseline = EconomySimulatorSampleData.CreateRunConsole().SelectedRun.Run;
        IReadOnlyList<SimulationRunRegistryRevision> observed = SimulationRunRegistryRevision.Capture([baseline]);

        Assert.True(observed.SequenceEqual(SimulationRunRegistryRevision.Capture([baseline with { }] )));

        SimulationRunRegistryRecord sameStepIntervention = baseline with
        {
            OutputHash = $"{baseline.OutputHash}-intervention"
        };
        Assert.Equal(baseline.CurrentStep, sameStepIntervention.CurrentStep);
        Assert.Equal(baseline.SelectedStep, sameStepIntervention.SelectedStep);
        Assert.False(observed.SequenceEqual(SimulationRunRegistryRevision.Capture([sameStepIntervention])));
    }

    [Fact]
    public async Task EconomySimulatorShell_RendersOnlyTheActiveProjectTabAndNavigatesToOtherContent()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement()));
        string markup = component.Markup;

        async Task ClickTabButtonAsync(int tabIndex)
        {
            var tabButtons = component.FindAll("[data-testid='simulation-project-tabs'] button[role='tab']");
            await component.InvokeAsync(() => tabButtons[tabIndex].Click());
            var updatedTabButtons = component.FindAll("[data-testid='simulation-project-tabs'] button[role='tab']");
            Assert.Equal("true", updatedTabButtons[tabIndex].GetAttribute("aria-selected"));
            Assert.Single(updatedTabButtons, button => button.GetAttribute("aria-selected") == "true");
        }

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
        Assert.DoesNotContain("Project treasury", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Cashpool sweep rule", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Sales tax rate", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Planned schedule tree", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Baseline monthly run", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Export manifest", markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(1);
        Assert.Contains("Project treasury", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Project identity", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(2);
        Assert.Contains("Cashpool sweep rule", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Project treasury", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(3);
        Assert.Contains("Sales tax rate", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Cashpool sweep rule", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(4);
        Assert.Contains("Planned schedule tree", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Sales tax rate", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(5);
        Assert.Contains("Baseline monthly run", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Planned schedule tree", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(6);
        Assert.Contains("Export manifest", component.Markup, StringComparison.Ordinal);
        Assert.Contains("reserve-threshold", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"simulation-setup-tab\"", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void EconomySimulatorShell_RendersProjectsAsACompactGroupedTree()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationProjectManagementViewModel sample = EconomySimulatorSampleData.CreateProjectManagement();
        var factory = new SimulationScenarioProjectFactory(new EconomyScenarioTemplateCatalog(), new EconomySimulatorJsonSerializer());
        IReadOnlyList<SimulationProjectState> projectStates = sample.ProjectStates
            .Concat(factory.CreateScenarioProjects(DateTimeOffset.Parse("2026-05-15T12:00:00Z")))
            .ToArray();
        SimulationProjectManagementViewModel model = sample with
        {
            Projects = projectStates.Select(project => project.Summary).ToArray(),
            ProjectStates = projectStates
        };

        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, model));

        var tree = component.Find("[data-testid='simulation-project-tree']");
        Assert.Equal("tree", tree.GetAttribute("role"));
        Assert.Equal("Simulation projects grouped by project family", tree.GetAttribute("aria-label"));
        Assert.Contains("Workspace projects", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Company scenarios", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Reference plans", component.Markup, StringComparison.Ordinal);
        Assert.Equal(projectStates.Count, component.FindAll("[data-testid^='simulation-project-row-']").Count);
        Assert.Single(component.FindAll("[role='treeitem'][aria-selected='true']"));

        component.Find("[data-testid='simulation-project-group-company-scenarios']").Click();

        Assert.Equal("false", component.Find("[data-testid='simulation-project-group-company-scenarios']").GetAttribute("aria-expanded"));
        Assert.Empty(component.FindAll("[data-testid='simulation-project-group-company-scenarios-children']"));
        Assert.Single(component.FindAll("[role='treeitem'][aria-selected='true']"));
    }

    [Fact]
    public void EconomySimulatorShell_ProjectTreeSupportsTheBaseLibKeyboardModel()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement()));

        var tree = component.Find("[data-testid='simulation-project-tree']");
        var group = component.Find("[data-testid='simulation-project-group-workspace-projects']");
        var firstProject = component.Find("[data-testid='simulation-project-row-project-alpha']");

        Assert.Equal("0", tree.GetAttribute("tabindex"));
        Assert.All(component.FindAll("[role='treeitem']"), item => Assert.Equal("-1", item.GetAttribute("tabindex")));

        tree.KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal(group.GetAttribute("id"), tree.GetAttribute("aria-activedescendant"));

        tree.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal("false", group.GetAttribute("aria-expanded"));

        tree.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", group.GetAttribute("aria-expanded"));

        tree.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(firstProject.GetAttribute("id"), tree.GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public void EconomySimulatorShell_ProjectTreeExposesMultipleScenariosUnderTheirProject()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationProjectManagementViewModel sample = EconomySimulatorSampleData.CreateProjectManagement();
        SimulationProjectState project = sample.ProjectStates.Single(candidate => candidate.Summary.ProjectId == "project-alpha");
        SimulationRunSetupState downside = project.RunSetups[0] with
        {
            SetupId = "setup:downside-quarterly",
            ScenarioId = new ScenarioId("downside-quarterly"),
            ScenarioName = "Downside quarterly run",
            StepPeriod = SimulationRunStepPeriod.Quarter,
            StepCount = 4,
            Seed = 84
        };
        SimulationProjectState projectWithScenarios = project with { RunSetups = [project.RunSetups[0], downside] };
        SimulationProjectManagementViewModel model = sample with
        {
            SelectedProject = projectWithScenarios,
            ProjectStates = sample.ProjectStates
                .Select(candidate => candidate.Summary.ProjectId == project.Summary.ProjectId ? projectWithScenarios : candidate)
                .ToArray()
        };

        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, model));

        Assert.Equal("true", component.Find("[data-testid='simulation-project-row-project-alpha']").GetAttribute("aria-expanded"));
        Assert.Equal(2, component.FindAll("[data-testid^='simulation-project-setup-project-alpha-']").Count);
        Assert.Equal(
            "true",
            component.Find("[data-testid='simulation-project-setup-project-alpha-setup-baseline-monthly']").GetAttribute("aria-selected"));

        component.Find("[data-testid='simulation-project-setup-project-alpha-setup-downside-quarterly']").Click();

        Assert.Equal(
            "true",
            component.Find("[data-testid='simulation-project-setup-project-alpha-setup-downside-quarterly']").GetAttribute("aria-selected"));
        Assert.Equal(
            "false",
            component.Find("[data-testid='simulation-project-setup-project-alpha-setup-baseline-monthly']").GetAttribute("aria-selected"));

        component.Find("[data-testid='simulation-project-toggle-project-alpha']").Click();
        Assert.Equal("false", component.Find("[data-testid='simulation-project-row-project-alpha']").GetAttribute("aria-expanded"));
        Assert.Empty(component.FindAll("[data-testid='simulation-project-scenarios-project-alpha']"));
    }

    [Fact]
    public void EconomySimulatorShell_SelectsAndFiltersProjectsInsideTheirGroup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement()));

        component.Find("[data-testid='simulation-project-row-project-beta']").Click();

        Assert.Equal("true", component.Find("[data-testid='simulation-project-row-project-beta']").GetAttribute("aria-selected"));
        Assert.Equal("false", component.Find("[data-testid='simulation-project-row-project-alpha']").GetAttribute("aria-selected"));
        Assert.Equal("Project Beta", component.Find("[data-testid='simulation-selected-project-name']").TextContent.Trim());

        component.Find("input[placeholder='Search project, scenario, or currency...']").Input("Beta");

        Assert.Single(component.FindAll("[data-testid^='simulation-project-row-']"));
        Assert.Contains("Workspace projects", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Project Beta", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Gamma", component.Markup, StringComparison.Ordinal);

        component.Find("input[placeholder='Search project, scenario, or currency...']").Input("no matching project");
        Assert.Contains("No projects match the current search.", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void EconomySimulatorShell_ShowsCompleteProjectDetailsThroughTheRightTooltipService()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement()));
        IRenderedComponent<Tooltip> tooltipHost = Render<Tooltip>();
        IRenderedComponent<TooltipTarget> tooltipTarget = Assert.Single(
            component.FindComponents<TooltipTarget>(),
            candidate => candidate.Instance.TestId == "simulation-project-tooltip-project-beta");

        Assert.Equal(TooltipPosition.Right, tooltipTarget.Instance.Position);
        var trigger = tooltipTarget.Find("span.cda-sim-project-tree__tooltip-target");
        trigger.MouseEnter(new MouseEventArgs { ClientX = 280, ClientY = 240 });

        TooltipService service = Services.GetRequiredService<TooltipService>();
        tooltipHost.WaitForAssertion(() =>
        {
            Assert.NotNull(service.Current);
            Assert.Equal(TooltipPosition.Right, service.Current!.Options.Position);
            Assert.Equal("simulation-project-tooltip-project-beta", service.Current.Options.TestId);
            Assert.Contains("Project Beta", tooltipHost.Markup, StringComparison.Ordinal);
            Assert.Contains("NeedsAttention", tooltipHost.Markup, StringComparison.Ordinal);
            Assert.Contains("2026-04-01", tooltipHost.Markup, StringComparison.Ordinal);
            Assert.Contains("5", tooltipHost.Markup, StringComparison.Ordinal);
            Assert.Contains("InMemory", tooltipHost.Markup, StringComparison.Ordinal);
        }, TimeSpan.FromSeconds(2));

        trigger.MouseLeave();
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task EconomySimulatorShell_RunSelectedInvokesRunRequest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        SimulationProjectRunRequest? captured = null;
        IRenderedComponent<EconomySimulatorShell> component = Render<EconomySimulatorShell>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateProjectManagement())
            .Add(x => x.RunRequested, EventCallback.Factory.Create<SimulationProjectRunRequest>(this, request => captured = request)));

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-run-selected']").Click());
        Assert.Contains("Start simulation", component.Markup, StringComparison.Ordinal);
        Assert.Equal("false", component.Find("[data-testid='simulation-start-automatic']").GetAttribute("aria-pressed"));
        Assert.Equal("true", component.Find("[data-testid='simulation-start-manual']").GetAttribute("aria-pressed"));
        await component.InvokeAsync(() => component.Find("[data-testid='simulation-start-automatic']").Click());
        Assert.Equal("true", component.Find("[data-testid='simulation-start-automatic']").GetAttribute("aria-pressed"));
        Assert.Equal("false", component.Find("[data-testid='simulation-start-manual']").GetAttribute("aria-pressed"));
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

        IRenderedComponent<PlannedFinancialSchedulePanel> component = Render<PlannedFinancialSchedulePanel>(parameters => parameters
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

        IRenderedComponent<SimulationBoRuleDesigner> component = Render<SimulationBoRuleDesigner>(parameters => parameters
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
        IRenderedComponent<TimelineStepper> component = Render<TimelineStepper>(parameters => parameters
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
        Services.AddSingleton<IJSRuntime>(new SideMenuTestJsRuntime());

        IRenderedComponent<EconomySimulatorAppShell> component = Render<EconomySimulatorAppShell>(parameters => parameters
            .Add(x => x.ActivePage, "runs")
            .AddChildContent("<span>Runs content</span>"));

        Assert.Contains("CanDoItAll", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Projects", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Runs", component.Markup, StringComparison.Ordinal);
        Assert.Contains("simulation-app-shell", component.Markup, StringComparison.Ordinal);
        Layout layout = component.FindComponent<Layout>().Instance;
        Body body = component.FindComponent<Body>().Instance;
        Assert.True(layout.FitViewport);
        Assert.True(layout.LockOverflow);
        Assert.True(body.FillHeight);
        Assert.True(body.Scrollable);
        Assert.Single(component.FindAll("[data-testid='simulation-app-scroll-owner']"));
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
        IRenderedComponent<BusinessUnitTree> component = Render<BusinessUnitTree>(parameters => parameters
            .Add(x => x.Project, EconomySimulatorSampleData.CreateProjectState()));

        component.Find("[data-testid='simulation-binding-node-Revenue']").Click();

        Assert.Contains("Account binding", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Sales revenue", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Revenue", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulationRunObservationPage_RendersOnlyTheActiveObservationTabAndNavigatesToOtherContent()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<SimulationRunObservationPage> component = Render<SimulationRunObservationPage>(parameters => parameters
            .Add(x => x.ViewModel, EconomySimulatorSampleData.CreateRunObservation()));
        string markup = component.Markup;

        async Task ClickTabButtonAsync(int tabIndex)
        {
            var tabButtons = component.FindAll("[data-testid='simulation-run-observation-tabs'] button[role='tab']");
            await component.InvokeAsync(() => tabButtons[tabIndex].Click());
            var updatedTabButtons = component.FindAll("[data-testid='simulation-run-observation-tabs'] button[role='tab']");
            Assert.Equal("true", updatedTabButtons[tabIndex].GetAttribute("aria-selected"));
            Assert.Single(updatedTabButtons, button => button.GetAttribute("aria-selected") == "true");
        }

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
        Assert.Contains("Data available", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Management summary", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Output source map", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated run outputs", markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(1);
        Assert.Contains("Management summary", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Income distribution", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Expense distribution", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Recent events", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(2);
        Assert.Contains("simulation-run-graphs-tab", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Management summary", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(3);
        Assert.Contains("simulation-run-bu-tab", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("simulation-run-graphs-tab", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(4);
        Assert.Contains("Output source map", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Rule execution evidence", component.Markup, StringComparison.Ordinal);
        Assert.Contains("alpha.cashpool-sweep.v1", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("simulation-run-bu-tab", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(5);
        Assert.Contains("BO-FX-02", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Output source map", component.Markup, StringComparison.Ordinal);

        await ClickTabButtonAsync(6);
        Assert.Contains("Generated run outputs", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("BO-FX-02", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulationPeriodSummaryPanel_TogglesQuarterAndYearViews()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        IRenderedComponent<SimulationPeriodSummaryPanel> component = Render<SimulationPeriodSummaryPanel>(parameters => parameters
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
        IRenderedComponent<SimulationRunObservationPage> component = Render<SimulationRunObservationPage>(parameters => parameters
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
        IRenderedComponent<SimulationRunObservationPage> component = Render<SimulationRunObservationPage>(parameters => parameters
            .Add(x => x.ConsoleViewModel, console)
            .Add(x => x.InterventionRequested, EventCallback.Factory.Create<SimulationRunInterventionRequest>(this, request => captured = request)));

        Assert.Contains("Manual interventions", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Demand", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Production line", component.Markup, StringComparison.Ordinal);
        Assert.Equal("true", component.Find("[data-testid='simulation-intervention-demand']").GetAttribute("aria-pressed"));
        Assert.Equal("false", component.Find("[data-testid='simulation-intervention-capacity']").GetAttribute("aria-pressed"));

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-intervention-capacity']").Click());
        Assert.Equal("false", component.Find("[data-testid='simulation-intervention-demand']").GetAttribute("aria-pressed"));
        Assert.Equal("true", component.Find("[data-testid='simulation-intervention-capacity']").GetAttribute("aria-pressed"));

        await component.InvokeAsync(() => component.Find("[data-testid='simulation-intervention-demand']").Click());

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
        IRenderedComponent<EconomyScenarioGraphPanel> component = Render<EconomyScenarioGraphPanel>(parameters => parameters
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
