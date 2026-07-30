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
            Assert.Equal(800, form.ClientSize.Width);
            Assert.Equal(800, form.MinimumSize.Width);
            Assert.Equal(800, form.MaximumSize.Width);
        });
    }

    [Fact]
    public void FixedWidthBaseLeavesDpiResizeToWinForms()
    {
        const BindingFlags instanceMembers =
            BindingFlags.Instance |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;
        const BindingFlags staticMembers =
            BindingFlags.Static |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        Assert.Null(typeof(FixedWidthResizableForm).GetMethod(
            "SetBoundsCore",
            instanceMembers));
        Assert.Null(typeof(FixedWidthResizableForm).GetField(
            "_fixedWidth",
            instanceMembers));
        Assert.Null(typeof(FixedWidthResizableForm).GetField(
            "WmDpiChanged",
            staticMembers));
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
        }
    }
}
