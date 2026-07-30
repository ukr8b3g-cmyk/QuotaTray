using System.Runtime.CompilerServices;

namespace QuantaTrain.App;

internal readonly record struct LogicalDpiWindowDefinition(
    Size LogicalClientSize,
    Size? LogicalMinimumSize,
    Size? LogicalMaximumSize,
    bool PreserveCurrentLogicalHeight);

internal sealed class LogicalDpiWindowManager : IMessageFilter, IDisposable
{
    private const int LogicalDpi = 96;
    private const int WmShowWindow = 0x0018;
    private const int WmDpiChanged = 0x02E0;

    private readonly ConditionalWeakTable<Form, WindowState> _states = new();
    private bool _disposed;

    public LogicalDpiWindowManager()
    {
        Application.Idle += HandleApplicationIdle;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg == WmShowWindow && message.WParam != nint.Zero)
        {
            ApplyFromHandle(message.HWnd, forcedDpi: null);
        }
        else if (message.Msg == WmDpiChanged)
        {
            ScheduleApplyAfterDpiChange(
                message.HWnd,
                DpiFromWParam(message.WParam));
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Application.Idle -= HandleApplicationIdle;
    }

    internal static bool TryGetDefinition(
        Type formType,
        out LogicalDpiWindowDefinition definition)
    {
        if (formType == typeof(MiniForm))
        {
            definition = new LogicalDpiWindowDefinition(
                new Size(220, 95),
                null,
                null,
                false);
            return true;
        }

        if (formType == typeof(CompactForm))
        {
            definition = new LogicalDpiWindowDefinition(
                new Size(286, 384),
                new Size(286, 384),
                null,
                false);
            return true;
        }

        if (formType == typeof(DetailForm))
        {
            definition = new LogicalDpiWindowDefinition(
                new Size(800, 600),
                new Size(800, 520),
                new Size(800, 2160),
                true);
            return true;
        }

        if (formType == typeof(SettingsForm))
        {
            definition = new LogicalDpiWindowDefinition(
                new Size(800, 680),
                new Size(800, 680),
                new Size(800, 2160),
                true);
            return true;
        }

        definition = default;
        return false;
    }

    internal static Size ScaleLogicalSize(Size logicalSize, int dpi) =>
        new(
            ScaleLogicalValue(logicalSize.Width, dpi),
            ScaleLogicalValue(logicalSize.Height, dpi));

    internal static int UnscaleLogicalValue(int value, int dpi)
    {
        dpi = NormalizeDpi(dpi);
        return (int)Math.Round(
            value * LogicalDpi / (double)dpi,
            MidpointRounding.AwayFromZero);
    }

    private static int ScaleLogicalValue(int value, int dpi)
    {
        dpi = NormalizeDpi(dpi);
        return (int)Math.Round(
            value * dpi / (double)LogicalDpi,
            MidpointRounding.AwayFromZero);
    }

    private static int NormalizeDpi(int dpi) => dpi > 0 ? dpi : LogicalDpi;

    private static int DpiFromWParam(nint value)
    {
        var packed = value.ToInt64();
        var dpiX = unchecked((ushort)(packed & 0xffff));
        var dpiY = unchecked((ushort)((packed >> 16) & 0xffff));
        return dpiX > 0 ? dpiX : dpiY;
    }

    private void HandleApplicationIdle(object? sender, EventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
        {
            EnsureAttachedAndApply(form, forcedDpi: null);
        }
    }

    private void ApplyFromHandle(nint handle, int? forcedDpi)
    {
        if (_disposed || Control.FromHandle(handle) is not Form form)
        {
            return;
        }

        EnsureAttachedAndApply(form, forcedDpi);
    }

