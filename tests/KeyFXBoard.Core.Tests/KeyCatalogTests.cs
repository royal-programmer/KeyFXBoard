using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Tests;

public sealed class KeyCatalogTests
{
    [Fact]
    public void Maps_letter_and_enter()
    {
        Assert.True(KeyCatalog.TryGetVirtualKey("KeyA", out var a));
        Assert.Equal(0x41, a);
        Assert.Equal("Enter", KeyCatalog.NameOf(KeyId.FromVirtualKey(0x0D)));
    }

    [Fact]
    public void Unknown_virtual_key_uses_vk_name()
    {
        Assert.Equal("Vk1", KeyCatalog.NameOf(KeyId.FromVirtualKey(1)));
    }

    [Fact]
    public void Function_and_numpad_silent_groups()
    {
        Assert.True(KeyCatalog.InSilentGroup(0x70, "function"));
        Assert.True(KeyCatalog.InSilentGroup(0x60, "numpad"));
        Assert.True(KeyCatalog.InSilentGroup(0x25, "navigation"));
        Assert.True(KeyCatalog.InSilentGroup(0xA2, "modifiers"));
        Assert.False(KeyCatalog.InSilentGroup(0x41, "function"));
    }
}
