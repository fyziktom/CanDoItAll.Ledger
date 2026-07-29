using CanDoItAll.Ledger.Persistence.EFCore.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Ledger.Persistence.EFCore.Configurations;

public sealed class LedgerStateRecordConfiguration : IEntityTypeConfiguration<LedgerStateRecord>
{
    public void Configure(EntityTypeBuilder<LedgerStateRecord> builder)
    {
        builder.ToTable("Economy_LedgerStates");
        builder.HasKey(x => x.StateKey);
        builder.Property(x => x.StateKey).HasMaxLength(512);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.CurrentLedgerHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId }).IsUnique();
    }
}

public sealed class LedgerTransactionRecordConfiguration : IEntityTypeConfiguration<LedgerTransactionRecord>
{
    public void Configure(EntityTypeBuilder<LedgerTransactionRecord> builder)
    {
        builder.ToTable("Economy_LedgerTransactions");
        builder.HasKey(x => x.TxId);
        builder.Property(x => x.TxId).HasMaxLength(128);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.PreviousLedgerHash).HasMaxLength(128);
        builder.Property(x => x.LedgerEntryHash).HasMaxLength(128);
        builder.Property(x => x.TxHash).HasMaxLength(128);
        builder.Property(x => x.DataHash).HasMaxLength(128);
        builder.Property(x => x.TokenDefinitionJson).HasColumnType("text");
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.LedgerSequence }).IsUnique();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.Status });
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.DataItemType });
    }
}

public sealed class LedgerTransactionInputRecordConfiguration : IEntityTypeConfiguration<LedgerTransactionInputRecord>
{
    public void Configure(EntityTypeBuilder<LedgerTransactionInputRecord> builder)
    {
        builder.ToTable("Economy_LedgerTransactionInputs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.PreviousTxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OutPointKey).HasMaxLength(128);
        builder.Property(x => x.SignaturePayloadHash).HasMaxLength(128);
        builder.HasIndex(x => new { x.TxId, x.InputIndex }).IsUnique();
        builder.HasIndex(x => x.OutPointKey);
        builder.HasIndex(x => x.Address);
    }
}

public sealed class LedgerTransactionOutputRecordConfiguration : IEntityTypeConfiguration<LedgerTransactionOutputRecord>
{
    public void Configure(EntityTypeBuilder<LedgerTransactionOutputRecord> builder)
    {
        builder.ToTable("Economy_LedgerTransactionOutputs", table =>
        {
            table.HasCheckConstraint("CK_Economy_TxOutput_MainNonNegative", "\"MainAmount\" >= 0");
            table.HasCheckConstraint(
                "CK_Economy_TxOutput_TokenPair",
                "(\"TokenId\" IS NULL AND \"TokenAmount\" IS NULL) OR (\"TokenId\" IS NOT NULL AND \"TokenAmount\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Economy_TxOutput_TokenPositive",
                "\"TokenAmount\" IS NULL OR \"TokenAmount\" > 0");
            table.HasCheckConstraint(
                "CK_Economy_TxOutput_AssetSeparation",
                "NOT (\"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Economy_TxOutput_HasValue",
                "\"MainAmount\" > 0 OR \"TokenId\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.MainAmount).HasColumnType("numeric").IsRequired();
        builder.Property(x => x.TokenId).HasMaxLength(128);
        builder.Property(x => x.TokenAmount).HasColumnType("numeric");
        builder.Property(x => x.BusinessObjectId).HasMaxLength(128);
        builder.Property(x => x.BusinessObjectDataHash).HasMaxLength(128);
        builder.Property(x => x.MetadataHash).HasMaxLength(128);
        builder.HasIndex(x => new { x.TxId, x.OutputIndex }).IsUnique();
        builder.HasIndex(x => x.Address);
        builder.HasIndex(x => x.BusinessObjectId);
    }
}

public sealed class UnspentOutputRecordConfiguration : IEntityTypeConfiguration<UnspentOutputRecord>
{
    public void Configure(EntityTypeBuilder<UnspentOutputRecord> builder)
    {
        builder.ToTable("Economy_UnspentOutputs", table =>
        {
            table.HasCheckConstraint("CK_Economy_Utxo_MainNonNegative", "\"MainAmount\" >= 0");
            table.HasCheckConstraint(
                "CK_Economy_Utxo_TokenPair",
                "(\"TokenId\" IS NULL AND \"TokenAmount\" IS NULL) OR (\"TokenId\" IS NOT NULL AND \"TokenAmount\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Economy_Utxo_TokenPositive",
                "\"TokenAmount\" IS NULL OR \"TokenAmount\" > 0");
            table.HasCheckConstraint(
                "CK_Economy_Utxo_AssetSeparation",
                "NOT (\"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Economy_Utxo_HasValue",
                "\"MainAmount\" > 0 OR \"TokenId\" IS NOT NULL");
        });
        builder.HasKey(x => x.OutPointKey);
        builder.Property(x => x.OutPointKey).HasMaxLength(128);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.TxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.MainAmount).HasColumnType("numeric").IsRequired();
        builder.Property(x => x.TokenId).HasMaxLength(128);
        builder.Property(x => x.TokenAmount).HasColumnType("numeric");
        builder.Property(x => x.BusinessObjectId).HasMaxLength(128);
        builder.Property(x => x.BusinessObjectDataHash).HasMaxLength(128);
        builder.Property(x => x.MetadataHash).HasMaxLength(128);
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.TxId, x.OutputIndex }).IsUnique();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.Address });
    }
}

