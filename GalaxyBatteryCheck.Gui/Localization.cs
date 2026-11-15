using GalaxyBatteryCheck.Core;

namespace GalaxyBatteryCheck.Gui;

internal static class Localization
{
    public static IReadOnlyDictionary<string, string> LanguageNames => AppText.LanguageNames;
    public static string Get(string language, string key) => AppText.Get(language, key);
    public static string Get(string language, string key, params object[] args) => AppText.Get(language, key, args);
}
