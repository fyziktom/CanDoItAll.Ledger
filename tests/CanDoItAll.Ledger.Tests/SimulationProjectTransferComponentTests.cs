using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.Simulator;
using CanDoItAll.Ledger.Simulator.Components.Components;
using CanDoItAll.Ledger.Simulator.Components.Models;
using CanDoItAll.Ledger.Simulator.Components.Samples;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class SimulationProjectTransferComponentTests : BunitContext
{
    private readonly SimulationProjectImportExportService importExport = new(
        new LedgerSimulatorJsonSerializer(),
        new SimulationProjectValidationService());

    public SimulationProjectTransferComponentTests()
    {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ExportManifest_AlwaysRepresentsTheCurrentlySelectedProject()
    {
        SimulationProjectManagementViewModel management = LedgerSimulatorSampleData.CreateProjectManagement();
        SimulationProjectState staleProject = management.ProjectStates.Single(project => project.Summary.ProjectId == "project-alpha");
        SimulationProjectState selectedProject = management.ProjectStates.Single(project => project.Summary.ProjectId == "project-beta");
        SimulationProjectExportManifest staleManifest = importExport.CreateManifest(
            staleProject,
            DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
        SimulationProjectImportPreview stalePreview = importExport.PreviewImport(importExport.SerializeManifest(staleManifest));

        IRenderedComponent<SimulationProjectImportExportPanel> component = Render<SimulationProjectImportExportPanel>(parameters => parameters
            .Add(x => x.Project, selectedProject)
            .Add(x => x.ImportExportService, importExport)
            .Add(x => x.Manifest, staleManifest)
            .Add(x => x.ImportPreview, stalePreview));

        var download = component.Find("[data-testid='simulation-download-manifest']");
        string href = Assert.IsType<string>(download.GetAttribute("href"));
        int payloadStart = href.IndexOf(',', StringComparison.Ordinal);
        Assert.True(payloadStart >= 0, "The manifest download must use a data URI with a JSON payload.");
        string exportedJson = Uri.UnescapeDataString(href[(payloadStart + 1)..]);
        SimulationProjectImportPreview exportedPreview = importExport.PreviewImport(exportedJson);

        Assert.Equal("project-beta-manifest.json", download.GetAttribute("download"));
        Assert.NotNull(exportedPreview.Manifest);
        Assert.Equal("project-beta", exportedPreview.Manifest!.ProjectId);
        Assert.Equal(selectedProject.Summary.ProjectId, exportedPreview.Manifest.Project.Summary.ProjectId);
        Assert.DoesNotContain("project-alpha-manifest.json", component.Markup, StringComparison.Ordinal);
        Assert.All(
            component.FindAll("details span[aria-describedby]"),
            trigger => Assert.Equal("0", trigger.GetAttribute("tabindex")));

        SimulationProjectState revisedProject = selectedProject with
        {
            RunSetups =
            [
                selectedProject.RunSetups[0] with { Seed = selectedProject.RunSetups[0].Seed + 1 }
            ]
        };
        SimulationProjectExportManifest staleSameProjectManifest = importExport.CreateManifest(
            selectedProject,
            DateTimeOffset.Parse("2026-05-15T12:00:00Z"));
        SimulationProjectImportPreview staleSameProjectPreview = importExport.PreviewImport(importExport.SerializeManifest(staleSameProjectManifest));

        component.Render(parameters => parameters
            .Add(x => x.Project, revisedProject)
            .Add(x => x.ImportExportService, importExport)
            .Add(x => x.Manifest, staleSameProjectManifest)
            .Add(x => x.ImportPreview, staleSameProjectPreview));

        download = component.Find("[data-testid='simulation-download-manifest']");
        href = Assert.IsType<string>(download.GetAttribute("href"));
        payloadStart = href.IndexOf(',', StringComparison.Ordinal);
        exportedJson = Uri.UnescapeDataString(href[(payloadStart + 1)..]);
        exportedPreview = importExport.PreviewImport(exportedJson);
        SimulationProjectExportManifest expectedRevision = importExport.CreateManifest(revisedProject, staleSameProjectManifest.ExportedAtUtc);

        Assert.NotNull(exportedPreview.Manifest);
        Assert.Equal(expectedRevision.ContentHashes["runSetups"], exportedPreview.Manifest!.ContentHashes["runSetups"]);
        Assert.NotEqual(staleSameProjectManifest.ContentHashes["runSetups"], exportedPreview.Manifest.ContentHashes["runSetups"]);
    }

    [Fact]
    public void InvalidPastedJson_BlocksAcceptAndReportsTheValidationFailure()
    {
        SimulationProjectState project = LedgerSimulatorSampleData.CreateProjectState();
        SimulationProjectExportManifest manifest = importExport.CreateManifest(project, project.Summary.UpdatedAtUtc);
        SimulationProjectImportPreview preview = importExport.PreviewImport(importExport.SerializeManifest(manifest));
        SimulationProjectState? importedProject = null;
        IRenderedComponent<SimulationProjectImportExportPanel> component = Render<SimulationProjectImportExportPanel>(parameters => parameters
            .Add(x => x.Project, project)
            .Add(x => x.ImportExportService, importExport)
            .Add(x => x.Manifest, manifest)
            .Add(x => x.ImportPreview, preview)
            .Add(x => x.ProjectImported, EventCallback.Factory.Create<SimulationProjectState>(this, imported => importedProject = imported)));

        component.Find("textarea[aria-label='Incoming project manifest JSON']").Input("{ this is not valid JSON }");
        component.Find("[data-testid='simulation-validate-import']").Click();

        var accept = component.Find("[data-testid='simulation-accept-import']");
        Assert.True(accept.HasAttribute("disabled"));
        Assert.Contains("Import blocked", component.Markup, StringComparison.Ordinal);
        Assert.Contains("import-json-invalid", component.Markup, StringComparison.Ordinal);
        Assert.Null(importedProject);
    }

    [Fact]
    public async Task ValidPastedJson_EnablesAcceptAndEmitsTheImportedProject()
    {
        SimulationProjectState project = LedgerSimulatorSampleData.CreateProjectState();
        SimulationProjectExportManifest manifest = importExport.CreateManifest(project, project.Summary.UpdatedAtUtc);
        SimulationProjectImportPreview preview = importExport.PreviewImport(importExport.SerializeManifest(manifest));
        SimulationProjectState? importedProject = null;
        IRenderedComponent<SimulationProjectImportExportPanel> component = Render<SimulationProjectImportExportPanel>(parameters => parameters
            .Add(x => x.Project, project)
            .Add(x => x.ImportExportService, importExport)
            .Add(x => x.Manifest, manifest)
            .Add(x => x.ImportPreview, preview)
            .Add(x => x.ProjectImported, EventCallback.Factory.Create<SimulationProjectState>(this, imported => importedProject = imported)));

        component.Find("textarea[aria-label='Incoming project manifest JSON']").Input(importExport.SerializeManifest(manifest));
        component.Find("[data-testid='simulation-validate-import']").Click();

        component.WaitForAssertion(() => Assert.False(component.Find("[data-testid='simulation-accept-import']").HasAttribute("disabled")));
        await component.InvokeAsync(() => component.Find("[data-testid='simulation-accept-import']").Click());

        Assert.NotNull(importedProject);
        Assert.Equal(project.Summary.ProjectId, importedProject!.Summary.ProjectId);
        Assert.Contains("Accepted", component.Markup, StringComparison.Ordinal);
        Assert.True(component.Find("[data-testid='simulation-accept-import']").HasAttribute("disabled"));
    }
}
