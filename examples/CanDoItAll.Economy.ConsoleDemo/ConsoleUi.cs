using CanDoItAll.Economy.Core;

namespace CanDoItAll.Economy.ConsoleDemo;

internal static class ConsoleUi
{
    public static void Title(string text)
    {
        WriteColored(ConsoleColor.Cyan, text);
        Console.WriteLine(new string('=', text.Length));
    }

    public static void Section(string text)
    {
        Console.WriteLine();
        WriteColored(ConsoleColor.Cyan, text);
        Console.WriteLine(new string('-', text.Length));
    }

    public static void KeyValue(string key, string value)
        => Console.WriteLine($"{key.PadRight(10)} {value}");

    public static void Info(string message)
        => Console.WriteLine($"  {message}");

    public static void Success(string message)
        => WriteColored(ConsoleColor.Green, $"  OK  {message}");

    public static void Warning(string message)
        => WriteColored(ConsoleColor.Yellow, $"  WARN {message}");

    public static void Error(string message)
        => WriteColored(ConsoleColor.Red, $"  ERR {message}");

    public static void PrintReceipt(string label, LedgerSubmitResult result)
    {
        if (result.Accepted)
        {
            Success($"{label}: tx {Short(result.TxId?.Value ?? "(no tx)")}, sequence {result.LedgerSequence}.");
            return;
        }

        Warning($"{label}: rejected - {FormatErrors(result.Errors)}");
    }

    public static async Task ExpectFailureAsync<T>(string label, Func<Task<T>> operation)
    {
        try
        {
            T result = await operation().ConfigureAwait(false);
            if (result is LedgerSubmitResult submitResult && !submitResult.Accepted)
            {
                Warning($"{label}: rejected - {FormatErrors(submitResult.Errors)}");
                return;
            }

            Warning($"{label}: operation unexpectedly succeeded.");
        }
        catch (Exception ex)
        {
            Warning($"{label}: {ex.Message}");
        }
    }

    public static void Table(string[] headers, IEnumerable<string[]> rows)
    {
        string[][] materializedRows = rows.ToArray();
        int[] widths = headers
            .Select((header, index) => Math.Max(header.Length, materializedRows.Length == 0 ? 0 : materializedRows.Max(row => row[index].Length)))
            .ToArray();

        Console.WriteLine("  " + FormatRow(headers, widths));
        Console.WriteLine("  " + FormatRow(widths.Select(width => new string('-', width)).ToArray(), widths));
        foreach (string[] row in materializedRows)
        {
            Console.WriteLine("  " + FormatRow(row, widths));
        }
    }

    public static string Short(string value, int start = 12, int end = 6)
    {
        if (value.Length <= start + end + 3)
        {
            return value;
        }

        return $"{value[..start]}...{value[^end..]}";
    }

    private static string FormatRow(string[] columns, int[] widths)
        => string.Join("  ", columns.Select((column, index) => column.PadRight(widths[index])));

    private static string FormatErrors(IReadOnlyList<ValidationError> errors)
        => errors.Count == 0
            ? "no details returned"
            : string.Join("; ", errors.Select(error => error.ToString()));

    private static void WriteColored(ConsoleColor color, string text)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
