namespace CanDoItAll.Economy.AccountApp.Services;

public sealed record AccountAppOptions
{
    public Uri NodeBaseUri { get; init; } = new("http://127.0.0.1:5299/", UriKind.Absolute);

    public string ApiBasePath { get; init; } = "api/v1";

    public string LedgerId { get; init; } = "account-app-ledger";

    public string? ScenarioId { get; init; } = "browser-demo";

    public string VaultId { get; init; } = "account-app-default";

    public string DefaultAccountName { get; init; } = "Browser account";
}
