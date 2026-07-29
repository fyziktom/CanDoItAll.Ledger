using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Ledger.Components;
using CanDoItAll.Ledger.Components.Models;
using CanDoItAll.Ledger.Components.Samples;
using CanDoItAll.Ledger.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class TransactionPayloadSignaturePrimitiveTests : BunitContext
{
    public TransactionPayloadSignaturePrimitiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void PayloadViewer_RendersBusinessObjectOutputReferencesAsDenseRows()
    {
        LedgerTransactionDetailsModel details = LedgerComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TxDataPayloadViewer> component = Render<TxDataPayloadViewer>(parameters => parameters
            .Add(x => x.Data, details.Data)
            .Add(x => x.Outputs, details.Outputs)
            .Add(x => x.TransactionId, details.TxId));

        Assert.Single(component.FindAll("[data-testid='cda-tx-data-payload-viewer']"));
        Assert.Single(component.FindAll("[data-testid='cda-tx-payload-bo-outputs']"));
        Assert.Single(component.FindAll("[data-testid='cda-tx-payload-bo-output']"));
        Assert.Single(component.FindAll("[data-testid='cda-tx-payload-bo-copy-id']"));
        string expectedObjectId = BusinessObjectId.FromOutPoint(details.TxId, details.Outputs[0].OutputIndex).Value;
        Assert.Contains(
            component.FindComponents<CopyableMonoValue>(),
            value => string.Equals(value.Instance.Value, expectedObjectId, StringComparison.Ordinal));
        Assert.Empty(component.FindComponents<SectionCard>());
        Assert.Empty(component.FindComponents<MetricCard>());
    }

    [Fact]
    public void PayloadViewer_AllowsBusinessObjectReferencesWithoutTransactionData()
    {
        LedgerTransactionDetailsModel details = LedgerComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TxDataPayloadViewer> component = Render<TxDataPayloadViewer>(parameters => parameters
            .Add(x => x.Outputs, details.Outputs));

        Assert.Contains("No transaction data item is attached.", component.Markup, StringComparison.Ordinal);
        Assert.Single(component.FindAll("[data-testid='cda-tx-payload-bo-output']"));
        Assert.Empty(component.FindAll("[data-testid='cda-tx-payload-bo-copy-id']"));
        Assert.Contains("Assigned after confirmation", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SignatureList_FlattensSignaturesAcrossInputsIntoDenseRows()
    {
        LedgerTransactionDetailsModel details = LedgerComponentSampleData.CreateTransactionDetails();
        SignatureEnvelope secondSignature = CreateSignature("secondary-key", 0x20);
        LedgerTransactionInputDetails secondInput = details.Inputs[0] with
        {
            InputIndex = 3,
            SignatureBundle = new SignatureBundle([secondSignature])
        };

        IRenderedComponent<TransactionSignatureList> component = Render<TransactionSignatureList>(parameters => parameters
            .Add(x => x.Inputs, [details.Inputs[0], secondInput]));

        Assert.Equal(2, component.FindAll("[data-testid='cda-tx-signature-row']").Count);
        Assert.Contains("#0", component.Markup, StringComparison.Ordinal);
        Assert.Contains("#3", component.Markup, StringComparison.Ordinal);
        Assert.Contains("operations-primary", component.Markup, StringComparison.Ordinal);
        Assert.Contains("secondary-key", component.Markup, StringComparison.Ordinal);
        string[] visibleHashes = component.FindComponents<HashDisplay>()
            .Select(hash => hash.Instance.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        Assert.Contains(details.Inputs[0].SignatureBundle.Signatures[0].PublicKeyHash.Value, visibleHashes);
        Assert.Contains(details.Inputs[0].SignaturePayloadHash!.Value.Value, visibleHashes);
        Assert.Contains(details.Inputs[0].OutPointKey!.Value.Value.Value, visibleHashes);
        Assert.Empty(component.FindComponents<SectionCard>());
        Assert.Empty(component.FindComponents<MetricCard>());
    }

    [Fact]
    public void SignatureList_RendersCompactEmptyStateWhenNoSignaturesExist()
    {
        LedgerTransactionDetailsModel details = LedgerComponentSampleData.CreateTransactionDetails();
        LedgerTransactionInputDetails unsignedInput = details.Inputs[0] with
        {
            SignatureBundle = SignatureBundle.Empty
        };

        IRenderedComponent<TransactionSignatureList> component = Render<TransactionSignatureList>(parameters => parameters
            .Add(x => x.Inputs, [unsignedInput]));

        Assert.Contains("No input signatures were recorded.", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll("[data-testid='cda-tx-signature-row']"));
        Assert.Empty(component.FindComponents<EmptyState>());
    }

    [Fact]
    public void SignatureCard_PreservesTestIdAndDelegatesToReusableList()
    {
        LedgerTransactionInputDetails input = LedgerComponentSampleData.CreateTransactionDetails().Inputs[0];

        IRenderedComponent<TxSignatureCard> component = Render<TxSignatureCard>(parameters => parameters
            .Add(x => x.Input, input));

        Assert.Single(component.FindAll("[data-testid='cda-tx-signature-card']"));
        IRenderedComponent<TransactionSignatureList> list = Assert.Single(component.FindComponents<TransactionSignatureList>());
        Assert.Equal(input, Assert.Single(list.Instance.Inputs));
    }

    [Fact]
    public void TransactionInspector_UsesFlexiblePayloadAndFlattenedSignaturePrimitives()
    {
        LedgerTransactionDetailsModel details = LedgerComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TransactionInspectorDialog> component = Render<TransactionInspectorDialog>(parameters => parameters
            .Add(x => x.Details, details));

        IRenderedComponent<TxDataPayloadViewer> payload = Assert.Single(component.FindComponents<TxDataPayloadViewer>());
        Assert.Equal(details.TxId, payload.Instance.TransactionId);
        Assert.Same(details.Outputs, payload.Instance.Outputs);
        IRenderedComponent<TransactionSignatureList> signatures = Assert.Single(component.FindComponents<TransactionSignatureList>());
        Assert.Same(details.Inputs, signatures.Instance.Inputs);
        Assert.Empty(component.FindComponents<TxSignatureCard>());
    }

    private static SignatureEnvelope CreateSignature(string keyId, byte seed)
        => new(
            SignatureAlgorithmId.EcdsaP256Sha256,
            keyId,
            new HashHex(new string('b', 64)),
            new HashHex(new string('a', 64)),
            [seed, (byte)(seed + 1)],
            [(byte)(seed + 2), (byte)(seed + 3)]);
}
