using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.Components.Models;

public sealed record AccountBackupWorkflowModel(
    string AccountName,
    Address Address,
    string BackupFileName,
    bool BackupAvailable,
    bool PasswordConfirmed,
    bool ImportMode,
    int Step,
    string StatusMessage);
