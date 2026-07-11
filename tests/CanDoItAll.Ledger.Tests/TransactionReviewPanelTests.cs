using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.BusinessObjects;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Tests;

public sealed class TransactionReviewPanelTests : TestContext
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

        IRenderedComponent<TransactionReviewPanel> component = RenderComponent<TransactionReviewPanel>(parameters => parameters
            .Add(item => item.Draft, draft));

        Assert.Contains("Attached main amount", component.Markup, StringComparison.Ordinal);
        Assert.Contains("3.75", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Token_transfer_context_summary_discloses_nonzero_attached_main_currency()
    {
        IRenderedComponent<TransactionComposerShell> component = RenderComponent<TransactionComposerShell>(parameters => parameters
            .Add(item => item.Draft, CreateTokenTransferDraft()));

        component.Find("[data-testid='transaction-mode-TokenTransfer']").Click();

        string summary = component.Find("[data-testid='transaction-context-rail']").TextContent;
        Assert.Contains("12 demo-token + 3.75 main", summary, StringComparison.Ordinal);
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
