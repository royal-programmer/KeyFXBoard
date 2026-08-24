using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Tests;

public sealed class KeyFilterTests
{
    [Fact]
    public void Down_plays_once_when_repeat_is_off()
    {
        var filter = new KeyFilter();
        var key = KeyId.FromVirtualKey(0x20);

        Assert.True(filter.ShouldPlay(Down(key)));
        Assert.False(filter.ShouldPlay(Repeat(key)));
        Assert.False(filter.ShouldPlay(Up(key)));
    }

    [Fact]
    public void Repeat_plays_when_repeat_is_on()
    {
        var filter = new KeyFilter();
        filter.Settings.Repeat = RepeatMode.On;
        var key = KeyId.FromVirtualKey(0x20);

        Assert.True(filter.ShouldPlay(Down(key)));
        Assert.True(filter.ShouldPlay(Repeat(key)));
    }

    [Fact]
    public void Control_shortcut_is_ignored_by_default()
    {
        var filter = new KeyFilter();
        var key = KeyId.FromVirtualKey(0x43);

        Assert.False(filter.ShouldPlay(new KeyEvent(key, KeyKind.Down, false, true, false, false)));
    }

    [Fact]
    public void Injected_keys_are_ignored_by_default()
    {
        var filter = new KeyFilter();
        var key = KeyId.FromVirtualKey(0x41);

        Assert.False(filter.ShouldPlay(new KeyEvent(key, KeyKind.Down, Injected: true, false, false, false)));
    }

    [Fact]
    public void PlayOn_up_only_fires_on_release()
    {
        var filter = new KeyFilter();
        filter.Settings.PlayOn = PlayOn.Up;
        var key = KeyId.FromVirtualKey(0x41);

        Assert.False(filter.ShouldPlay(Down(key)));
        Assert.True(filter.ShouldPlay(Up(key)));
    }

    [Fact]
    public void Rate_limit_blocks_fast_repeats()
    {
        var filter = new KeyFilter();
        filter.Settings.Repeat = RepeatMode.RateLimit;
        filter.Settings.RepeatRateLimitHz = 2;
        var key = KeyId.FromVirtualKey(0x20);

        Assert.True(filter.ShouldPlay(Repeat(key)));
        Assert.False(filter.ShouldPlay(Repeat(key)));
    }

    [Fact]
    public void Silent_groups_can_be_combined()
    {
        var filter = new KeyFilter();
        filter.Settings.SilentGroups = ["function", "numpad"];

        Assert.False(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x70))));
        Assert.False(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x60))));
        Assert.True(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x41))));
    }

    [Fact]
    public void Silent_keys_list_blocks_named_keys()
    {
        var filter = new KeyFilter();
        filter.Settings.SilentKeys = ["Space", "Tab"];

        Assert.False(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x20))));
        Assert.False(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x09))));
        Assert.True(filter.ShouldPlay(Down(KeyId.FromVirtualKey(0x41))));
    }

    private static KeyEvent Down(KeyId key) => new(key, KeyKind.Down, false, false, false, false);
    private static KeyEvent Repeat(KeyId key) => new(key, KeyKind.Repeat, false, false, false, false);
    private static KeyEvent Up(KeyId key) => new(key, KeyKind.Up, false, false, false, false);
}
