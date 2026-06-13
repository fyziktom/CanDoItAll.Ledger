using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Economy.Simulator.Persistence.EFCore;

public sealed class EconomySimulatorProjectionDbContext(DbContextOptions<EconomySimulatorProjectionDbContext> options) : DbContext(options)
{
    public DbSet<EconomySimulatorProjectionRecord> Projections => Set<EconomySimulatorProjectionRecord>();

    public DbSet<EconomySimulatorScenarioRunRecord> ScenarioRuns => Set<EconomySimulatorScenarioRunRecord>();

    public DbSet<EconomySimulatorGraphPointRecord> GraphPoints => Set<EconomySimulatorGraphPointRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EconomySimulatorProjectionRecord>(entity =>
        {
            entity.ToTable("economy_simulator_projections");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjectId, x.ScenarioId, x.Kind, x.AsOfDate });
            entity.Property(x => x.ProjectId).HasMaxLength(128);
            entity.Property(x => x.ScenarioId).HasMaxLength(128);
            entity.Property(x => x.Kind).HasMaxLength(64);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<EconomySimulatorScenarioRunRecord>(entity =>
        {
            entity.ToTable("economy_simulator_scenario_runs");
            entity.HasKey(x => x.RunId);
            entity.HasIndex(x => new { x.ProjectId, x.ScenarioId, x.StartedAtUtc });
            entity.Property(x => x.RunId).HasMaxLength(192);
            entity.Property(x => x.ProjectId).HasMaxLength(128);
            entity.Property(x => x.ScenarioId).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.ResultJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<EconomySimulatorGraphPointRecord>(entity =>
        {
            entity.ToTable("economy_simulator_graph_points");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProjectId, x.ScenarioId, x.PeriodStart, x.FinancialStatementSection });
            entity.HasIndex(x => new { x.ProjectId, x.BusinessUnitId, x.AccountAddress, x.PeriodStart });
            entity.HasIndex(x => new { x.ProjectId, x.CategoryCode, x.EventStatus, x.ImpactKind, x.PeriodStart });
            entity.Property(x => x.ProjectId).HasMaxLength(128);
            entity.Property(x => x.ScenarioId).HasMaxLength(128);
            entity.Property(x => x.RunId).HasMaxLength(192);
            entity.Property(x => x.BusinessUnitId).HasMaxLength(192);
            entity.Property(x => x.AccountAddress).HasMaxLength(128);
            entity.Property(x => x.CounterpartyId).HasMaxLength(128);
            entity.Property(x => x.CustomerContractId).HasMaxLength(128);
            entity.Property(x => x.ProductOrServiceId).HasMaxLength(128);
            entity.Property(x => x.CategoryCode).HasMaxLength(128);
            entity.Property(x => x.EventStatus).HasMaxLength(64);
            entity.Property(x => x.ImpactKind).HasMaxLength(64);
            entity.Property(x => x.EventKind).HasMaxLength(64);
            entity.Property(x => x.FinancialStatementSection).HasMaxLength(64);
            entity.Property(x => x.Interval).HasMaxLength(32);
            entity.Property(x => x.SourceEventId).HasMaxLength(192);
            entity.Property(x => x.SourceObjectId).HasMaxLength(192);
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });
    }
}

public sealed class EconomySimulatorProjectionRecord
{
    public long Id { get; set; }

    [MaxLength(128)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ScenarioId { get; set; }

    [MaxLength(64)]
    public string Kind { get; set; } = string.Empty;

    public DateOnly AsOfDate { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class EconomySimulatorScenarioRunRecord
{
    [MaxLength(192)]
    public string RunId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string ScenarioId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = string.Empty;

    public string ResultJson { get; set; } = "{}";
}

public sealed class EconomySimulatorGraphPointRecord
{
    public long Id { get; set; }

    [MaxLength(128)]
    public string ProjectId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ScenarioId { get; set; }

    [MaxLength(192)]
    public string? RunId { get; set; }

    [MaxLength(192)]
    public string BusinessUnitId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? AccountAddress { get; set; }

    [MaxLength(128)]
    public string? CounterpartyId { get; set; }

    [MaxLength(128)]
    public string? CustomerContractId { get; set; }

    [MaxLength(128)]
    public string? ProductOrServiceId { get; set; }

    [MaxLength(128)]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EventStatus { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ImpactKind { get; set; } = string.Empty;

    [MaxLength(64)]
    public string EventKind { get; set; } = string.Empty;

    [MaxLength(64)]
    public string FinancialStatementSection { get; set; } = string.Empty;

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    [MaxLength(32)]
    public string Interval { get; set; } = "monthly";

    public decimal PlannedInflow { get; set; }

    public decimal PlannedOutflow { get; set; }

    public decimal CommittedInflow { get; set; }

    public decimal CommittedOutflow { get; set; }

    public decimal ActualInflow { get; set; }

    public decimal ActualOutflow { get; set; }

    public decimal SimulatedInflow { get; set; }

    public decimal SimulatedOutflow { get; set; }

    public decimal NonCashIncome { get; set; }

    public decimal NonCashExpense { get; set; }

    public decimal BalanceAfter { get; set; }

    [MaxLength(192)]
    public string SourceEventId { get; set; } = string.Empty;

    [MaxLength(192)]
    public string? SourceObjectId { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