    private void ScheduleApplyAfterDpiChange(nint handle, int dpi)
    {
        if (_disposed || Control.FromHandle(handle) is not Form form ||
            form.IsDisposed || !form.IsHandleCreated)
        {
            return;
        }

        try
        {
            form.BeginInvoke(
                (MethodInvoker)(() => EnsureAttachedAndApply(form, dpi)));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void EnsureAttachedAndApply(Form form, int? forcedDpi)
    {
        if (_disposed || form.IsDisposed ||
            !TryGetDefinition(form.GetType(), out var definition))
        {
            return;
        }

        var dpi = NormalizeDpi(forcedDpi ?? form.DeviceDpi);
        if (!_states.TryGetValue(form, out var state))
        {
            state = CreateInitialState(form, definition, dpi);
            _states.Add(form, state);
            form.ResizeEnd += (_, _) => CaptureResizedLogicalHeight(form, state);
        }

        Apply(form, state, dpi);
    }

    private static WindowState CreateInitialState(
        Form form,
        LogicalDpiWindowDefinition definition,
        int dpi)
    {
        if (!definition.PreserveCurrentLogicalHeight)
        {
            return new WindowState(definition);
        }

        var logicalHeight = LooksAlreadyScaled(
                form.ClientSize.Width,
                definition.LogicalClientSize.Width,
                dpi)
            ? UnscaleLogicalValue(form.ClientSize.Height, dpi)
            : form.ClientSize.Height;
        var minimumHeight = definition.LogicalMinimumSize?.Height ?? 1;
        var maximumHeight = definition.LogicalMaximumSize?.Height ?? int.MaxValue;
        logicalHeight = Math.Clamp(logicalHeight, minimumHeight, maximumHeight);

        return new WindowState(
            definition with
            {
                LogicalClientSize = new Size(
                    definition.LogicalClientSize.Width,
                    logicalHeight),
            });
    }

    private static bool LooksAlreadyScaled(
        int currentWidth,
        int logicalWidth,
        int dpi)
    {
        var scaledWidth = ScaleLogicalValue(logicalWidth, dpi);
        return Math.Abs(currentWidth - scaledWidth) <
               Math.Abs(currentWidth - logicalWidth);
    }

    private static void CaptureResizedLogicalHeight(Form form, WindowState state)
    {
        if (state.Applying || !state.Definition.PreserveCurrentLogicalHeight ||
            form.IsDisposed)
        {
            return;
        }

        var dpi = NormalizeDpi(form.DeviceDpi);
        var logicalHeight = UnscaleLogicalValue(form.ClientSize.Height, dpi);
        var minimumHeight = state.Definition.LogicalMinimumSize?.Height ?? 1;
        var maximumHeight = state.Definition.LogicalMaximumSize?.Height ?? int.MaxValue;
        logicalHeight = Math.Clamp(logicalHeight, minimumHeight, maximumHeight);
        state.Definition = state.Definition with
        {
            LogicalClientSize = new Size(
                state.Definition.LogicalClientSize.Width,
                logicalHeight),
        };
        state.LastAppliedDpi = dpi;
    }

    private static void Apply(Form form, WindowState state, int dpi)
    {
        if (state.Applying || state.LastAppliedDpi == dpi)
        {
            return;
        }

        state.Applying = true;
        try
        {
            form.MinimumSize = Size.Empty;
            form.MaximumSize = Size.Empty;

            if (state.Definition.LogicalMaximumSize is { } logicalMaximumSize)
            {
                form.MaximumSize = ScaleLogicalSize(logicalMaximumSize, dpi);
            }

            if (state.Definition.LogicalMinimumSize is { } logicalMinimumSize)
            {
                form.MinimumSize = ScaleLogicalSize(logicalMinimumSize, dpi);
            }

            form.ClientSize = ScaleLogicalSize(
                state.Definition.LogicalClientSize,
                dpi);
            state.LastAppliedDpi = dpi;
        }
        finally
        {
            state.Applying = false;
        }
    }

    private sealed class WindowState(LogicalDpiWindowDefinition definition)
    {
        public LogicalDpiWindowDefinition Definition { get; set; } = definition;
        public int LastAppliedDpi { get; set; }
        public bool Applying { get; set; }
    }
}
