using QuantaTrain.Core;

namespace QuantaTrain.App;

internal static class PanelPlacement
{
    private const int SnapThreshold = 16;
    private const int EdgeAnchorThreshold = 32;

    public static bool TryRestore(Form form, PanelPositionSettings position)
    {
        if (position.X is null || position.Y is null)
        {
            return false;
        }

        var screen = Screen.AllScreens.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.DeviceName,
                    position.MonitorDeviceName,
                    StringComparison.OrdinalIgnoreCase))
            ?? Screen.PrimaryScreen
            ?? Screen.FromPoint(Point.Empty);
        var x = string.Equals(
            position.HorizontalAnchor,
            "right",
            StringComparison.OrdinalIgnoreCase)
            ? screen.WorkingArea.Right - form.Width - position.X.Value
            : screen.WorkingArea.Left + position.X.Value;
        var y = string.Equals(
            position.VerticalAnchor,
            "bottom",
            StringComparison.OrdinalIgnoreCase)
            ? screen.WorkingArea.Bottom - form.Height - position.Y.Value
            : screen.WorkingArea.Top + position.Y.Value;
        form.Location = ClampToWorkingArea(
            screen.WorkingArea,
            form.Size,
            new Point(x, y));
        return true;
    }

    public static void Capture(Form form, PanelPositionSettings position)
    {
        var screen = Screen.FromRectangle(form.Bounds);
        position.MonitorDeviceName = screen.DeviceName;
        var rightMargin = screen.WorkingArea.Right - form.Right;
        var bottomMargin = screen.WorkingArea.Bottom - form.Bottom;
        var anchorRight = Math.Abs(rightMargin) <= EdgeAnchorThreshold;
        var anchorBottom = Math.Abs(bottomMargin) <= EdgeAnchorThreshold;
        position.HorizontalAnchor = anchorRight ? "right" : "left";
        position.VerticalAnchor = anchorBottom ? "bottom" : "top";
        position.X = anchorRight
            ? Math.Max(0, rightMargin)
            : Math.Max(0, form.Left - screen.WorkingArea.Left);
        position.Y = anchorBottom
            ? Math.Max(0, bottomMargin)
            : Math.Max(0, form.Top - screen.WorkingArea.Top);
    }

    public static PanelPositionSettings Clone(PanelPositionSettings position) =>
        new()
        {
            MonitorDeviceName = position.MonitorDeviceName,
            HorizontalAnchor = position.HorizontalAnchor,
            VerticalAnchor = position.VerticalAnchor,
            X = position.X,
            Y = position.Y,
        };

    public static bool IsReachable(Form form)
    {
        foreach (var screen in Screen.AllScreens)
        {
            var intersection = Rectangle.Intersect(screen.WorkingArea, form.Bounds);
            if (intersection.Width >= 32 && intersection.Height >= 32)
            {
                return true;
            }
        }
        return false;
    }

    public static void CenterOnPrimary(Form form)
    {
        var screen = Screen.PrimaryScreen ?? Screen.FromPoint(Point.Empty);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = CenterInWorkingArea(screen.WorkingArea, form.Size);
    }

    public static void SnapToEdge(Form form)
    {
        var screen = Screen.FromRectangle(form.Bounds);
        form.Location = SnapToWorkingArea(
            screen.WorkingArea,
            form.Size,
            form.Location,
            SnapThreshold);
    }

    internal static Point ClampToWorkingArea(
        Rectangle workingArea,
        Size panelSize,
        Point location)
    {
        var maximumX = Math.Max(workingArea.Left, workingArea.Right - panelSize.Width);
        var maximumY = Math.Max(workingArea.Top, workingArea.Bottom - panelSize.Height);
        return new Point(
            Math.Clamp(location.X, workingArea.Left, maximumX),
            Math.Clamp(location.Y, workingArea.Top, maximumY));
    }

    internal static Point CenterInWorkingArea(
        Rectangle workingArea,
        Size panelSize) =>
        ClampToWorkingArea(
            workingArea,
            panelSize,
            new Point(
                workingArea.Left + ((workingArea.Width - panelSize.Width) / 2),
                workingArea.Top + ((workingArea.Height - panelSize.Height) / 2)));

    internal static Point SnapToWorkingArea(
        Rectangle workingArea,
        Size panelSize,
        Point location,
        int threshold)
    {
        var clamped = ClampToWorkingArea(workingArea, panelSize, location);
        var right = workingArea.Right - panelSize.Width;
        var bottom = workingArea.Bottom - panelSize.Height;
        var x = Math.Abs(clamped.X - workingArea.Left) <= threshold
            ? workingArea.Left
            : Math.Abs(clamped.X - right) <= threshold
                ? right
                : clamped.X;
        var y = Math.Abs(clamped.Y - workingArea.Top) <= threshold
            ? workingArea.Top
            : Math.Abs(clamped.Y - bottom) <= threshold
                ? bottom
                : clamped.Y;
        return new Point(x, y);
    }
}
