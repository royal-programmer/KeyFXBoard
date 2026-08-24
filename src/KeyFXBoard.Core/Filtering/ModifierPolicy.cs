namespace KeyFXBoard.Core.Filtering;

public enum ModifierPolicy
{
    /// <summary>Mute when Ctrl, Alt, or Win is held. Shift still plays.</summary>
    Ignore,

    /// <summary>Play regardless of modifiers.</summary>
    Play
}
