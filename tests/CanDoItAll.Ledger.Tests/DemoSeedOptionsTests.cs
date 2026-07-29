namespace CanDoItAll.Ledger.Tests;

public sealed class DemoSeedOptionsTests
{
    private const string VerificationEnvironmentVariable =
        "CANDOITALL_LEDGER_VERIFY_PERMISSION_TOKEN_BO_FLOW";

    [Fact]
    public void Parse_LeavesLiveVerificationDisabledByDefault()
    {
        WithVerificationEnvironmentVariable(null, () =>
        {
            DemoSeedOptions options = DemoSeedOptions.Parse([]);

            Assert.False(options.VerifyPermissionTokenBusinessObjectFlow);
        });
    }

    [Fact]
    public void Parse_EnablesLiveVerificationOnlyForExplicitSwitchOrEnvironmentSetting()
    {
        WithVerificationEnvironmentVariable(null, () =>
        {
            DemoSeedOptions switchOptions = DemoSeedOptions.Parse(["--verify-permission-token-bo-flow"]);

            Assert.True(switchOptions.VerifyPermissionTokenBusinessObjectFlow);
        });

        WithVerificationEnvironmentVariable("true", () =>
        {
            DemoSeedOptions environmentOptions = DemoSeedOptions.Parse([]);

            Assert.True(environmentOptions.VerifyPermissionTokenBusinessObjectFlow);
        });
    }

    private static void WithVerificationEnvironmentVariable(string? value, Action assertion)
    {
        string? previous = Environment.GetEnvironmentVariable(VerificationEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(VerificationEnvironmentVariable, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(VerificationEnvironmentVariable, previous);
        }
    }
}