public sealed class SpentOutputRecordConfiguration : IEntityTypeConfiguration<SpentOutputRecord>
{
    public void Configure(EntityTypeBuilder<SpentOutputRecord> builder)
    {
        builder.ToTable("Economy_SpentOutputs");
        builder.HasKey(x => x.OutPointKey);
        builder.Property(x => x.OutPointKey).HasMaxLength(128);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.TxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SpentInTxId).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.TxId, x.OutputIndex }).IsUnique();
        builder.HasIndex(x => x.SpentInTxId);
    }
}

public sealed class AddressStateProjectionRecordConfiguration : IEntityTypeConfiguration<AddressStateProjectionRecord>
{
    public void Configure(EntityTypeBuilder<AddressStateProjectionRecord> builder)
    {
        builder.ToTable("Economy_AddressStateProjections");
        builder.HasKey(x => x.ProjectionKey);
        builder.Property(x => x.ProjectionKey).HasMaxLength(512);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.MainBalance).HasColumnType("numeric").IsRequired();
        builder.Property(x => x.StateHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.Address }).IsUnique();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.LastLedgerSequence });
    }
}

public sealed class LedgerTokenTypeRecordConfiguration : IEntityTypeConfiguration<LedgerTokenTypeRecord>
{
    public void Configure(EntityTypeBuilder<LedgerTokenTypeRecord> builder)
    {
        builder.ToTable("Economy_LedgerTokenTypes");
        builder.HasKey(x => x.TokenTypeKey);
        builder.Property(x => x.TokenTypeKey).HasMaxLength(512);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.TokenId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DefinitionHash).HasMaxLength(128);
        builder.Property(x => x.TokenNamespace).HasMaxLength(128);
        builder.Property(x => x.Symbol).HasMaxLength(64);
        builder.Property(x => x.IssuerAddress).HasMaxLength(256);
        builder.Property(x => x.InitialSupply).HasColumnType("numeric");
        builder.Property(x => x.DisplayName).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.DisplayExternalRef).HasMaxLength(2048);
        builder.Property(x => x.CreatedTxId).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.TokenId }).IsUnique();
    }
}

public sealed class RegisteredAddressRecordConfiguration : IEntityTypeConfiguration<RegisteredAddressRecord>
{
    public void Configure(EntityTypeBuilder<RegisteredAddressRecord> builder)
    {
        builder.ToTable("Economy_RegisteredAddresses");
        builder.HasKey(x => x.AddressKey);
        builder.Property(x => x.AddressKey).HasMaxLength(512);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PublicKeyHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.Address }).IsUnique();
    }
}

public sealed class BusinessObjectRecordConfiguration : IEntityTypeConfiguration<BusinessObjectRecord>
{
    public void Configure(EntityTypeBuilder<BusinessObjectRecord> builder)
    {
        builder.ToTable("Economy_BusinessObjects");
        builder.HasKey(x => x.BusinessObjectKey);
        builder.Property(x => x.BusinessObjectKey).HasMaxLength(512);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.ObjectId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ObjectType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CurrentOwnerAddress).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CurrentState).IsRequired();
        builder.Property(x => x.CurrentHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.ObjectId }).IsUnique();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.CurrentOwnerAddress });
    }
}

public sealed class BusinessObjectVersionRecordConfiguration : IEntityTypeConfiguration<BusinessObjectVersionRecord>
{
    public void Configure(EntityTypeBuilder<BusinessObjectVersionRecord> builder)
    {
        builder.ToTable("Economy_BusinessObjectVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.ObjectId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TxId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OwnerAddress).HasMaxLength(256).IsRequired();
        builder.Property(x => x.State).IsRequired();
        builder.Property(x => x.SchemaVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DataHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.ObjectId, x.Version }).IsUnique();
        builder.HasIndex(x => x.TxId);
    }
}

public sealed class LedgerSnapshotRecordConfiguration : IEntityTypeConfiguration<LedgerSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<LedgerSnapshotRecord> builder)
    {
        builder.ToTable("Economy_LedgerSnapshots");
        builder.HasKey(x => x.SnapshotId);
        builder.Property(x => x.SnapshotId).HasMaxLength(128);
        builder.Property(x => x.LedgerId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ScenarioId).HasMaxLength(128);
        builder.Property(x => x.LedgerHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.UtxoSetHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.AddressStateHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.BusinessObjectStateHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.LedgerId, x.ScenarioId, x.LedgerSequence }).IsUnique();
    }
}
