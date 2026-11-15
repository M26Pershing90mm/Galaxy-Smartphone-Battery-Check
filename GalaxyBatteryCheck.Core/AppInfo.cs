namespace GalaxyBatteryCheck.Core;

public static class AppInfo
{
    public const string Name = "Galaxy Battery Check";
    public const string Version = "1.4.8";
    public const string Author = "Sakai";
    public const string CopyrightHolder = "꿈을꾸는 파랑새";
    public const string BlogUrl = "https://wezard4u.tistory.com/";
    public const string DonationUrl = "https://toon.at/donate/637833976686140024";
    public const string KofiUrl = "https://ko-fi.com/sakai38666";
    public const string CopyrightNotice = "Galaxy Battery Check — Sakai";
    public const string AuthorNotice = $"Developed by {Author} | {CopyrightNotice}";

    public static readonly IReadOnlySet<string> SupportedLanguages = new HashSet<string> { "ko", "en", "ja" };
}
