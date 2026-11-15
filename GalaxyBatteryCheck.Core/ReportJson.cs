using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace GalaxyBatteryCheck.Core;

public static class ReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,

        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static string Serialize(Report report) => JsonSerializer.Serialize(report, Options) + "\n";
}
