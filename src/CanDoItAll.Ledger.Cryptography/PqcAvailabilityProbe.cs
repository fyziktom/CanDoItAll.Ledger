namespace CanDoItAll.Ledger.Cryptography;

public sealed record PqcAvailability(bool MLDsa, bool SlhDsa, bool MLKem);

public static class PqcAvailabilityProbe
{
    public static PqcAvailability Probe()
    {
#if NET10_0_OR_GREATER
#pragma warning disable SYSLIB5006
        bool mlDsa = System.Security.Cryptography.MLDsa.IsSupported;
        bool slhDsa = System.Security.Cryptography.SlhDsa.IsSupported;
        bool mlKem = System.Security.Cryptography.MLKem.IsSupported;
#pragma warning restore SYSLIB5006
        return new PqcAvailability(mlDsa, slhDsa, mlKem);
#else
        return new PqcAvailability(false, false, false);
#endif
    }
}
