namespace CanDoItAll.Economy.AccountApp.Services;

public sealed record AccountAppOptions
{
    public Uri NodeBaseUri { get; init; } = new("http://127.0.0.1:5299/", UriKind.Absolute);

    public string ApiBasePath { get; init; } = "api/v1";

    public string LedgerId { get; init; } = "default";

    public string? ScenarioId { get; init; }

    public string VaultId { get; init; } = "account-app-default";

    public string DefaultAccountName { get; init; } = "Browser account";

    public bool EnableIpfsUploads { get; init; }

    public Uri? IpfsApiBaseUri { get; init; }

    public string IpfsGatewayBaseUri { get; init; } = "http://127.0.0.1:5001/api/v0/cat?arg=";
}
