using CanDoItAll.Ledger.BusinessObjects;
using CanDoItAll.Ledger.Core;

namespace CanDoItAll.Ledger.ConsoleDemo;

internal sealed record DemoOptions(
    Uri NodeBaseUri,
    string KeyRoot,
    LedgerAccountContext Context,
    bool ListLessons,
    string? Lesson)
{
    public static DemoOptions Parse(string[] args)
    {
        bool listLessons = args.Any(arg => string.Equals(arg, "--list-lessons", StringComparison.OrdinalIgnoreCase));
        string? lesson = ReadOption(args, "--lesson");
        string node = ReadOption(args, "--node")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_NODE_URL")
            ?? "http://localhost:5000/";
        string keyRoot = ReadOption(args, "--keys")
            ?? Environment.GetEnvironmentVariable("CANDOITALL_LEDGER_TEST_KEY_ROOT")
            ?? @"C:\programovani\candoitall-ledger\testkeys";
        string ledger = ReadOption(args, "--ledger") ?? "default";
        string? scenario = ReadOption(args, "--scenario");

        return new DemoOptions(
            EnsureTrailingSlash(new Uri(node, UriKind.Absolute)),
            keyRoot,
            new LedgerAccountContext(new LedgerId(ledger), string.IsNullOrWhiteSpace(scenario) ? null : new ScenarioId(scenario)),
            listLessons,
            lesson);
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {name}.");
            }

            return args[i + 1];
        }

        return null;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        string value = uri.ToString();
        return value.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(value + "/", UriKind.Absolute);
    }
}
