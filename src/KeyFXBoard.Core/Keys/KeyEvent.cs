namespace KeyFXBoard.Core.Keys;

/// <summary>A classified key edge. Must not be logged or persisted.</summary>
public readonly record struct KeyEvent(
    KeyId Key,
    KeyKind Kind,
    bool Injected,
    bool Control,
    bool Alt,
    bool Win);
