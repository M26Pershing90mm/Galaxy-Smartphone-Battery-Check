using System.Runtime.InteropServices;

namespace GalaxyBatteryCheck.Core;

public static class Language
{

    public static string FromTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return "en";
        var primary = tag.Replace('_', '-').ToLowerInvariant().Split('-', '.', '@')[0];
        return AppInfo.SupportedLanguages.Contains(primary) ? primary : "en";
    }

    public static string WindowsDisplayLanguage()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "en";

        try
        {
            uint numLanguages = 0;
            uint bufferSize = 0;
            if (NativeMethods.GetUserPreferredUILanguages(NativeMethods.MuiLanguageName, ref numLanguages, null, ref bufferSize) && bufferSize > 0)
            {
                var buffer = new char[bufferSize];
                if (NativeMethods.GetUserPreferredUILanguages(NativeMethods.MuiLanguageName, ref numLanguages, buffer, ref bufferSize))
                {

                    var tag = new string(buffer).Split('\0', 2)[0];
                    if (!string.IsNullOrEmpty(tag))
                        return FromTag(tag);
                }
            }
        }
        catch
        {

        }

        try
        {
            uint langId = NativeMethods.GetUserDefaultUILanguage();
            uint primary = langId & 0x3FF;
            return primary switch { 0x12 => "ko", 0x09 => "en", 0x11 => "ja", _ => "en" };
        }
        catch
        {
            return "en";
        }
    }

    public static string DonationUrl(string? language = null)
    {
        var code = language is not null && AppInfo.SupportedLanguages.Contains(language) ? language : WindowsDisplayLanguage();
        return code == "ko" ? AppInfo.DonationUrl : AppInfo.KofiUrl;
    }
}
