using Bunit;
using AngleSharp.Dom;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Economy.Tests;

public sealed class TransactionFlowFoundationTests : TestContext
{
    private static readonly Address Alice = Address.FromPublicKey([1, 2, 3, 4]);
    private static readonly Address Bob = Address.FromPublicKey([5, 6, 7, 8]);

    public TransactionFlowFoundationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Catalog_covers_every_transaction_mode_once()
    {
        BusinessObjectTransactionMode[] modes = Enum.GetValues<BusinessObjectTransactionMode>();

        Assert.Equal(modes.Length, TransactionFlowCatalog.All.Count);
        Assert.Equal(modes.Order(), TransactionFlowCatalog.All.Select(item => item.Mode).Order());
    }

    [Fact]
    public void Catalog_delegates_rule_token_creation_to_the_specialized_flow()
    {
        TransactionFlowDefinition definition = TransactionFlowCatalog.Get(BusinessObjectTransactionMode.CreateRuleToken);

        Assert.Equal(TransactionFlowDisposition.Delegated, definition.Disposition);
        Assert.Equal(TransactionFlowFamily.Token, definition.Family);
    }

    [Fact]
    public void Flow_chooser_changes_family_without_committing_until_an_operation_is_selected()
    {
        BusinessObjectTransactionMode? selected = null;
        IRenderedComponent<TransactionFlowChooser> component = RenderComponent<TransactionFlowChooser>(parameters => parameters
            .Add(item => item.SelectedMode, BusinessObjectTransactionMode.CloneObject)
            .Add(item => item.SelectedModeChanged, EventCallback.Factory.Create<BusinessObjectTransactionMode>(this, mode => selected = mode)));

        IElement payments = component.FindAll("button")
            .Single(button => button.TextContent.Contains("Payments", StringComparison.Ordinal));
        payments.Click();

        Assert.Null(selected);

        component.Find("[data-testid='transaction-mode-MainPayment']").Click();

        Assert.Equal(BusinessObjectTransactionMode.MainPayment, selected);
    }

    [Fact]
    public void Validator_rejects_incomplete_payment_and_accepts_a_funded_payment()
    {
        BusinessObjectTransactionDraft incomplete = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment,
            PaymentReceiverAddress = default,
            PaymentAmount = Amount.Zero
        };

