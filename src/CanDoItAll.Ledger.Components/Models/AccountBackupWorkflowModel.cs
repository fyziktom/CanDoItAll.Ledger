using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.Components.Models;

public sealed record AccountBackupWorkflowModel(
    string AccountName,
    Address Address,
    string BackupFileName,
    bool BackupAvailable,
    bool PasswordConfirmed,
    bool ImportMode,
    int Step,
    string StatusMessage);
