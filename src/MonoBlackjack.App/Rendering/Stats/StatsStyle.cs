using Microsoft.Xna.Framework;

namespace MonoBlackjack.Rendering.Stats;

internal static class StatsStyle
{
    internal static readonly string[] UpcardOrder = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "T"];
    internal static readonly Color DashboardSurface = new(8, 12, 18, 216);
    internal static readonly Color ChartSurface = new(14, 22, 31, 230);
    internal static readonly Color PrimaryText = new(236, 242, 248);
    internal static readonly Color SecondaryText = new(183, 197, 214);
    internal static readonly Color DividerColor = new(104, 126, 148, 118);
    internal static readonly Color MatrixLowSampleColor = new(68, 78, 92, 208);
}
