namespace KeyFXBoard.Core.Keys;

/// <summary>Platform-neutral key identity. v1 is a virtual-key code, not a character.</summary>
public readonly record struct KeyId(int VirtualKey)
{
    public static KeyId FromVirtualKey(int virtualKey) => new(virtualKey);
}
