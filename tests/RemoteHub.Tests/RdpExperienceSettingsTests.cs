using RemoteHub.Core.Models;
using Xunit;

namespace RemoteHub.Tests;

public class RdpExperienceSettingsTests
{
    [Fact]
    public void Defaults_EnableEverything()
    {
        // Nothing disabled; font smoothing (0x80) and desktop composition (0x100) switched on.
        Assert.Equal(0x180, new RdpExperienceSettings().ToPerformanceFlags());
    }

    [Fact]
    public void AllOff_DisablesEveryFeatureAndEnablesNone()
    {
        var experience = new RdpExperienceSettings
        {
            DesktopBackground = false,
            FontSmoothing = false,
            DesktopComposition = false,
            ShowWindowContentsWhileDragging = false,
            MenuAnimations = false,
            VisualStyles = false,
        };

        Assert.Equal(0x0F, experience.ToPerformanceFlags());
    }

    [Theory]
    [InlineData(nameof(RdpExperienceSettings.DesktopBackground), 0x181)]
    [InlineData(nameof(RdpExperienceSettings.ShowWindowContentsWhileDragging), 0x182)]
    [InlineData(nameof(RdpExperienceSettings.MenuAnimations), 0x184)]
    [InlineData(nameof(RdpExperienceSettings.VisualStyles), 0x188)]
    [InlineData(nameof(RdpExperienceSettings.FontSmoothing), 0x100)]
    [InlineData(nameof(RdpExperienceSettings.DesktopComposition), 0x080)]
    public void TurningOneFeatureOff_FlipsOnlyItsBit(string property, int expected)
    {
        var experience = new RdpExperienceSettings();
        typeof(RdpExperienceSettings).GetProperty(property)!.SetValue(experience, false);

        Assert.Equal(expected, experience.ToPerformanceFlags());
    }
}
