using System.Globalization;

namespace QuantaTrain.App;

internal static class QuotaDisplay
{
    public static string Number(double value) =>
        Math.Round(value, 1).ToString("0.#", CultureInfo.CurrentCulture);

    public static string Percent(double value) => $"{Number(value)}%";
}
