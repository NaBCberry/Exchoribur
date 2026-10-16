using Avalonia;
using Exchoribur.App.Services;

namespace Exchoribur.App.Tests;

public sealed class VideoPreviewWindowSizingTests
{
    [Theory]
    [InlineData(640, 360, 1920, 1080, 1, 640, 360)]
    [InlineData(3840, 2160, 1920, 1080, 1, 1280, 720)]
    [InlineData(1920, 1080, 3840, 2160, 2, 960, 540)]
    [InlineData(1080, 1920, 1920, 1080, 1, 405, 720)]
    [InlineData(0, 0, 1920, 1080, 1, 800, 450)]
    public void Fits_within_two_thirds_of_physical_working_area_without_upscaling(
        int width, int height, int screenWidth, int screenHeight, double scaling,
        double expectedWidth, double expectedHeight)
    {
        var size = VideoPreviewWindowSizing.Calculate(new PixelSize(width, height),
            new PixelSize(screenWidth, screenHeight), scaling, default);
        Assert.Equal(expectedWidth, size.Width, 5);
        Assert.Equal(expectedHeight, size.Height, 5);
    }

    [Fact]
    public void Window_decorations_are_included_in_the_limit()
    {
        var size = VideoPreviewWindowSizing.Calculate(new PixelSize(3840, 2160),
            new PixelSize(1920, 1080), 1, new Thickness(8, 32, 8, 8));
        Assert.True(size.Width + 16 <= 1280);
        Assert.True(size.Height + 40 <= 720);
        Assert.Equal(16d / 9, size.Width / size.Height, 5);
    }
}
