using QuantaTrain.Infrastructure;

namespace QuantaTrain.App;

internal sealed class ReleaseTrayLink(Action<string> openPage)
{
    public Version? Available { get; set; }
    public Version? BalloonTarget { private get; set; }

    public void AddMenuItem(ContextMenuStrip menu, LocalizationService localizer)
    {
        if (Available is { } release)
        {
            menu.Items.Add(localizer.Text("Update.Available", release), null,
                (_, _) => OpenIfAvailable(release));
        }
    }

    public void OpenBalloon()
    {
        var release = BalloonTarget;
        BalloonTarget = null;
        OpenIfAvailable(release);
    }

    private void OpenIfAvailable(Version? release)
    {
        if (release is not null && release == Available)
        {
            openPage(ReleaseUpdateChecker.ReleasePage(release));
        }
    }
}
