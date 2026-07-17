using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Economy.Components;
using CanDoItAll.Economy.Components.Models;
using CanDoItAll.Economy.Components.Samples;
using CanDoItAll.Economy.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CanDoItAll.Economy.Tests;

public sealed class TransactionPayloadSignaturePrimitiveTests : TestContext
{
    public TransactionPayloadSignaturePrimitiveTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void PayloadViewer_RendersBusinessObjectOutputReferencesAsDenseRows()
    {
        EconomyTransactionDetailsModel details = EconomyComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TxDataPayloadViewer> component = RenderComponent<TxDataPayloadViewer>(parameters => parameters
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
        EconomyTransactionDetailsModel details = EconomyComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TxDataPayloadViewer> component = RenderComponent<TxDataPayloadViewer>(parameters => parameters
            .Add(x => x.Outputs, details.Outputs));

        Assert.Contains("No transaction data item is attached.", component.Markup, StringComparison.Ordinal);
        Assert.Single(component.FindAll("[data-testid='cda-tx-payload-bo-output']"));
        Assert.Empty(component.FindAll("[data-testid='cda-tx-payload-bo-copy-id']"));
        Assert.Contains("Assigned after confirmation", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SignatureList_FlattensSignaturesAcrossInputsIntoDenseRows()
    {
        EconomyTransactionDetailsModel details = EconomyComponentSampleData.CreateTransactionDetails();
        SignatureEnvelope secondSignature = CreateSignature("secondary-key", 0x20);
        EconomyTransactionInputDetails secondInput = details.Inputs[0] with
        {
            InputIndex = 3,
            SignatureBundle = new SignatureBundle([secondSignature])
        };

        IRenderedComponent<TransactionSignatureList> component = RenderComponent<TransactionSignatureList>(parameters => parameters
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
        EconomyTransactionDetailsModel details = EconomyComponentSampleData.CreateTransactionDetails();
        EconomyTransactionInputDetails unsignedInput = details.Inputs[0] with
        {
            SignatureBundle = SignatureBundle.Empty
        };

        IRenderedComponent<TransactionSignatureList> component = RenderComponent<TransactionSignatureList>(parameters => parameters
            .Add(x => x.Inputs, [unsignedInput]));

        Assert.Contains("No input signatures were recorded.", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll("[data-testid='cda-tx-signature-row']"));
        Assert.Empty(component.FindComponents<EmptyState>());
    }

    [Fact]
    public void SignatureCard_PreservesTestIdAndDelegatesToReusableList()
    {
        EconomyTransactionInputDetails input = EconomyComponentSampleData.CreateTransactionDetails().Inputs[0];

        IRenderedComponent<TxSignatureCard> component = RenderComponent<TxSignatureCard>(parameters => parameters
            .Add(x => x.Input, input));

        Assert.Single(component.FindAll("[data-testid='cda-tx-signature-card']"));
        IRenderedComponent<TransactionSignatureList> list = Assert.Single(component.FindComponents<TransactionSignatureList>());
        Assert.Equal(input, Assert.Single(list.Instance.Inputs));
    }

    [Fact]
    public void TransactionInspector_UsesFlexiblePayloadAndFlattenedSignaturePrimitives()
    {
        EconomyTransactionDetailsModel details = EconomyComponentSampleData.CreateTransactionDetails();

        IRenderedComponent<TransactionInspectorDialog> component = RenderComponent<TransactionInspectorDialog>(parameters => parameters
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
