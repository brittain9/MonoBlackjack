using MonoBlackjack.Core.Ports;

namespace MonoBlackjack.Rendering.Stats;

internal static class StatsFormatting
{
    internal static (decimal Latest, decimal Peak, decimal Trough) ComputeBankrollSummary(IReadOnlyList<BankrollPoint> history)
    {
        if (history.Count == 0)
            return (0m, 0m, 0m);

        decimal latest = history[^1].CumulativeProfit;
        decimal peak = history.Max(x => x.CumulativeProfit);
        decimal trough = history.Min(x => x.CumulativeProfit);
        return (latest, peak, trough);
    }

    internal static string FormatSignedCurrency(decimal amount)
    {
        if (amount > 0)
            return $"+${amount:F0}";
        if (amount < 0)
            return $"-${Math.Abs(amount):F0}";
        return "$0";
    }
}
