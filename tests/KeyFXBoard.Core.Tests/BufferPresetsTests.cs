using KeyFXBoard.Core.Audio;

namespace KeyFXBoard.Core.Tests;

public sealed class BufferPresetsTests
{
    [Theory]
    [InlineData("LowLatency", 10)]
    [InlineData("Stable", 20)]
    [InlineData("Safe", 40)]
    [InlineData(null, 20)]
    [InlineData("nope", 20)]
    public void LatencyMs_maps_known_presets(string? id, int expected) =>
        Assert.Equal(expected, BufferPresets.LatencyMs(id));

    [Fact]
    public void Normalize_defaults_to_stable() =>
        Assert.Equal(BufferPresets.Stable, BufferPresets.Normalize("garbage"));
}
