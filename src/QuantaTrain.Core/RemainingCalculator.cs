namespace QuantaTrain.Core;

public static class RemainingCalculator
{
    public static double FromUsedPercent(double usedPercent) =>
        Math.Clamp(100d - usedPercent, 0d, 100d);
}
