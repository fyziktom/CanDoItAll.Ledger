using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Tests;

public sealed class TransactionReviewPanelTests : BunitContext
{
    public TransactionReviewPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Token_transfer_review_discloses_attached_main_currency()
    {
        BusinessObjectTransactionDraft draft = CreateTokenTransferDraft();

        IRenderedComponent<TransactionReviewPanel> component = Render<TransactionReviewPanel>(parameters => parameters
            .Add(item => item.Draft, draft));

        Assert.Contains("Attached main amount", component.Markup, StringComparison.Ordinal);
        Assert.Contains("3.75", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Token_transfer_context_summary_discloses_nonzero_attached_main_currency()
    {
        IRenderedComponent<TransactionComposerShell> component = Render<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, CreateTokenTransferDraft()));

        component.Find("[data-testid='transaction-mode-TokenTransfer']").Click();

        string summary = component.Find("[data-testid='transaction-context-rail']").TextContent;
        Assert.Contains("12 demo-token + 3.75 main", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BusinessObjectTransactionMode.Create)]
    [InlineData(BusinessObjectTransactionMode.CloneObject)]
    [InlineData(BusinessObjectTransactionMode.MergeObjects)]
    public void New_business_object_reviews_disclose_that_identity_is_confirmation_assigned(BusinessObjectTransactionMode mode)
    {
        BusinessObjectTransactionDraft draft = CreateTokenTransferDraft() with
        {
            SelectedMode = mode,
            SourceObjectId = new BusinessObjectId("source-transaction:0"),
            SecondObjectId = new BusinessObjectId("second-transaction:0")
        };

        IRenderedComponent<TransactionReviewPanel> component = Render<TransactionReviewPanel>(parameters => parameters
            .Add(item => item.Draft, draft));

        Assert.Contains("Assigned after confirmation", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(draft.ObjectId.Value, component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clone_review_names_the_canonical_protocol_source_separately_from_external_reference()
    {
        var sourceId = new BusinessObjectId($"{new string('a', 64)}:0");
        BusinessObjectTransactionDraft draft = CreateTokenTransferDraft() with
        {
            SelectedMode = BusinessObjectTransactionMode.CloneObject,
            SourceObjectId = sourceId,
            ExternalRef = string.Empty
        };

        IRenderedComponent<TransactionReviewPanel> component = Render<TransactionReviewPanel>(parameters => parameters
            .Add(item => item.Draft, draft));

        Assert.Contains("Clone-source reference", component.Markup, StringComparison.Ordinal);
        Assert.Contains(sourceId.Value, component.Markup, StringComparison.Ordinal);
        Assert.Contains("External reference", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Not provided", component.Markup, StringComparison.Ordinal);
    }

    private static BusinessObjectTransactionDraft CreateTokenTransferDraft()
    {
        Address sender = Address.FromPublicKey([1, 2, 3, 4]);
        Address receiver = Address.FromPublicKey([5, 6, 7, 8]);
        return new BusinessObjectTransactionDraft(
            new BusinessObjectId("bo:test:token-transfer"),
            "test",
            "1.0",
            sender,
            receiver,
            receiver,
            Amount.FromDecimal(3.75m),
            Amount.Zero,
            "{}",
            "test://token-transfer",
            SelectedMode: BusinessObjectTransactionMode.TokenTransfer,
            TokenId: new TokenId("demo-token"),
            TokenAmount: Amount.FromInt64(12));
    }
}
