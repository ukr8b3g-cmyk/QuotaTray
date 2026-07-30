using System.Drawing;
using System.Windows.Forms;

namespace QuantaTrain.App.Tests;

public sealed class LogicalDpiWindowManagerTests
{
    [Theory]
    [InlineData(220, 95, 440, 190)]
    [InlineData(286, 384, 572, 768)]
    [InlineData(800, 600, 1600, 1200)]
    [InlineData(800, 680, 1600, 1360)]
    public void PrimaryWindowSizesDoubleAtTwoHundredPercent(
        int logicalWidth,
        int logicalHeight,
        int expectedWidth,
        int expectedHeight)
    {
        Assert.Equal(
            new Size(expectedWidth, expectedHeight),
            LogicalDpiWindowManager.ScaleLogicalSize(
                new Size(logicalWidth, logicalHeight),
                192));
    }

    [Theory]
    [InlineData(190, 192, 95)]
    [InlineData(768, 192, 384)]
    [InlineData(1200, 192, 600)]
    [InlineData(1360, 192, 680)]
    public void DeviceHeightConvertsBackToLogicalHeight(
        int deviceHeight,
        int dpi,
        int expectedLogicalHeight)
    {
        Assert.Equal(
            expectedLogicalHeight,
            LogicalDpiWindowManager.UnscaleLogicalValue(deviceHeight, dpi));
    }

    [Fact]
    public void DefinitionsCoverMiniCompactDetailAndSettings()
    {
        AssertDefinition(
            typeof(MiniForm),
            new Size(220, 95),
            null,
            null,
            preserveCurrentLogicalHeight: false);
        AssertDefinition(
            typeof(CompactForm),
            new Size(286, 384),
            new Size(286, 384),
            null,
            preserveCurrentLogicalHeight: false);
        AssertDefinition(
            typeof(DetailForm),
            new Size(800, 600),
            new Size(800, 520),
            new Size(800, 2160),
            preserveCurrentLogicalHeight: true);
        AssertDefinition(
            typeof(SettingsForm),
            new Size(800, 680),
            new Size(800, 680),
            new Size(800, 2160),
            preserveCurrentLogicalHeight: true);

        Assert.False(LogicalDpiWindowManager.TryGetDefinition(
            typeof(Form),
            out _));
    }

    private static void AssertDefinition(
        Type formType,
        Size clientSize,
        Size? minimumSize,
        Size? maximumSize,
        bool preserveCurrentLogicalHeight)
    {
        Assert.True(LogicalDpiWindowManager.TryGetDefinition(
            formType,
            out var definition));
        Assert.Equal(clientSize, definition.LogicalClientSize);
        Assert.Equal(minimumSize, definition.LogicalMinimumSize);
        Assert.Equal(maximumSize, definition.LogicalMaximumSize);
        Assert.Equal(
            preserveCurrentLogicalHeight,
            definition.PreserveCurrentLogicalHeight);
    }
}