        TransactionFlowValidation invalid = TransactionDraftValidator.Validate(
            incomplete,
            new TransactionDraftValidationContext(Balance: CreateBalance(25m)));
        TransactionFlowValidation valid = TransactionDraftValidator.Validate(
            incomplete with { PaymentReceiverAddress = Bob, PaymentAmount = Amount.FromInt64(10) },
            new TransactionDraftValidationContext(Balance: CreateBalance(25m)));

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.Code == "receiver.required");
        Assert.Contains(invalid.Errors, error => error.Code == "amount.positive");
        Assert.True(valid.IsValid);
    }

    [Fact]
    public void Validator_rejects_merge_with_fewer_than_two_plain_inputs()
    {
        OutPoint selected = new(new TxId("plain-1"), 0);
        LedgerSpendableOutput output = CreatePlainOutput(selected, 10m);
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.UtxoMerge,
            SelectedUtxos = [selected],
            PaymentReceiverAddress = Alice
        };

        TransactionFlowValidation validation = TransactionDraftValidator.Validate(
            draft,
            new TransactionDraftValidationContext(SpendableOutputs: [output]));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == "utxo.merge.minimum");
    }

    [Fact]
    public void Validator_rejects_owned_object_operation_until_an_available_object_is_selected()
    {
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.TransferObject
        };

        TransactionFlowValidation validation = TransactionDraftValidator.Validate(
            draft,
            new TransactionDraftValidationContext(BusinessObjects: []));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == "object.unavailable");
    }

    [Fact]
    public void Validator_rejects_merge_for_incompatible_object_types()
    {
        IBusinessObjectView first = CreateBusinessObject("bo:order:1", "purchase-order");
        IBusinessObjectView second = CreateBusinessObject("bo:ticket:1", "ticket");
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MergeObjects,
            SourceObjectId = BusinessObjectId.FromOutPoint(first.CurrentOutPoint),
            SecondObjectId = BusinessObjectId.FromOutPoint(second.CurrentOutPoint)
        };

        TransactionFlowValidation validation = TransactionDraftValidator.Validate(
            draft,
            new TransactionDraftValidationContext(BusinessObjects: [first, second]));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Code == "object.merge.compatibility");
    }

    [Fact]
    public void Composer_disables_submit_and_explains_invalid_draft()
    {
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment,
            PaymentReceiverAddress = default,
            PaymentAmount = Amount.Zero
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.Balance, CreateBalance(25m)));

        component.Find("[data-testid='transaction-mode-MainPayment']").Click();
        component.SetParametersAndRender(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.Balance, CreateBalance(25m)));

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
        Assert.Contains("receiver", component.Find("[data-testid='transaction-validation']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(component.FindAll("[data-testid='transaction-type-step']"));
        Assert.Contains("Selected Payment transaction", component.Find("[data-testid='transaction-config-header']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Change transaction type", component.Find("[data-testid='transaction-config-header']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Single(component.FindAll("[data-testid='transaction-workbench']"));
        Assert.Single(component.FindAll("[data-testid='transaction-context-rail']"));
        Assert.NotNull(component.Find("[data-testid='transaction-context-rail'] [data-testid='transaction-submit']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-workbench'] main [data-testid='transaction-submit']"));
        Assert.Contains("Payment", component.Find("[data-testid='transaction-context-rail']").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("ModeLabel", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewReceiver", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewAmount", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Composer_disables_owned_object_submit_until_selection_is_available()
    {
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.TransferObject
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.BusinessObjects, Array.Empty<IBusinessObjectView>()));

        component.Find("[data-testid='transaction-mode-TransferObject']").Click();

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
        Assert.Contains("available in this account", component.Find("[data-testid='transaction-validation']").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Composer_keeps_submit_disabled_until_create_wizard_review_step()
    {
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.Create
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft));

        await component.Find("[data-testid='transaction-mode-Create']").ClickAsync(new MouseEventArgs());

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
        Assert.Contains("Review required", component.Find("[data-testid='transaction-context-rail']").TextContent, StringComparison.OrdinalIgnoreCase);

        await component.Find("[data-testid='bo-create-next']").ClickAsync(new MouseEventArgs());

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));

        await component.Find("[data-testid='bo-create-next']").ClickAsync(new MouseEventArgs());

        Assert.False(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
        Assert.Contains("Ready", component.Find("[data-testid='transaction-context-rail']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Review new object", component.Find("[data-testid='cda-transaction-review-panel']").TextContent, StringComparison.OrdinalIgnoreCase);

        await component.Find("[data-testid='bo-create-back']").ClickAsync(new MouseEventArgs());

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Composer_keeps_direct_payment_submit_disabled_until_review()
    {
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment,
            PaymentReceiverAddress = Bob,
            PaymentAmount = Amount.FromInt64(10)
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.Balance, CreateBalance(25m)));

        await component.Find("[data-testid='transaction-mode-MainPayment']").ClickAsync(new MouseEventArgs());

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
        Assert.Single(component.FindAll("[data-testid='transaction-details-review-wizard']"));

        await component.Find("[data-testid='transaction-details-next']").ClickAsync(new MouseEventArgs());

        Assert.False(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));

        await component.Find("[data-testid='transaction-details-back']").ClickAsync(new MouseEventArgs());

        Assert.True(component.Find("[data-testid='transaction-submit']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Token_creator_exposes_submit_only_on_review_step()
    {
        var permissionSource = new OutPoint(new TxId(new string('a', 64)), 0);
        EconomyTokenCreationAuthority authority = new(
            "demo",
            BusinessObjectId.FromOutPoint(permissionSource),
            Alice,
            permissionSource,
            Amount.FromInt64(1),
            Repeatable: true);
        EconomyTokenCreationRequest? submitted = null;
        IRenderedComponent<TokenCreationDialogContent> component = RenderComponent<TokenCreationDialogContent>(parameters => parameters
            .Add(item => item.Authorities, new[] { authority })
            .Add(
                item => item.SubmitRequested,
                EventCallback.Factory.Create<EconomyTokenCreationRequest>(this, request => submitted = request)));

        Assert.Empty(component.FindAll("[data-testid='token-creation-submit']"));

        await component.Find("[data-testid='token-creation-next']").ClickAsync(new MouseEventArgs());
        Assert.Empty(component.FindAll("[data-testid='token-creation-submit']"));

        await component.Find("[data-testid='token-creation-next']").ClickAsync(new MouseEventArgs());

        Assert.Single(component.FindAll("[data-testid='token-creation-submit']"));
        Assert.Contains("demo.voucher", component.Markup, StringComparison.OrdinalIgnoreCase);

        component.Find("[data-testid='token-creation-submit']").Click();

        Assert.NotNull(submitted);
        Assert.Equal(permissionSource, submitted.PermissionSourceOutPoint);
    }

    [Fact]
    public void Token_transfer_explains_when_no_transferable_balance_exists()
    {
        IRenderedComponent<TransactionTokenTransferForm> component = RenderComponent<TransactionTokenTransferForm>(parameters => parameters
            .Add(item => item.Draft, CreateDraft() with { SelectedMode = BusinessObjectTransactionMode.TokenTransfer })
            .Add(item => item.Options, Array.Empty<EconomyTokenOption>()));

        Assert.Contains("No transferable token balance", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(component.FindAll("[data-testid='composer-token-selector']"));
    }

    [Fact]
    public void Review_panel_uses_mode_specific_facts_without_irrelevant_empty_fields()
    {
        BusinessObjectTransactionDraft payment = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment,
            PaymentReceiverAddress = Bob,
            PaymentAmount = Amount.FromInt64(9)
        };

        IRenderedComponent<TransactionReviewPanel> component = RenderComponent<TransactionReviewPanel>(parameters => parameters
            .Add(item => item.Draft, payment));

        Assert.Contains("Review payment", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Bob.Value, component.Markup, StringComparison.Ordinal);
        Assert.Contains("9", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Owner", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Object ID", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(BusinessObjectTransactionMode.Create, "bo-create-wizard")]
    [InlineData(BusinessObjectTransactionMode.TransferObject, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.UpdateObject, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.CloseObject, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.CloneObject, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.MergeObjects, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.SplitBusinessObjectPayment, "bo-operation-wizard")]
    [InlineData(BusinessObjectTransactionMode.MainPayment, "payment-transaction-form")]
    [InlineData(BusinessObjectTransactionMode.TokenTransfer, "token-transfer-form")]
    [InlineData(BusinessObjectTransactionMode.CreateRuleToken, "transaction-open-token-creator")]
    [InlineData(BusinessObjectTransactionMode.UtxoMerge, "utxo-merge-form")]
    [InlineData(BusinessObjectTransactionMode.UtxoSplit, "utxo-split-form")]
    public async Task Composer_renders_each_catalog_mode_in_the_shared_workbench(
        BusinessObjectTransactionMode mode,
        string expectedTestId)
    {
        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, CreateDraft() with { SelectedMode = mode }));

        await component.Find($"[data-testid='transaction-mode-{mode}']").ClickAsync(new MouseEventArgs());

        Assert.Single(component.FindAll("[data-testid='transaction-workbench']"));
        Assert.Single(component.FindAll("[data-testid='transaction-context-rail']"));
        Assert.Single(component.FindAll($"[data-testid='{expectedTestId}']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-type-step']"));

        if (mode == BusinessObjectTransactionMode.CreateRuleToken)
        {
            Assert.Empty(component.FindAll("[data-testid='transaction-submit']"));
        }
        else
        {
            Assert.Single(component.FindAll("[data-testid='transaction-submit']"));
        }
    }

    [Fact]
    public async Task Composer_change_type_removes_workbench_and_restores_only_selector()
    {
        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, CreateDraft() with { SelectedMode = BusinessObjectTransactionMode.Create }));

        await component.Find("[data-testid='transaction-mode-Create']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-testid='transaction-change-mode']").ClickAsync(new MouseEventArgs());

        Assert.Single(component.FindAll("[data-testid='transaction-type-step']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-workbench']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-context-rail']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-submit']"));
    }

    [Fact]
    public async Task Composer_change_type_clears_contextual_source_and_allocation_state()
    {
        OutPoint staleOutPoint = new(new TxId("stale-utxo"), 0);
        BusinessObjectTransactionDraft? captured = null;
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.CloneObject,
            SourceObjectId = new BusinessObjectId("bo:stale:source"),
            SecondObjectId = new BusinessObjectId("bo:stale:second"),
            ExternalRef = "clone://stale-source",
            SecondaryPaymentAmount = Amount.FromInt64(2),
            SelectedUtxos = [staleOutPoint],
            UtxoSplitOutputs = [new UtxoSplitOutput(Bob, Amount.FromInt64(1))]
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.DraftChanged, next => captured = next));

        await component.Find("[data-testid='transaction-mode-CloneObject']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-testid='transaction-change-mode']").ClickAsync(new MouseEventArgs());
        await component.Find("[data-testid='transaction-mode-SplitBusinessObjectPayment']").ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(BusinessObjectTransactionMode.SplitBusinessObjectPayment, captured!.SelectedMode);
        Assert.Null(captured.SourceObjectId);
        Assert.Null(captured.SecondObjectId);
        Assert.Equal(string.Empty, captured.ExternalRef);
        Assert.Null(captured.SecondaryPaymentAmount);
        Assert.Empty(captured.SelectedUtxosOrEmpty);
        Assert.Empty(captured.UtxoSplitOutputsOrEmpty);
    }

    [Fact]
    public async Task Composer_change_type_restores_seed_values_and_drops_hidden_assets()
    {
        TokenId seedToken = new("seed-token");
        Address staleReceiver = Address.FromPublicKey([9, 10, 11, 12]);
        BusinessObjectTransactionDraft? captured = null;
        EconomyTokenOption tokenOption = new(
            seedToken.Value,
            "Seed token",
            seedToken,
            Amount.FromInt64(100),
            IsMain: false);
        BusinessObjectTransactionDraft baseline = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.TokenTransfer,
            TokenId = seedToken,
            TokenAmount = Amount.FromInt64(1)
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, baseline)
            .Add(item => item.TokenOptions, new[] { tokenOption })
            .Add(item => item.DraftChanged, next => captured = next));

        await component.Find("[data-testid='transaction-mode-TokenTransfer']").ClickAsync(new MouseEventArgs());
        component.Find("[data-testid='composer-token-main-amount']").Input("7");
        component.Find("[data-testid='composer-token-receiver']").Input(staleReceiver.Value);
        component.Find("[data-testid='composer-token-amount']").Input("11");
        component.Find("[data-testid='composer-token-memo']").Input("stale token memo");
        await component.Find("[data-testid='transaction-change-mode']").ClickAsync(new MouseEventArgs());
        component.FindAll("button")
            .Single(button => button.TextContent.Contains("Business objects", StringComparison.Ordinal))
            .Click();
        await component.Find("[data-testid='transaction-mode-Create']").ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(BusinessObjectTransactionMode.Create, captured!.SelectedMode);
        Assert.Equal(baseline.ObjectId, captured.ObjectId);
        Assert.Equal(baseline.ObjectType, captured.ObjectType);
        Assert.Equal(baseline.SchemaVersion, captured.SchemaVersion);
        Assert.Equal(baseline.StateJson, captured.StateJson);
        Assert.Equal(baseline.ReceiverAddress, captured.ReceiverAddress);
        Assert.Equal(baseline.PaymentReceiverAddress, captured.PaymentReceiverAddress);
        Assert.Equal(baseline.MainAmount, captured.MainAmount);
        Assert.Equal(baseline.PaymentAmount, captured.PaymentAmount);
        Assert.Equal(seedToken, captured.TokenId);
        Assert.Equal(Amount.FromInt64(1), captured.TokenAmount);
        Assert.Equal(baseline.ExternalRef, captured.ExternalRef);

        await component.Find("[data-testid='transaction-change-mode']").ClickAsync(new MouseEventArgs());
        component.FindAll("button")
            .Single(button => button.TextContent.Contains("Payments", StringComparison.Ordinal))
            .Click();
        await component.Find("[data-testid='transaction-mode-MainPayment']").ClickAsync(new MouseEventArgs());
        component.Find("[data-testid='cda-send-payment-receiver']").Input(staleReceiver.Value);
        component.Find("[data-testid='cda-send-payment-amount']").Input("13");
        component.Find("[data-testid='cda-send-payment-memo']").Input("stale payment memo");
        await component.Find("[data-testid='transaction-change-mode']").ClickAsync(new MouseEventArgs());
        component.FindAll("button")
            .Single(button => button.TextContent.Contains("Tokens", StringComparison.Ordinal))
            .Click();
        await component.Find("[data-testid='transaction-mode-TokenTransfer']").ClickAsync(new MouseEventArgs());

        Assert.Equal(BusinessObjectTransactionMode.TokenTransfer, captured!.SelectedMode);
        Assert.Equal(baseline.PaymentReceiverAddress, captured.PaymentReceiverAddress);
        Assert.Equal(baseline.PaymentAmount, captured.PaymentAmount);
        Assert.Equal(seedToken, captured.TokenId);
        Assert.Equal(Amount.FromInt64(1), captured.TokenAmount);
        Assert.Equal(string.Empty, captured.ExternalRef);
    }

    [Fact]
    public async Task Composer_genuine_draft_replacement_resets_workflow_and_becomes_the_new_seed()
    {
        Address replacementOwner = Address.FromPublicKey([13, 14, 15, 16]);
        BusinessObjectTransactionDraft? captured = null;
        BusinessObjectTransactionDraft initial = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.MainPayment
        };
        BusinessObjectTransactionDraft replacement = initial with
        {
            ObjectId = new BusinessObjectId("bo:replacement:1"),
            OwnerAddress = replacementOwner,
            ReceiverAddress = replacementOwner,
            PaymentReceiverAddress = replacementOwner,
            PaymentAmount = Amount.FromInt64(22),
            ExternalRef = "replacement://seed"
        };

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, initial)
            .Add(item => item.DraftChanged, next => captured = next));

        await component.Find("[data-testid='transaction-mode-MainPayment']").ClickAsync(new MouseEventArgs());
        Assert.Single(component.FindAll("[data-testid='transaction-workbench']"));

        component.SetParametersAndRender(parameters => parameters
            .Add(item => item.Draft, replacement)
            .Add(item => item.DraftChanged, next => captured = next));

        Assert.Single(component.FindAll("[data-testid='transaction-type-step']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-workbench']"));
        Assert.Empty(component.FindAll("[data-testid='transaction-submit']"));

        component.FindAll("button")
            .Single(button => button.TextContent.Contains("Business objects", StringComparison.Ordinal))
            .Click();
        await component.Find("[data-testid='transaction-mode-Create']").ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(BusinessObjectTransactionMode.Create, captured!.SelectedMode);
        Assert.Equal(replacement.ObjectId, captured.ObjectId);
        Assert.Equal(replacement.OwnerAddress, captured.OwnerAddress);
        Assert.Equal(replacement.PaymentReceiverAddress, captured.PaymentReceiverAddress);
        Assert.Equal(replacement.PaymentAmount, captured.PaymentAmount);
        Assert.Equal(replacement.ExternalRef, captured.ExternalRef);
    }

    [Fact]
    public async Task Composer_token_transfer_entry_selects_the_first_spendable_token_in_the_draft()
    {
        TokenId tokenId = new("available-token");
        BusinessObjectTransactionDraft? captured = null;
        BusinessObjectTransactionDraft draft = CreateDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.TokenTransfer,
            TokenId = null,
            TokenAmount = null
        };
        EconomyTokenOption option = new(
            tokenId.Value,
            "Available token",
            tokenId,
            Amount.FromInt64(5),
            IsMain: false);

        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, draft)
            .Add(item => item.TokenOptions, new[] { option })
            .Add(item => item.DraftChanged, next => captured = next));

        await component.Find("[data-testid='transaction-mode-TokenTransfer']").ClickAsync(new MouseEventArgs());

        Assert.NotNull(captured);
        Assert.Equal(tokenId, captured!.TokenId);
        Assert.Equal(Amount.FromInt64(1), captured.TokenAmount);
        Assert.Contains(tokenId.Value, component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Utxo_selection_exposes_an_accessible_name_for_each_outpoint()
    {
        OutPoint outPoint = new(new TxId("plain-accessible"), 2);
        LedgerSpendableOutput output = CreatePlainOutput(outPoint, 5m);

        IRenderedComponent<UtxoSelectionList> component = RenderComponent<UtxoSelectionList>(parameters => parameters
            .Add(item => item.Outputs, new[] { output }));

        Assert.Contains($"Select UTXO {outPoint}", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Utxo_picker_drops_non_plain_initial_selections_before_enabling_confirm()
    {
        LedgerSpendableOutput plain = CreatePlainOutput(new OutPoint(new TxId("plain"), 0), 5m);
        LedgerSpendableOutput token = CreateTokenOutput(new OutPoint(new TxId("token"), 1));

        IRenderedComponent<UtxoPickerDialogContent> component = RenderComponent<UtxoPickerDialogContent>(parameters => parameters
            .Add(item => item.Outputs, new[] { plain, token })
            .Add(item => item.InitialSelected, new[] { plain.OutPoint, token.OutPoint })
            .Add(item => item.MinimumSelections, 2));

        Assert.True(component.Find("[data-testid='utxo-picker-confirm']").HasAttribute("disabled"));
        Assert.Contains("1 selected", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Utxo_selection_callback_excludes_ineligible_stale_outpoints()
    {
        LedgerSpendableOutput plain = CreatePlainOutput(new OutPoint(new TxId("plain"), 0), 5m);
        LedgerSpendableOutput token = CreateTokenOutput(new OutPoint(new TxId("token"), 1));
        IReadOnlyList<OutPoint>? captured = null;
        IRenderedComponent<UtxoSelectionList> component = RenderComponent<UtxoSelectionList>(parameters => parameters
            .Add(item => item.Outputs, new[] { plain, token })
            .Add(item => item.SelectedOutPoints, new[] { token.OutPoint })
            .Add(item => item.SelectedOutPointsChanged, EventCallback.Factory.Create<IReadOnlyList<OutPoint>>(this, value => captured = value)));

        component.Find("input[type='checkbox']").Change(true);

        Assert.NotNull(captured);
        Assert.Equal(plain.OutPoint, Assert.Single(captured!));
    }

    [Fact]
    public void Business_object_selection_grid_renders_a_compact_card_and_reports_selection()
    {
        IBusinessObjectView item = CreateBusinessObject("bo:order:1", "purchase-order");
        IReadOnlyList<IBusinessObjectView>? selected = null;
        IRenderedComponent<BusinessObjectSelectionGrid> component = RenderComponent<BusinessObjectSelectionGrid>(parameters => parameters
            .Add(value => value.Items, new[] { item })
            .Add(value => value.SelectedChanged, EventCallback.Factory.Create<IReadOnlyList<IBusinessObjectView>>(this, value => selected = value)));

        IElement card = component.Find($"[data-testid='{SelectionTestId(item)}']");
        Assert.Contains("purchase-order", card.TextContent, StringComparison.Ordinal);
        Assert.Contains("v1", card.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("v@Item.Version", card.TextContent, StringComparison.Ordinal);

        card.QuerySelector("button")!.Click();

        Assert.NotNull(selected);
        Assert.Equal(item.CurrentOutPoint, Assert.Single(selected).CurrentOutPoint);
    }

    [Fact]
    public void Business_object_selection_grid_searches_across_large_picker_metadata()
    {
        IBusinessObjectView order = CreateBusinessObject("bo:order:1", "purchase-order");
        IBusinessObjectView ticket = CreateBusinessObject("bo:ticket:1", "ticket");
        IRenderedComponent<BusinessObjectSelectionGrid> component = RenderComponent<BusinessObjectSelectionGrid>(parameters => parameters
            .Add(value => value.Items, new[] { order, ticket }));

        component.Find("[data-testid='bo-selection-filter']").Input("ticket");

        IReadOnlyList<IElement> cards = component.FindAll("[data-testid^='bo-select-']");
        Assert.Single(cards);
        Assert.Contains(ticket.CurrentOutPoint.ToString(), cards[0].TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Business_object_merge_picker_keeps_incompatible_objects_visible_but_disabled()
    {
        IBusinessObjectView order = CreateBusinessObject("bo:order:1", "purchase-order");
        IBusinessObjectView ticket = CreateBusinessObject("bo:ticket:1", "ticket");
        IRenderedComponent<BusinessObjectPickerDialogContent> component = RenderComponent<BusinessObjectPickerDialogContent>(parameters => parameters
            .Add(value => value.Items, new[] { order, ticket })
            .Add(value => value.MinimumSelections, 2)
            .Add(value => value.MaximumSelections, 2)
            .Add(value => value.RequireCompatibleSelection, true));

        component.Find($"[data-testid='{SelectionTestId(order)}'] button").Click();

        IElement incompatible = component.Find($"[data-testid='{SelectionTestId(ticket)}']");
        Assert.True(incompatible.QuerySelector("button")!.HasAttribute("disabled"));
        Assert.Contains("Different object type or schema", incompatible.TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.True(component.Find("[data-testid='bo-picker-confirm']").HasAttribute("disabled"));
    }

    [Fact]
    public void Utxo_selection_list_searches_without_changing_the_current_selection()
    {
        LedgerSpendableOutput first = CreatePlainOutput(new OutPoint(new TxId("tx-first"), 0), 4m);
        LedgerSpendableOutput second = CreatePlainOutput(new OutPoint(new TxId("tx-second"), 1), 6m);
        IRenderedComponent<UtxoSelectionList> component = RenderComponent<UtxoSelectionList>(parameters => parameters
            .Add(value => value.Outputs, new[] { first, second })
            .Add(value => value.SelectedOutPoints, new[] { first.OutPoint }));

        component.Find("[data-testid='cda-utxo-selector-filter']").Input("tx-second");

        IReadOnlyList<IElement> rows = component.FindAll("[data-testid='cda-utxo-selector-row']");
        Assert.Single(rows);
        Assert.Contains("tx-second", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("1 selected", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Business_object_lookup_panel_loads_a_direct_reference_before_selection()
    {
        IBusinessObjectView item = CreateBusinessObject("bo:order:1", "purchase-order");
        BusinessObjectLookupRequest? request = null;
        IReadOnlyList<IBusinessObjectView>? loaded = null;
        Func<BusinessObjectLookupRequest, Task<IReadOnlyList<IBusinessObjectView>>> lookup = value =>
        {
            request = value;
            return Task.FromResult<IReadOnlyList<IBusinessObjectView>>([item]);
        };
        IRenderedComponent<BusinessObjectLookupPanel> component = RenderComponent<BusinessObjectLookupPanel>(parameters => parameters
            .Add(value => value.AllowExternalSources, true)
            .Add(value => value.LookupAsync, lookup)
            .Add(value => value.ResultsLoaded, EventCallback.Factory.Create<IReadOnlyList<IBusinessObjectView>>(this, value => loaded = value)));

        component.Find("[data-testid='bo-source-direct']").Click();
        component.Find("[data-testid='bo-source-query']").Input("hash-bo-order-1");
        component.Find("[data-testid='bo-source-load']").Click();

        Assert.NotNull(request);
        Assert.Equal(BusinessObjectSourceMode.DirectReference, request!.SourceMode);
        Assert.Equal("hash-bo-order-1", request.Query);
        Assert.Equal(item.ObjectId, Assert.Single(loaded!).ObjectId);
    }

    private static BusinessObjectTransactionDraft CreateDraft()
        => new(
            new BusinessObjectId("bo:test:1"),
            "purchase-order",
            "1.0",
            Alice,
            Bob,
            Bob,
            Amount.Zero,
            Amount.FromInt64(1),
            "{}",
            "test://transaction");

    private static LedgerAccountBalance CreateBalance(decimal amount)
        => new(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            Alice,
            Amount.FromDecimal(amount),
            new Dictionary<TokenId, Amount>(),
            1,
            DateTimeOffset.UtcNow);

    private static LedgerSpendableOutput CreatePlainOutput(OutPoint outPoint, decimal amount)
        => new(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new OutPointKey(new HashHex($"key-{outPoint}")),
            outPoint,
            Alice,
            Amount.FromDecimal(amount),
            null,
            null,
            DateTimeOffset.UtcNow);

    private static LedgerSpendableOutput CreateTokenOutput(OutPoint outPoint)
        => new(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new OutPointKey(new HashHex($"key-{outPoint}")),
            outPoint,
            Alice,
            Amount.Zero,
            new TokenTransfer(new TokenId("TEST-TOKEN"), Amount.FromInt64(1)),
            null,
            DateTimeOffset.UtcNow);

    private static IBusinessObjectView CreateBusinessObject(string objectId, string objectType)
        => new GenericBusinessObjectView(
            new LedgerAccountContext(new LedgerId("test-ledger")),
            new BusinessObjectId(objectId),
            objectType,
            "1.0",
            1,
            Alice,
            BusinessObjectOperation.Create,
            new HashHex($"hash-{objectId}"),
            new OutPoint(new TxId($"tx-{objectId}"), 0),
            "{}",
            null,
            TransactionDataItemType.BusinessObject,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static string SelectionTestId(IBusinessObjectView item)
        => $"bo-select-{new string(item.CurrentOutPoint.ToString().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray())}";
}
