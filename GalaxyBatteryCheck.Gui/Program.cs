using GalaxyBatteryCheck.Core;

namespace GalaxyBatteryCheck.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        void ShowError(Exception error)
        {
            var language = Language.WindowsDisplayLanguage();
            MessageBox.Show(AppText.Get(language, "unexpected_error", error.GetType().Name),
                AppText.Get(language, "heading"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        try
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => { ShowError(e.Exception); Application.Exit(); };
            using var form = new MainForm();
            Application.Run(form);
        }
        catch (Exception error) { ShowError(error); }
    }
}
