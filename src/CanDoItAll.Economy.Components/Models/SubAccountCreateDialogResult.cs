namespace CanDoItAll.Economy.Components.Models;

public sealed record SubAccountCreateDialogResult(
    string Name,
    bool ImportExisting,
    bool SendInitialAirdrop);
