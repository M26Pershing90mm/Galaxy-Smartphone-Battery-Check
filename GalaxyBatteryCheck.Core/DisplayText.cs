using System.Globalization;
using System.Text;

namespace GalaxyBatteryCheck.Core;

public static class DisplayText
{
    public static string Clean(string value)
    {
        var result = new StringBuilder(Math.Min(value.Length, 2048));
        foreach (var c in value.Take(2048))
            result.Append(char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format ? ' ' : c);
        return result.ToString();
    }
}
