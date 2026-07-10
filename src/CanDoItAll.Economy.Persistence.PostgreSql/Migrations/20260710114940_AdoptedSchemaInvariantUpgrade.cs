using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Economy.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AdoptedSchemaInvariantUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Economy_LedgerTransactions"
                    ADD COLUMN IF NOT EXISTS "TokenDefinitionJson" text NULL;

                ALTER TABLE "Economy_LedgerTokenTypes"
                    ADD COLUMN IF NOT EXISTS "ProtocolVersion" integer NOT NULL DEFAULT 1,
                    ADD COLUMN IF NOT EXISTS "DefinitionHash" character varying(128) NULL,
                    ADD COLUMN IF NOT EXISTS "TokenNamespace" character varying(128) NULL,
                    ADD COLUMN IF NOT EXISTS "Symbol" character varying(64) NULL,
                    ADD COLUMN IF NOT EXISTS "IssuerAddress" character varying(256) NULL,
                    ADD COLUMN IF NOT EXISTS "InitialSupply" numeric NULL,
                    ADD COLUMN IF NOT EXISTS "DecimalPlaces" integer NULL,
                    ADD COLUMN IF NOT EXISTS "Classification" integer NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "DisplayName" character varying(160) NULL,
                    ADD COLUMN IF NOT EXISTS "Description" character varying(2048) NULL,
                    ADD COLUMN IF NOT EXISTS "DisplayExternalRef" character varying(2048) NULL;

                ALTER TABLE "Economy_LedgerTokenTypes" ALTER COLUMN "ProtocolVersion" DROP DEFAULT;
                ALTER TABLE "Economy_LedgerTokenTypes" ALTER COLUMN "Classification" DROP DEFAULT;

                DO $upgrade$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_TxOutput_MainNonNegative') THEN
                        ALTER TABLE "Economy_LedgerTransactionOutputs" ADD CONSTRAINT "CK_Economy_TxOutput_MainNonNegative" CHECK ("MainAmount" >= 0);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_TxOutput_TokenPair') THEN
                        ALTER TABLE "Economy_LedgerTransactionOutputs" ADD CONSTRAINT "CK_Economy_TxOutput_TokenPair" CHECK (("TokenId" IS NULL AND "TokenAmount" IS NULL) OR ("TokenId" IS NOT NULL AND "TokenAmount" IS NOT NULL));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_TxOutput_TokenPositive') THEN
                        ALTER TABLE "Economy_LedgerTransactionOutputs" ADD CONSTRAINT "CK_Economy_TxOutput_TokenPositive" CHECK ("TokenAmount" IS NULL OR "TokenAmount" > 0);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_TxOutput_AssetSeparation') THEN
                        ALTER TABLE "Economy_LedgerTransactionOutputs" ADD CONSTRAINT "CK_Economy_TxOutput_AssetSeparation" CHECK (NOT ("MainAmount" > 0 AND "TokenId" IS NOT NULL));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_TxOutput_HasValue') THEN
                        ALTER TABLE "Economy_LedgerTransactionOutputs" ADD CONSTRAINT "CK_Economy_TxOutput_HasValue" CHECK ("MainAmount" > 0 OR "TokenId" IS NOT NULL);
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_Utxo_MainNonNegative') THEN
                        ALTER TABLE "Economy_UnspentOutputs" ADD CONSTRAINT "CK_Economy_Utxo_MainNonNegative" CHECK ("MainAmount" >= 0);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_Utxo_TokenPair') THEN
                        ALTER TABLE "Economy_UnspentOutputs" ADD CONSTRAINT "CK_Economy_Utxo_TokenPair" CHECK (("TokenId" IS NULL AND "TokenAmount" IS NULL) OR ("TokenId" IS NOT NULL AND "TokenAmount" IS NOT NULL));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_Utxo_TokenPositive') THEN
                        ALTER TABLE "Economy_UnspentOutputs" ADD CONSTRAINT "CK_Economy_Utxo_TokenPositive" CHECK ("TokenAmount" IS NULL OR "TokenAmount" > 0);
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_Utxo_AssetSeparation') THEN
                        ALTER TABLE "Economy_UnspentOutputs" ADD CONSTRAINT "CK_Economy_Utxo_AssetSeparation" CHECK (NOT ("MainAmount" > 0 AND "TokenId" IS NOT NULL));
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_Economy_Utxo_HasValue') THEN
                        ALTER TABLE "Economy_UnspentOutputs" ADD CONSTRAINT "CK_Economy_Utxo_HasValue" CHECK ("MainAmount" > 0 OR "TokenId" IS NOT NULL);
                    END IF;
                END
                $upgrade$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Economy_LedgerTransactionOutputs"
                    DROP CONSTRAINT IF EXISTS "CK_Economy_TxOutput_MainNonNegative",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_TxOutput_TokenPair",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_TxOutput_TokenPositive",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_TxOutput_AssetSeparation",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_TxOutput_HasValue";
                ALTER TABLE "Economy_UnspentOutputs"
                    DROP CONSTRAINT IF EXISTS "CK_Economy_Utxo_MainNonNegative",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_Utxo_TokenPair",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_Utxo_TokenPositive",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_Utxo_AssetSeparation",
                    DROP CONSTRAINT IF EXISTS "CK_Economy_Utxo_HasValue";
                ALTER TABLE "Economy_LedgerTransactions" DROP COLUMN IF EXISTS "TokenDefinitionJson";
                ALTER TABLE "Economy_LedgerTokenTypes"
                    DROP COLUMN IF EXISTS "ProtocolVersion",
                    DROP COLUMN IF EXISTS "DefinitionHash",
                    DROP COLUMN IF EXISTS "TokenNamespace",
                    DROP COLUMN IF EXISTS "Symbol",
                    DROP COLUMN IF EXISTS "IssuerAddress",
                    DROP COLUMN IF EXISTS "InitialSupply",
                    DROP COLUMN IF EXISTS "DecimalPlaces",
                    DROP COLUMN IF EXISTS "Classification",
                    DROP COLUMN IF EXISTS "DisplayName",
                    DROP COLUMN IF EXISTS "Description",
                    DROP COLUMN IF EXISTS "DisplayExternalRef";
                """);
        }
    }
}
