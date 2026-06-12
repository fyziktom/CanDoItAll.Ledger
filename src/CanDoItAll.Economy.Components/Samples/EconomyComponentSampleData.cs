using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using CanDoItAll.Economy.Cryptography;

namespace CanDoItAll.Economy.Components.Samples;

public static class EconomyComponentSampleData
{
    public static BusinessObjectWorkspaceModel CreateWorkspace()
    {
        var context = new LedgerAccountContext(new LedgerId("company-economy"), new ScenarioId("forward-sim-q2"));
        var primaryAddress = AddressForKey("operations-primary");
        var approvalAddress = AddressForKey("operations-approval");
        var warehouseAddress = AddressForKey("warehouse-north");
        var supplierAddress = AddressForKey("supplier-acme");
        var treasuryAddress = AddressForKey("treasury-mirror");
        var viewedAt = new DateTimeOffset(2026, 05, 06, 14, 30, 00, TimeSpan.Zero);
        IReadOnlyList<IMediaBusinessObjectView> mediaObjects = CreateMediaObjects(context, primaryAddress, viewedAt);
        AccountRuleDefinition cashpoolRule = CreateCashpoolRule(treasuryAddress);
        TransactionData cashpoolRuleData = RuleTokenAuthority.CreateData(cashpoolRule);
        HashHex cashpoolRuleHash = cashpoolRuleData.DataHash
            ?? throw new InvalidOperationException("Sample rule data did not produce a hash.");

        var businessObjects = new List<IBusinessObjectView>(mediaObjects)
        {
            new GenericBusinessObjectView(
                context,
                RuleTokenAuthority.CreateRuleObjectId(cashpoolRule.RuleId),
                "account-rule",
                "1.0",
                1,
                primaryAddress,
                BusinessObjectOperation.Create,
                cashpoolRuleHash,
                new OutPoint(new TxId("tx-rule-cashpool"), 0),
                cashpoolRuleData.InlineJson,
                null,
                TransactionDataItemType.Rule,
                viewedAt.AddHours(-3),
                viewedAt),
            new GenericBusinessObjectView(
                context,
                new BusinessObjectId("bo:purchase-order:1042"),
                "purchase-order",
                "1.0",
                3,
                primaryAddress,
                BusinessObjectOperation.Update,
                new HashHex("6ac9f112c0ffee9a58f399dba4e1841a101d93462d3a7b19f5032aebd9e90100"),
                new OutPoint(new TxId("tx-po-1042-v3"), 0),
                "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"approved\"}",
                "erp://purchase-orders/1042",
                TransactionDataItemType.BusinessObject,
                viewedAt.AddHours(-6),
                viewedAt),
            new AccountSettingsBusinessObjectView(
                context,
                new BusinessObjectId("bo:settings:ops"),
                "account-settings",
                "1.0",
                1,
                primaryAddress,
                BusinessObjectOperation.Create,
                new HashHex("21b7a4c2b2bb74e63cfae2839116e81a11f4505230089a127a30cb6f19d01221"),
                new OutPoint(new TxId("tx-settings-ops"), 0),
                "{\"currency\":\"USD\",\"approvalMode\":\"two-step\",\"simulationClock\":\"manual\"}",
                null,
                new Dictionary<string, string>
                {
                    ["approvalMode"] = "two-step",
                    ["currency"] = "USD",
                    ["simulationClock"] = "manual"
                },
                viewedAt.AddDays(-1),
                viewedAt),
            new GenericBusinessObjectView(
                context,
                new BusinessObjectId("bo:inventory-lot:steel-77"),
                "inventory-lot",
                "2.1",
                5,
                warehouseAddress,
                BusinessObjectOperation.Transfer,
                new HashHex("f8f744cc3d865fa7263caa9317d60f8a0ae03136ae189d8471c8c55462d22145"),
                new OutPoint(new TxId("tx-steel-77-transfer"), 1),
                "{\"sku\":\"steel-sheet\",\"quantity\":900,\"uom\":\"kg\",\"reserved\":120}",
                "wms://lots/steel-77",
                TransactionDataItemType.BusinessObject,
                viewedAt.AddHours(-18),
                viewedAt)
        };

        var balance = new LedgerAccountBalance(
            context,
            primaryAddress,
            Amount.FromInt64(126500),
            new Dictionary<TokenId, Amount>
            {
                [new TokenId("USD-CREDIT")] = Amount.FromInt64(39000),
                [new TokenId("SUPPLIER-SLOT")] = Amount.FromInt64(7)
            },
            11,
            viewedAt);

        IReadOnlyList<BusinessObjectSubAccountSummary> subAccounts =
        [
            new("Operations approval", approvalAddress, Amount.FromInt64(25000), 4, 8, viewedAt.AddMinutes(-42)),
            new("Warehouse north", warehouseAddress, Amount.FromInt64(41000), 6, 14, viewedAt.AddHours(-2)),
            new("Treasury mirror", treasuryAddress, Amount.FromInt64(60500), 9, 3, viewedAt.AddDays(-1))
        ];

        IReadOnlyList<LedgerTransactionSummary> transactions =
        [
            new(
                context,
                new TxId("tx-po-1042-v3"),
                TransactionKind.BusinessObject,
                TransactionStatus.Confirmed,
                viewedAt.AddHours(-6),
                [primaryAddress],
                [primaryAddress],
                [new LedgerTransactionInputSummary(0, new OutPoint(new TxId("tx-po-1042-v2"), 0), primaryAddress)],
                [new LedgerTransactionOutputSummary(0, primaryAddress, Amount.Zero)]),
            new(
                context,
                new TxId("tx-steel-77-transfer"),
                TransactionKind.Transfer,
                TransactionStatus.Confirmed,
                viewedAt.AddHours(-18),
                [primaryAddress],
                [warehouseAddress],
                [new LedgerTransactionInputSummary(0, new OutPoint(new TxId("tx-steel-77-create"), 1), primaryAddress)],
                [new LedgerTransactionOutputSummary(0, warehouseAddress, Amount.FromInt64(0))]),
            new(
                context,
                new TxId("tx-settings-ops"),
                TransactionKind.Mint,
                TransactionStatus.Confirmed,
                viewedAt.AddDays(-1),
                [],
                [primaryAddress],
                [],
                [new LedgerTransactionOutputSummary(0, primaryAddress, Amount.Zero)])
        ];

        var draft = new BusinessObjectTransactionDraft(
            new BusinessObjectId("bo:purchase-order:1043"),
            "purchase-order",
            "1.0",
            primaryAddress,
            approvalAddress,
            supplierAddress,
            Amount.FromInt64(0),
            Amount.FromInt64(42000),
            "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"draft\"}",
            "erp://purchase-orders/1043");

        var editor = new BusinessObjectEditorModel(
            new BusinessObjectId("bo:purchase-order:1042"),
            "purchase-order",
            "1.0",
            primaryAddress,
            "{\"supplier\":\"Acme Metals\",\"amount\":42000,\"status\":\"approved\"}",
            viewedAt.AddDays(30));

        IReadOnlyList<LedgerSpendableOutput> spendableOutputs =
        [
            new(
                context,
                new OutPointKey(new HashHex("0f31b8f4cafe0101010101010101010101010101010101010101010101010101")),
                new OutPoint(new TxId("tx-main-source"), 0),
                primaryAddress,
                Amount.FromInt64(40000),
                null,
                null,
                viewedAt.AddHours(-2)),
            new(
                context,
                new OutPointKey(new HashHex("1f31b8f4cafe0202020202020202020202020202020202020202020202020202")),
                new OutPoint(new TxId("tx-token-source"), 1),
                primaryAddress,
                Amount.Zero,
                new TokenTransfer(new TokenId("USD-CREDIT"), Amount.FromInt64(39000)),
                null,
                viewedAt.AddHours(-6)),
            new(
                context,
                new OutPointKey(new HashHex("2f31b8f4cafe0303030303030303030303030303030303030303030303030303")),
                new OutPoint(new TxId("tx-permission-source"), 0),
                primaryAddress,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(1)),
                new BusinessObjectRef(
                    PermissionTokenAuthority.CreatePermissionObjectId(
                        PermissionTokenAuthority.MintNewTokenTypeAction,
                        "ops.credit",
                        "sample"),
                    1,
                    BusinessObjectOperation.Create,
                    new HashHex("3f31b8f4cafe0404040404040404040404040404040404040404040404040404")),
                viewedAt.AddDays(-1)),
            new(
                context,
                new OutPointKey(new HashHex("3f31b8f4cafe0303030303030303030303030303030303030303030303030303")),
                new OutPoint(new TxId("tx-rule-permission-source"), 0),
                primaryAddress,
                Amount.Zero,
                new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(5)),
                new BusinessObjectRef(
                    PermissionTokenAuthority.CreatePermissionObjectId(
                        RuleTokenAuthority.CreateRuleTokenAction,
                        "rules.company",
                        "sample"),
                    1,
                    BusinessObjectOperation.Create,
                    new HashHex("4f31b8f4cafe0404040404040404040404040404040404040404040404040404")),
                viewedAt.AddHours(-4)),
            new(
                context,
                new OutPointKey(new HashHex("4f31b8f4cafe0303030303030303030303030303030303030303030303030303")),
                new OutPoint(new TxId("tx-rule-cashpool"), 0),
                primaryAddress,
                Amount.Zero,
                new TokenTransfer(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1)),
                new BusinessObjectRef(
                    RuleTokenAuthority.CreateRuleObjectId(cashpoolRule.RuleId),
                    1,
                    BusinessObjectOperation.Create,
                    cashpoolRuleHash),
                viewedAt.AddHours(-3))
        ];

        IReadOnlyList<LedgerTokenType> tokenTypes =
        [
            new(new TokenId("USD-CREDIT"), Amount.FromInt64(39000), Amount.FromInt64(39000), Amount.Zero, 1, 0, 1),
            new(new TokenId("SUPPLIER-SLOT"), Amount.FromInt64(7), Amount.Zero, Amount.FromInt64(7), 0, 1, 1),
            new(PermissionTokenAuthority.PermissionTokenId, Amount.FromInt64(6), Amount.FromInt64(6), Amount.Zero, 2, 0, 2),
            new(RuleTokenAuthority.RuleTokenId, Amount.FromInt64(1), Amount.Zero, Amount.FromInt64(1), 0, 1, 1)
        ];

        IReadOnlyList<LedgerPermissionSource> permissionSources =
        [
            new(spendableOutputs[2], PermissionTokenAuthority.CreateMintNewTokenTypeGrant("ops.credit", repeatable: true)),
            new(spendableOutputs[3], RuleTokenAuthority.CreateRuleTokenPermissionGrant("rules.company", repeatable: true))
        ];

        return new BusinessObjectWorkspaceModel(
            "Operations account",
            context,
            primaryAddress,
            balance,
            CreateTokenOptions(balance),
            [new(
                "ops.credit",
                PermissionTokenAuthority.CreatePermissionObjectId(
                    PermissionTokenAuthority.MintNewTokenTypeAction,
                    "ops.credit",
                    "sample"),
                primaryAddress)],
            [
                new("Operations approval", approvalAddress),
                new("Warehouse north", warehouseAddress),
                new("Supplier Acme", supplierAddress)
            ],
            businessObjects,
            subAccounts,
            transactions,
            draft,
            editor,
            viewedAt,
            AccountAlias: "operations",
            NodeBaseUri: new Uri("http://localhost:5000"),
            ConnectionLabel: "sample in-memory",
            SpendableOutputs: spendableOutputs,
            TokenTypes: tokenTypes,
            PermissionSources: permissionSources);
    }

    public static EconomyTransactionDetailsModel CreateTransactionDetails()
    {
        BusinessObjectWorkspaceModel workspace = CreateWorkspace();
        IBusinessObjectView businessObject = workspace.BusinessObjects
            .First(item => item.ObjectId.Value.Equals("bo:purchase-order:1042", StringComparison.Ordinal));
        DateTimeOffset timestamp = workspace.ViewedAtUtc.AddHours(-6);
        var payloadHash = new HashHex("4f31b8f4cafe0505050505050505050505050505050505050505050505050505");
        var signature = new SignatureEnvelope(
            SignatureAlgorithmId.EcdsaP256Sha256,
            "operations-primary",
            new HashHex("5f31b8f4cafe0606060606060606060606060606060606060606060606060606"),
            payloadHash,
            [1, 3, 5, 8, 13, 21],
            [34, 55, 89]);

        return new EconomyTransactionDetailsModel(
            new TxId("tx-po-1042-v3"),
            new HashHex("6f31b8f4cafe0707070707070707070707070707070707070707070707070707"),
            TransactionKind.BusinessObject,
            TransactionStatus.Confirmed,
            SigningMode.Independent,
            timestamp,
            new TransactionData(
                "application/json",
                businessObject.DataHash,
                businessObject.StateJson,
                "erp://purchase-orders/1042",
                TransactionDataItemType.BusinessObject),
            [
                new EconomyTransactionInputDetails(
                    0,
                    new OutPoint(new TxId("tx-po-1042-v2"), 0),
                    workspace.Address,
                    Amount.Zero,
                    null,
                    new BusinessObjectRef(
                        businessObject.ObjectId,
                        businessObject.Version - 1,
                        BusinessObjectOperation.Update,
                        new HashHex("7f31b8f4cafe0808080808080808080808080808080808080808080808080808")),
                    new SignatureBundle([signature]),
                    payloadHash,
                    new OutPointKey(new HashHex("8f31b8f4cafe0909090909090909090909090909090909090909090909090909")))
            ],
            [
                new EconomyTransactionOutputDetails(
                    0,
                    workspace.Address,
                    Amount.Zero,
                    null,
                    new BusinessObjectRef(
                        businessObject.ObjectId,
                        businessObject.Version,
                        businessObject.Operation,
                        businessObject.DataHash),
                    businessObject.DataHash)
            ]);
    }

    private static IReadOnlyList<EconomyTokenOption> CreateTokenOptions(LedgerAccountBalance balance)
        =>
        [
            new(EconomyTokenOption.MainKey, "Main", null, balance.MainBalance, IsMain: true),
            .. balance.TokenBalances
                .OrderBy(token => token.Key.Value, StringComparer.Ordinal)
                .Select(token => new EconomyTokenOption(token.Key.Value, token.Key.Value, token.Key, token.Value, IsMain: false, CanCreateMore: true))
        ];

    private static AccountRuleDefinition CreateCashpoolRule(Address receiverAddress)
        => new(
            "cashpool-sweep",
            "rules.company",
            Enabled: true,
            new AccountRuleTrigger(AccountRuleTriggerKind.AccountSnapshot),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["limit"] = "1000"
            },
            ["account_main > limit"],
            [
                new AccountRuleAction(
                    AccountRuleActionKind.Payment,
                    ReceiverAddress: receiverAddress.Value,
                    AmountExpression: "account_main - limit")
            ],
            new AccountRuleProcessingOptions(
                MarkTriggerProcessed: false,
                ProcessedProperty: "processed",
                KeepProcessedBusinessObject: true,
                MaxActionsPerRefresh: 5));

    public static AccountBackupWorkflowModel CreateAccountBackup()
        => new(
            "Operations account",
            AddressForKey("operations-primary"),
            "operations-account-20260506.cdabackup",
            BackupAvailable: true,
            PasswordConfirmed: true,
            ImportMode: false,
            Step: 2,
            "Encrypted backup is ready for this account.");

    public static BusinessObjectCreationWizardModel CreatePostWizard()
    {
        IReadOnlyList<BusinessObjectDataItem> items =
        [
            ImageItem(
                "main-photo.png",
                "QmPostMainPhoto20260506",
                "https://picsum.photos/seed/candoitall-post-main/900/600",
                true,
                "Main post image"),
            new(
                "post-body.md",
                BusinessObjectDataItemType.Text,
                BusinessObjectDataItemStorage.Ipfs,
                "QmPostTextBody20260506",
                null,
                "text/markdown",
                "post-body.md",
                1834,
                false,
                "Markdown long-form content"),
            new(
                "chart.json",
                BusinessObjectDataItemType.Json,
                BusinessObjectDataItemStorage.Ipfs,
                "QmPostChartJson20260506",
                null,
                "application/json",
                "chart.json",
                956,
                false,
                "Structured simulation data")
        ];

        return new(
            "post",
            Step: 2,
            "Quarterly energy balance",
            "#energy #simulation #operations",
            "Company economy simulation note with attached evidence.",
            "This post records a forward-simulation checkpoint. It combines operator notes, IPFS-backed data, and a chart payload so the BO can travel through the ledger with all supporting files attached.",
            items,
            items[0]);
    }

    public static BusinessObjectCreationWizardModel CreateImageWizard()
    {
        IReadOnlyList<BusinessObjectDataItem> items =
        [
            ImageItem(
                "warehouse-model.png",
                "QmWarehouseModel20260506",
                "https://picsum.photos/seed/candoitall-image-main/900/600",
                true,
                "Primary image"),
            ImageItem(
                "detail-angle.png",
                "QmWarehouseModelDetail20260506",
                "https://picsum.photos/seed/candoitall-image-detail/900/600",
                false,
                "Detail angle")
        ];

        return new(
            "image",
            Step: 1,
            "Warehouse capacity model",
            "#warehouse #capacity #model",
            "Visual BO for the north warehouse simulation.",
            "The image BO captures visual state that later calculations can reference without embedding binary payloads directly into ledger transactions.",
            items,
            items[0]);
    }

    public static IReadOnlyList<BusinessObjectGalleryItem> CreateGalleryItems()
    {
        var context = new LedgerAccountContext(new LedgerId("company-economy"), new ScenarioId("forward-sim-q2"));
        var primaryAddress = AddressForKey("operations-primary");
        var viewedAt = new DateTimeOffset(2026, 05, 06, 14, 30, 00, TimeSpan.Zero);

        return CreateMediaObjects(context, primaryAddress, viewedAt)
            .Select((item, index) => new BusinessObjectGalleryItem(
                item,
                ResolvePreview(item),
                IsOwnObject: index is not 2,
                IsOffered: index is 1,
                Accent: index switch
                {
                    0 => "#2c7a7b",
                    1 => "#8a5a18",
                    2 => "#5f6f52",
                    _ => "#576574"
                }))
            .ToArray();
    }

    private static IReadOnlyList<IMediaBusinessObjectView> CreateMediaObjects(
        LedgerAccountContext context,
        Address primaryAddress,
        DateTimeOffset viewedAt)
    {
        IReadOnlyList<BusinessObjectDataItem> energyItems =
        [
            ImageItem(
                "energy-balance.png",
                "QmEnergyBalanceMain20260506",
                "https://picsum.photos/seed/candoitall-energy-balance/900/600",
                true,
                "Energy balance image"),
            new(
                "energy-balance.json",
                BusinessObjectDataItemType.Json,
                BusinessObjectDataItemStorage.Ipfs,
                "QmEnergyBalanceJson20260506",
                null,
                "application/json",
                "energy-balance.json",
                2448,
                false,
                "Simulation source data")
        ];

        IReadOnlyList<BusinessObjectDataItem> maintenanceItems =
        [
            ImageItem(
                "maintenance-post.png",
                "QmMaintenancePostMain20260506",
                "https://picsum.photos/seed/candoitall-maintenance-post/900/600",
                true,
                "Maintenance image"),
            new(
                "maintenance-note.md",
                BusinessObjectDataItemType.Text,
                BusinessObjectDataItemStorage.Ipfs,
                "QmMaintenanceMarkdown20260506",
                null,
                "text/markdown",
                "maintenance-note.md",
                1190,
                false,
                "Operator note")
        ];

        IReadOnlyList<BusinessObjectDataItem> warehouseItems =
        [
            ImageItem(
                "warehouse-capacity.png",
                "QmWarehouseCapacityMain20260506",
                "https://picsum.photos/seed/candoitall-warehouse-capacity/900/600",
                true,
                "Warehouse capacity image"),
            ImageItem(
                "warehouse-loading.png",
                "QmWarehouseCapacityLoading20260506",
                "https://picsum.photos/seed/candoitall-warehouse-loading/900/600",
                false,
                "Loading area image"),
            new(
                "capacity.pdf",
                BusinessObjectDataItemType.Pdf,
                BusinessObjectDataItemStorage.Ipfs,
                "QmWarehouseCapacityPdf20260506",
                new Uri("https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf"),
                "application/pdf",
                "capacity.pdf",
                88012,
                false,
                "Capacity report"),
            new(
                "capacity-flow.mmd",
                BusinessObjectDataItemType.Mermaid,
                BusinessObjectDataItemStorage.Inline,
                """
                flowchart LR
                    intake["Inbound dock"] --> inspect["Inspection"]
                    inspect --> cold["Cold zone"]
                    inspect --> bulk["Bulk racks"]
                    bulk --> ship["Outbound staging"]
                    cold --> ship
                """,
                null,
                "text/vnd.mermaid",
                "capacity-flow.mmd",
                612,
                false,
                "Capacity movement map")
        ];

        return
        [
            new PostBusinessObjectView(
                context,
                new BusinessObjectId("bo:post:energy-balance"),
                "post",
                "1.0",
                4,
                primaryAddress,
                BusinessObjectOperation.Update,
                new HashHex("7ed0f112c0ffee9a58f399dba4e1841a101d93462d3a7b19f5032aebd9e90201"),
                new OutPoint(new TxId("tx-post-energy-balance-v4"), 0),
                null,
                viewedAt.AddDays(-3),
                viewedAt,
                "Quarterly energy balance",
                "Simulation note with an attached energy image and source data.",
                "The energy balance BO carries the narrative and the evidence needed by the upper simulation layer. Its DataItems stay IPFS-addressed while the ledger keeps ownership and version history.",
                "Operations",
                null,
                energyItems[0].Uri?.ToString(),
                ["energy", "simulation", "operations"],
                energyItems),
            new ImageBusinessObjectView(
                context,
                new BusinessObjectId("bo:image:warehouse-capacity"),
                "image",
                "1.0",
                2,
                primaryAddress,
                BusinessObjectOperation.Transfer,
                new HashHex("8bd1f112c0ffee9a58f399dba4e1841a101d93462d3a7b19f5032aebd9e90202"),
                new OutPoint(new TxId("tx-image-warehouse-capacity-v2"), 1),
                null,
                viewedAt.AddDays(-2),
                viewedAt,
                "Warehouse capacity model",
                "Visual BO with additional images and a PDF data item.",
                "The capacity image is the main artifact; supporting DataItems keep context for audit and future simulation steps.",
                "Warehouse north",
                null,
                warehouseItems[0].Uri?.ToString(),
                ["warehouse", "capacity", "model"],
                warehouseItems),
            new PostBusinessObjectView(
                context,
                new BusinessObjectId("bo:post:maintenance-window"),
                "post",
                "1.0",
                1,
                AddressForKey("maintenance-shared"),
                BusinessObjectOperation.Create,
                new HashHex("9ce2f112c0ffee9a58f399dba4e1841a101d93462d3a7b19f5032aebd9e90203"),
                new OutPoint(new TxId("tx-post-maintenance-window-v1"), 0),
                null,
                viewedAt.AddHours(-10),
                viewedAt,
                "Maintenance window",
                "Shared post BO for a planned production pause.",
                "A short operational post can still include image and text DataItems, making it render like a familiar NFT post while keeping enterprise naming.",
                "Maintenance",
                null,
                maintenanceItems[0].Uri?.ToString(),
                ["maintenance", "planning", "shared"],
                maintenanceItems)
        ];
    }

    private static BusinessObjectDataItem ImageItem(
        string fileName,
        string hash,
        string uri,
        bool isMain,
        string description)
        => new(
            fileName,
            BusinessObjectDataItemType.Image,
            BusinessObjectDataItemStorage.Ipfs,
            hash,
            new Uri(uri),
            "image/png",
            fileName,
            648_000,
            isMain,
            description);

    private static Address AddressForKey(string keyId)
        => Address.FromPublicKey(Hex.Utf8($"{keyId}-public"));

    private static string ResolvePreview(IMediaBusinessObjectView item)
        => item.DataItems.FirstOrDefault(dataItem => dataItem.IsMain && dataItem.Uri is not null)?.Uri?.ToString()
            ?? item.ImageLink
            ?? "https://picsum.photos/seed/candoitall-business-object/900/600";
}
