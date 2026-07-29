namespace CanDoItAll.Ledger.Core;

public enum TransactionKind
{
    Genesis = 0,
    Mint = 1,
    Transfer = 2,
    BusinessObject = 3,
    Burn = 4
}

public enum TransactionStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Reverted = 3,
    Archived = 4
}

public enum SigningMode
{
    Independent = 0,
    Ordered = 1
}

public enum AddressStatus
{
    Active = 0,
    Suspended = 1,
    Retired = 2
}

public enum SignatureAlgorithmId
{
    VefSecp256K1Sha256 = 0,
    EcdsaP256Sha256 = 1,
    MLDsa44 = 10,
    MLDsa65 = 11,
    MLDsa87 = 12,
    SLHDsa128s = 20,
    SLHDsa192s = 21,
    SLHDsa256s = 22,
    HybridVefSecp256K1Mldsa65 = 100
}

public enum BusinessObjectOperation
{
    Create = 0,
    Update = 1,
    Transfer = 2,
    Close = 3
}

public enum TransactionDataItemType
{
    Generic = 0,
    TokenMetadata = 1,
    BusinessObject = 2,
    AccountSettings = 3,
    ScenarioMetadata = 4,
    SnapshotMetadata = 5,
    Permission = 6,
    Rule = 7
}
