using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace QuantaTrain.App.Tests;

public sealed class DpiScalingTests
{
    [Fact]
    public void CommonFormBaseKeepsNinetySixDpiDesignBaseline()
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
    public void FixedWidthBaseDoesNotApplyPhysicalPixelWidthOverrides()
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
    public void ManifestUsesDpiUnawareGdiScalingAndWindowsTenOrLater()
    {
        var manifest = File.ReadAllText(FindRepositoryFile(
            "src",
            "QuantaTrain.App",
            "app.manifest"));

        Assert.Contains(
            ">unaware</dpiAwareness>",
            manifest,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            ">true</gdiScaling>",
            manifest,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "PerMonitorV2",
            manifest,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
            manifest,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartupDoesNotOverrideManifestOrResizeTopLevelWindowsAgain()
    {
        var program = File.ReadAllText(FindRepositoryFile(
            "src",
            "QuantaTrain.App",
            "Program.cs"));

        Assert.DoesNotContain(
            "SetHighDpiMode",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LogicalDpiWindowManager",
            program,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[pathParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(pathParts, 0, parts, 1, pathParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Repository file was not found: {Path.Combine(pathParts)}");
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
