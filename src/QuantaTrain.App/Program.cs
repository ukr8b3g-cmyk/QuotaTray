namespace QuantaTrain.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var shutdownRequested = Environment.GetCommandLineArgs()
            .Contains("--shutdown", StringComparer.OrdinalIgnoreCase);
        using var singleInstance = SingleInstanceCoordinator.Create();
        if (shutdownRequested)
        {
            if (!singleInstance.IsPrimary)
            {
                singleInstance.RequestPrimaryShutdown();
                singleInstance.WaitForPrimaryExit(TimeSpan.FromSeconds(10));
            }
            return;
        }

        if (!singleInstance.IsPrimary)
        {
            singleInstance.NotifyPrimary();
            return;
        }

        using var context = new QuantaTrainContext(singleInstance);
        Application.Run(context);
    }
}
