using System.Text;
using CanDoItAll.Ledger.Core;
using Xunit;

namespace CanDoItAll.Ledger.Tests;

public sealed class AddressDerivationTests
{
    [Fact]
    public void Address_FromPublicKey_ReconstructsStableAddress()
    {
        byte[] publicKey = Encoding.UTF8.GetBytes("alice-public");

        Address address = Address.FromPublicKey(publicKey);

        Assert.Equal("cdia1NRTGdKVupoaKcALYmVk1Ai9zKbKKQE11fP", address.Value);
        Assert.Equal(address, Address.FromPublicKey(publicKey));
        Assert.True(address.MatchesPublicKey(publicKey));
    }

    [Fact]
    public void Address_TryParse_ValidatesDerivedAddressFormat()
    {
        Address expected = Address.FromPublicKey(Encoding.UTF8.GetBytes("alice-public"));

        Assert.True(Address.TryParse(expected.Value, out Address parsed));
        Assert.Equal(expected, parsed);
        Assert.True(Address.IsValid(expected.Value));
        Assert.False(Address.IsValid("treasury"));
        Assert.False(Address.IsValid("ops"));
        Assert.False(Address.IsValid($" {expected.Value}"));
        Assert.False(Address.IsValid(expected.Value[..^1] + (expected.Value[^1] == '1' ? '2' : '1')));
    }

    [Fact]
    public void Address_FromPublicKey_ChangesWhenPublicKeyChanges()
    {
        Address alice = Address.FromPublicKey(Encoding.UTF8.GetBytes("alice-public"));
        Address bob = Address.FromPublicKey(Encoding.UTF8.GetBytes("bob-public"));

        Assert.NotEqual(alice, bob);
    }

    [Fact]
    public void Address_FromPublicKey_RejectsEmptyPublicKeyMaterial()
    {
        Assert.Throws<ArgumentException>(() => Address.FromPublicKey(ReadOnlySpan<byte>.Empty));
        Assert.Throws<ArgumentException>(() => Address.CalculatePublicKeyHash(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Address_CalculatePublicKeyHash_IsStable()
    {
        HashHex publicKeyHash = Address.CalculatePublicKeyHash(Encoding.UTF8.GetBytes("alice-public"));

        Assert.Equal("3cc41521950d347465c08e0d6c7e5ecfb974892d3c6a284a40621844588d8414", publicKeyHash.Value);
    }
}
