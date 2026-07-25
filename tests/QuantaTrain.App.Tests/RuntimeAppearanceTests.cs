using QuantaTrain.Core;
using System.Windows.Forms;

namespace QuantaTrain.App.Tests;

public sealed class RuntimeAppearanceTests
{
    [Fact]
    public void ThemeCanSwitchBetweenDarkAndLightAtRuntime()
    {
        Theme.Configure("dark", "green");
        var darkWindow = Theme.Window;
        var darkText = Theme.Text;

        Theme.Configure("light", "green");

        Assert.NotEqual(darkWindow, Theme.Window);
        Assert.NotEqual(darkText, Theme.Text);
        Assert.True(Theme.Window.GetBrightness() > Theme.Text.GetBrightness());
    }

    [Fact]
    public void AccentCanSwitchAtRuntime()
    {
        Theme.Configure("dark", "green");
        var green = Theme.Accent;

        Theme.Configure("dark", "purple");

        Assert.NotEqual(green, Theme.Accent);
    }

    [Fact]
    public void LocalizationCanReloadAtRuntime()
    {
        var localizer = new LocalizationService(FindLocalesDirectory());
        localizer.Load(new LanguageSettings { Mode = "manual", Locale = "en-US" });
        var english = localizer.Text("Common.Settings");

        localizer.Load(new LanguageSettings { Mode = "manual", Locale = "ja-JP" });

        Assert.Equal("ja-JP", localizer.CurrentLocale);
        Assert.NotEqual(english, localizer.Text("Common.Settings"));
    }

    [Fact]
    public void OpacitySliderUpdatesSettingsAndVisibleFormImmediately()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);
            using var form = new SettingsForm(settings, localizer);
            var slider = (ValueSlider?)typeof(SettingsForm)
                .GetField(
                    "_opacity",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(form);

            Assert.NotNull(slider);
            slider.Value = 72;

            Assert.Equal(72, settings.Display.OpacityPercent);
            Assert.Equal(0.72d, form.Opacity, 2);
        });
    }

    [Fact]
    public void ThemeAndLanguageSelectionsRequestImmediateRebuild()
    {
        RunSta(() =>
        {
            var settings = new AppSettings();
            var localizer = new LocalizationService(FindLocalesDirectory());
            localizer.Load(settings.Language);

            using (var themeForm = new SettingsForm(settings, localizer))
            {
                _ = themeForm.Handle;
                var theme = GetField<ComboBox>(themeForm, "_theme");
                theme.SelectedItem = "light";

                Assert.Equal("light", settings.Display.Theme);
                Assert.True(themeForm.ReopenRequested);
                Assert.Equal(1, themeForm.ReopenPage);
            }

            using var languageForm = new SettingsForm(settings, localizer, 2);
            _ = languageForm.Handle;
            var locale = GetField<ComboBox>(languageForm, "_locale");
            locale.SelectedItem = "ja-JP";

            Assert.Equal("manual", settings.Language.Mode);
            Assert.Equal("ja-JP", settings.Language.Locale);
            Assert.True(languageForm.ReopenRequested);
            Assert.Equal(2, languageForm.ReopenPage);
        });
    }

    private static string FindLocalesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "QuantaTrain.App",
                "locales");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Source locales directory was not found.");
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

    private static T GetField<T>(object target, string name)
        where T : class
    {
        return typeof(SettingsForm)
            .GetField(
                name,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(target) as T
            ?? throw new InvalidOperationException(name);
    }
}
