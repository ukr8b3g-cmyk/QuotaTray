using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace QuantaTrain.App.Tests;

public sealed class DpiScalingTests
{
    [Fact]
    public void CommonFormBaseUsesNinetySixDpiDesignBaseline()
    {
        RunSta(() =>
        {
            using var form = new TestFixedWidthForm();

            Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
            Assert.Equal(new SizeF(96F, 96F), form.AutoScaleDimensions);
            Assert.Equal(800, form.MinimumSize.Width);
            Assert.Equal(800, form.MaximumSize.Width);
        });
    }

    [Fact]
    public void FixedWidthBaseDoesNotOverrideBoundsDuringDpiScaling()
    {
        var overrideMethod = typeof(FixedWidthResizableForm).GetMethod(
            "SetBoundsCore",
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);

        Assert.Null(overrideMethod);
    }

    [Fact]
    public void TwoHundredPercentScaleKeepsWindowAndRightEdgeControlTogether()
    {
        RunSta(() =>
        {
            using var form = new TestFixedWidthForm();
            var baseline = form.ClientSize;

            // Remove resize constraints only for this direct scaling probe. The
            // production constraints are DPI-scaled by WinForms in PerMonitorV2.
            form.MinimumSize = Size.Empty;
            form.MaximumSize = Size.Empty;
            form.Scale(new SizeF(2F, 2F));

            Assert.True(
                form.ClientSize.Width > baseline.Width,
                $"Width did not scale: {baseline.Width} -> {form.ClientSize.Width}.");
            Assert.True(
                form.ClientSize.Height > baseline.Height,
                $"Height did not scale: {baseline.Height} -> {form.ClientSize.Height}.");
            Assert.True(
                form.Probe.Right <= form.ClientSize.Width,
                $"Probe {form.Probe.Bounds} exceeded client {form.ClientRectangle}.");
        });
    }

    [Fact]
    public void ManifestDeclaresPerMonitorV2AndWindowsTenOrLater()
    {
        var manifest = File.ReadAllText(FindManifest());

        Assert.Contains(
            ">PerMonitorV2</dpiAwareness>",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
            manifest,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FindManifest()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QuantaTrain.App",
                "app.manifest");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("QuantaTray app.manifest was not found.");
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(failure)
                .Throw();
        }
    }

    private sealed class TestFixedWidthForm : FixedWidthResizableForm
    {
        public TestFixedWidthForm()
        {
            ConfigureFixedLogicalWidth(800, 680, 680);
            Probe.Bounds = new Rectangle(755, 10, 31, 32);
            Probe.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(Probe);
        }

        public Button Probe { get; } = new();
    }
}
