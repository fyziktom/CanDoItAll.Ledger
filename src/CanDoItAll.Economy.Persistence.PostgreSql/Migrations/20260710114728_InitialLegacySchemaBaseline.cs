using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanDoItAll.Economy.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialLegacySchemaBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Economy_AddressStateProjections",
                columns: table => new
                {
                    ProjectionKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MainBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    TokenBalancesJson = table.Column<string>(type: "text", nullable: false),
                    UtxoCount = table.Column<int>(type: "integer", nullable: false),
                    LastLedgerSequence = table.Column<long>(type: "bigint", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AdminSignatureJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_AddressStateProjections", x => x.ProjectionKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_BusinessObjects",
                columns: table => new
                {
                    BusinessObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObjectType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    CurrentOwnerAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CurrentState = table.Column<string>(type: "text", nullable: false),
                    CurrentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrentOutPointTxId = table.Column<string>(type: "text", nullable: false),
                    CurrentOutPointIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedTxId = table.Column<string>(type: "text", nullable: false),
                    UpdatedTxId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_BusinessObjects", x => x.BusinessObjectKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_BusinessObjectVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutPointTxId = table.Column<string>(type: "text", nullable: false),
                    OutPointIndex = table.Column<int>(type: "integer", nullable: false),
                    OwnerAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_BusinessObjectVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerSnapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LedgerSequence = table.Column<long>(type: "bigint", nullable: false),
                    LedgerHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UtxoSetHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AddressStateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BusinessObjectStateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AdminSignatureJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerSnapshots", x => x.SnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerStates",
                columns: table => new
                {
                    StateKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentSequence = table.Column<long>(type: "bigint", nullable: false),
                    CurrentLedgerHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerStates", x => x.StateKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerTokenTypes",
                columns: table => new
                {
                    TokenTypeKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProtocolVersion = table.Column<int>(type: "integer", nullable: false),
                    DefinitionHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenNamespace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IssuerAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InitialSupply = table.Column<decimal>(type: "numeric", nullable: true),
                    DecimalPlaces = table.Column<int>(type: "integer", nullable: true),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DisplayExternalRef = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedTxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedOutputIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerTokenTypes", x => x.TokenTypeKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerTransactionInputs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InputIndex = table.Column<int>(type: "integer", nullable: false),
                    PreviousTxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PreviousOutputIndex = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OutPointKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SignaturePayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SignatureBundleJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerTransactionInputs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerTransactionOutputs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputIndex = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MainAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TokenId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    BusinessObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BusinessObjectVersion = table.Column<int>(type: "integer", nullable: true),
                    BusinessObjectOperation = table.Column<int>(type: "integer", nullable: true),
                    BusinessObjectDataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerTransactionOutputs", x => x.Id);
                    table.CheckConstraint("CK_Economy_TxOutput_AssetSeparation", "NOT (\"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Economy_TxOutput_HasValue", "\"MainAmount\" > 0 OR \"TokenId\" IS NOT NULL");
                    table.CheckConstraint("CK_Economy_TxOutput_MainNonNegative", "\"MainAmount\" >= 0");
                    table.CheckConstraint("CK_Economy_TxOutput_TokenPair", "(\"TokenId\" IS NULL AND \"TokenAmount\" IS NULL) OR (\"TokenId\" IS NOT NULL AND \"TokenAmount\" IS NOT NULL)");
                    table.CheckConstraint("CK_Economy_TxOutput_TokenPositive", "\"TokenAmount\" IS NULL OR \"TokenAmount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Economy_LedgerTransactions",
                columns: table => new
                {
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LedgerSequence = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SigningMode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PreviousLedgerHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LedgerEntryHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TxHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DataItemType = table.Column<int>(type: "integer", nullable: true),
                    DataContentType = table.Column<string>(type: "text", nullable: true),
                    DataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InlineJson = table.Column<string>(type: "text", nullable: true),
                    ExternalRef = table.Column<string>(type: "text", nullable: true),
                    TokenDefinitionJson = table.Column<string>(type: "text", nullable: true),
                    AdminSignatureJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_LedgerTransactions", x => x.TxId);
                });

            migrationBuilder.CreateTable(
                name: "Economy_RegisteredAddresses",
                columns: table => new
                {
                    AddressKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KeyAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    PublicKeyMaterial = table.Column<byte[]>(type: "bytea", nullable: false),
                    PublicKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignaturePolicyJson = table.Column<string>(type: "text", nullable: false),
                    AdminSignatureJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_RegisteredAddresses", x => x.AddressKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_SpentOutputs",
                columns: table => new
                {
                    OutPointKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputIndex = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SpentInTxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SpentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AdminSignatureJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_SpentOutputs", x => x.OutPointKey);
                });

            migrationBuilder.CreateTable(
                name: "Economy_UnspentOutputs",
                columns: table => new
                {
                    OutPointKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LedgerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScenarioId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TxId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputIndex = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MainAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    TokenId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    BusinessObjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BusinessObjectVersion = table.Column<int>(type: "integer", nullable: true),
                    BusinessObjectOperation = table.Column<int>(type: "integer", nullable: true),
                    BusinessObjectDataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MetadataHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy_UnspentOutputs", x => x.OutPointKey);
                    table.CheckConstraint("CK_Economy_Utxo_AssetSeparation", "NOT (\"MainAmount\" > 0 AND \"TokenId\" IS NOT NULL)");
                    table.CheckConstraint("CK_Economy_Utxo_HasValue", "\"MainAmount\" > 0 OR \"TokenId\" IS NOT NULL");
                    table.CheckConstraint("CK_Economy_Utxo_MainNonNegative", "\"MainAmount\" >= 0");
                    table.CheckConstraint("CK_Economy_Utxo_TokenPair", "(\"TokenId\" IS NULL AND \"TokenAmount\" IS NULL) OR (\"TokenId\" IS NOT NULL AND \"TokenAmount\" IS NOT NULL)");
                    table.CheckConstraint("CK_Economy_Utxo_TokenPositive", "\"TokenAmount\" IS NULL OR \"TokenAmount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_AddressStateProjections_LedgerId_ScenarioId_Address",
                table: "Economy_AddressStateProjections",
                columns: new[] { "LedgerId", "ScenarioId", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_AddressStateProjections_LedgerId_ScenarioId_LastLed~",
                table: "Economy_AddressStateProjections",
                columns: new[] { "LedgerId", "ScenarioId", "LastLedgerSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_BusinessObjects_LedgerId_ScenarioId_CurrentOwnerAdd~",
                table: "Economy_BusinessObjects",
                columns: new[] { "LedgerId", "ScenarioId", "CurrentOwnerAddress" });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_BusinessObjects_LedgerId_ScenarioId_ObjectId",
                table: "Economy_BusinessObjects",
                columns: new[] { "LedgerId", "ScenarioId", "ObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_BusinessObjectVersions_LedgerId_ScenarioId_ObjectId~",
                table: "Economy_BusinessObjectVersions",
                columns: new[] { "LedgerId", "ScenarioId", "ObjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_BusinessObjectVersions_TxId",
                table: "Economy_BusinessObjectVersions",
                column: "TxId");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerSnapshots_LedgerId_ScenarioId_LedgerSequence",
                table: "Economy_LedgerSnapshots",
                columns: new[] { "LedgerId", "ScenarioId", "LedgerSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerStates_LedgerId_ScenarioId",
                table: "Economy_LedgerStates",
                columns: new[] { "LedgerId", "ScenarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTokenTypes_LedgerId_ScenarioId_TokenId",
                table: "Economy_LedgerTokenTypes",
                columns: new[] { "LedgerId", "ScenarioId", "TokenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionInputs_Address",
                table: "Economy_LedgerTransactionInputs",
                column: "Address");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionInputs_OutPointKey",
                table: "Economy_LedgerTransactionInputs",
                column: "OutPointKey");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionInputs_TxId_InputIndex",
                table: "Economy_LedgerTransactionInputs",
                columns: new[] { "TxId", "InputIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionOutputs_Address",
                table: "Economy_LedgerTransactionOutputs",
                column: "Address");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionOutputs_BusinessObjectId",
                table: "Economy_LedgerTransactionOutputs",
                column: "BusinessObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactionOutputs_TxId_OutputIndex",
                table: "Economy_LedgerTransactionOutputs",
                columns: new[] { "TxId", "OutputIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactions_LedgerId_ScenarioId_DataItemType",
                table: "Economy_LedgerTransactions",
                columns: new[] { "LedgerId", "ScenarioId", "DataItemType" });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactions_LedgerId_ScenarioId_LedgerSequen~",
                table: "Economy_LedgerTransactions",
                columns: new[] { "LedgerId", "ScenarioId", "LedgerSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_LedgerTransactions_LedgerId_ScenarioId_Status",
                table: "Economy_LedgerTransactions",
                columns: new[] { "LedgerId", "ScenarioId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_RegisteredAddresses_LedgerId_Address",
                table: "Economy_RegisteredAddresses",
                columns: new[] { "LedgerId", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_SpentOutputs_LedgerId_ScenarioId_TxId_OutputIndex",
                table: "Economy_SpentOutputs",
                columns: new[] { "LedgerId", "ScenarioId", "TxId", "OutputIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Economy_SpentOutputs_SpentInTxId",
                table: "Economy_SpentOutputs",
                column: "SpentInTxId");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_UnspentOutputs_LedgerId_ScenarioId_Address",
                table: "Economy_UnspentOutputs",
                columns: new[] { "LedgerId", "ScenarioId", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_Economy_UnspentOutputs_LedgerId_ScenarioId_TxId_OutputIndex",
                table: "Economy_UnspentOutputs",
                columns: new[] { "LedgerId", "ScenarioId", "TxId", "OutputIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Economy_AddressStateProjections");

            migrationBuilder.DropTable(
                name: "Economy_BusinessObjects");

            migrationBuilder.DropTable(
                name: "Economy_BusinessObjectVersions");

            migrationBuilder.DropTable(
                name: "Economy_LedgerSnapshots");

            migrationBuilder.DropTable(
                name: "Economy_LedgerStates");

            migrationBuilder.DropTable(
                name: "Economy_LedgerTokenTypes");

            migrationBuilder.DropTable(
                name: "Economy_LedgerTransactionInputs");

            migrationBuilder.DropTable(
                name: "Economy_LedgerTransactionOutputs");

            migrationBuilder.DropTable(
                name: "Economy_LedgerTransactions");

            migrationBuilder.DropTable(
                name: "Economy_RegisteredAddresses");

            migrationBuilder.DropTable(
                name: "Economy_SpentOutputs");

            migrationBuilder.DropTable(
                name: "Economy_UnspentOutputs");
        }
    }
}
