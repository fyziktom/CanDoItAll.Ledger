using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid.Infrastructure;
using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Components.Rendering;
using CanDoItAll.Ledger.Core;
using CanDoItAll.Ledger.Cryptography;
using CanDoItAll.Ledger.Engine;
using CanDoItAll.Ledger.Node.Components;
using CanDoItAll.Ledger.Persistence.EFCore;
using CanDoItAll.Ledger.Persistence.PostgreSql;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace CanDoItAll.Ledger.Node;
internal static class LedgerNodeBootstrapper
{
    private static readonly LedgerId DefaultLedgerId = new("default");

    public static async Task BootstrapTreasuryAdminAsync(
        LedgerDbContext db,
        LedgerExecutor executor,
        ITimeProvider timeProvider,
        LedgerNodeHostOptions options,
        CancellationToken cancellationToken)
    {
        LedgerNodeAdminAccount adminAccount = options.ValidateAndGetTreasuryAdminAccount();
        string treasuryAddress = adminAccount.Address.Value;
        if (string.IsNullOrWhiteSpace(treasuryAddress))
        {
            throw new InvalidOperationException("Treasury admin address cannot be empty.");
        }

        if (options.TreasuryAdminMainAmount < 0m)
        {
            throw new InvalidOperationException("Treasury admin bootstrap amount must be non-negative.");
        }

        bool ledgerExists = await db.LedgerStates
            .AsNoTracking()
            .AnyAsync(x => x.LedgerId == DefaultLedgerId.Value && x.ScenarioId == null, cancellationToken)
            .ConfigureAwait(false);
        if (ledgerExists)
        {
            return;
        }

        var transaction = new LedgerTransaction(
            DefaultLedgerId,
            ScenarioId: null,
            Version: 1,
            TransactionKind.Genesis,
            SigningMode.Independent,
            timeProvider.UtcNow,
            Array.Empty<TransactionInput>(),
            new[]
            {
                new TransactionOutput(0, new Address(treasuryAddress), new Amount(options.TreasuryAdminMainAmount)),
                new TransactionOutput(
                    1,
                    new Address(treasuryAddress),
                    Amount.Zero,
                    new TokenTransfer(PermissionTokenAuthority.PermissionTokenId, new Amount(decimal.MaxValue)))
            },
            Data: null);

        LedgerSubmitResult result = await executor.SubmitAsync(transaction, cancellationToken).ConfigureAwait(false);
        if (!result.Accepted)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.ToString())));
        }
    }
}
