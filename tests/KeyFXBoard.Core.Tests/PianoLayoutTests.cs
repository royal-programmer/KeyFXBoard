using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Tests;

public sealed class PianoLayoutTests
{
    [Fact]
    public void A_row_is_home_C4()
    {
        Assert.Equal("C4", PianoLayout.KeyNotes["KeyA"]);
        Assert.Equal("E4", PianoLayout.KeyNotes["KeyD"]);
        Assert.Equal("G4", PianoLayout.KeyNotes["KeyG"]);
        Assert.Equal("C3", PianoLayout.KeyNotes["KeyZ"]);
        Assert.Equal("C5", PianoLayout.KeyNotes["KeyQ"]);
        Assert.Equal("G5", PianoLayout.KeyNotes["KeyT"]);
        Assert.Equal("B5", PianoLayout.KeyNotes["KeyU"]);
    }

    [Fact]
    public void KeyD_is_E4()
    {
        Assert.True(KeyCatalog.TryGetVirtualKey("KeyD", out var vk));
        Assert.True(PianoLayout.TryGetNote(vk, out var note));
        Assert.Equal("E4", note);
        Assert.Equal(64, PianoLayout.MidiOf("E4"));
    }

    [Fact]
    public void Transpose_clamps_to_C2_C6()
    {
        Assert.Equal("C5", PianoLayout.Transpose("C4", 12));
        Assert.Equal("C4", PianoLayout.Transpose("C5", -12));
        Assert.Equal("C6", PianoLayout.Transpose("C6", 12));
        Assert.Equal("C2", PianoLayout.Transpose("C2", -12));
    }

    [Fact]
    public void Keyboard_labels_match_physical_keys()
    {
        Assert.Equal("Q", PianoLayout.KeyboardLabel("KeyQ"));
        Assert.Equal("A", PianoLayout.KeyboardLabel("KeyA"));
        Assert.Equal("2", PianoLayout.KeyboardLabel("D2"));
        Assert.Equal("[", PianoLayout.KeyboardLabel("OemLeftBracket"));
        Assert.Equal("PgDn", PianoLayout.KeyboardLabel("PageDown"));
    }

    [Fact]
    public void Map_at_shift_zero_uses_home_keys()
    {
        Assert.True(PianoLayout.TryGetBinding("E4", out var e4));
        Assert.Equal("D", e4.KeyLabel);
        Assert.Equal(0, e4.OctaveShift);

        Assert.True(PianoLayout.TryGetBinding("C4", out var c4));
        Assert.Equal("A", c4.KeyLabel);

        Assert.False(PianoLayout.TryGetBinding("C2", out _));
        Assert.True(PianoLayout.TryGetBinding("C2", out var c2, octaveShift: -12));
        Assert.Equal("Z", c2.KeyLabel);

        Assert.True(PianoLayout.TryGetBinding("C6", out var c6, octaveShift: 12));
        Assert.Equal("Q", c6.KeyLabel);
    }
}
