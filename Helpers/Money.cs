namespace Arlink28.Api.Helpers;

public static class Money
{
    private static readonly Dictionary<string, int> Exponents = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 2, ["GBP"] = 2, ["EUR"] = 2,
        ["NGN"] = 2, ["KES"] = 2, ["GHS"] = 2,
        ["ZAR"] = 2, ["TZS"] = 2
    };

    public static long ToMinor(decimal major, string currency)
    {
        var exp = Exponents.GetValueOrDefault(currency.ToUpper(), 2);
        return (long)Math.Round(major * (decimal)Math.Pow(10, exp), MidpointRounding.AwayFromZero);
    }

    public static decimal FromMinor(long minor, string currency)
    {
        var exp = Exponents.GetValueOrDefault(currency.ToUpper(), 2);
        return minor / (decimal)Math.Pow(10, exp);
    }

    public static string Format(long minor, string currency)
    {
        var major = FromMinor(minor, currency);
        return $"{currency} {major:N2}";
    }
}
