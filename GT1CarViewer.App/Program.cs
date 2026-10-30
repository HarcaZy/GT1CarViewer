namespace GT1CarViewer.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppText.LoadPreference();
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
