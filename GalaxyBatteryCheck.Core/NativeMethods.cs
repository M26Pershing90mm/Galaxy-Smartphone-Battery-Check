using System.Runtime.InteropServices;

namespace GalaxyBatteryCheck.Core;

internal static class NativeMethods
{
    internal const uint MuiLanguageName = 0x8;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetUserPreferredUILanguages(
        uint dwFlags, ref uint pulNumLanguages, char[]? pwszLanguagesBuffer, ref uint pcchLanguagesBuffer);

    [DllImport("kernel32.dll")]
    internal static extern uint GetUserDefaultUILanguage();
}
