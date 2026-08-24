namespace KeyFXBoard.Core.Keys;

public static class KeyCatalog
{
    private static readonly Dictionary<string, int> ByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<int, string> ByVirtualKey = new();

    static KeyCatalog()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            Add($"Key{c}", c);
        }

        for (var d = 0; d <= 9; d++)
        {
            Add($"D{d}", 0x30 + d);
        }

        for (var f = 1; f <= 12; f++)
        {
            Add($"F{f}", 0x6F + f);
        }

        Add("Space", 0x20);
        Add("Enter", 0x0D);
        Add("Tab", 0x09);
        Add("Escape", 0x1B);
        Add("Backspace", 0x08);
        Add("CapsLock", 0x14);
        Add("LeftShift", 0xA0);
        Add("RightShift", 0xA1);
        Add("LeftCtrl", 0xA2);
        Add("RightCtrl", 0xA3);
        Add("LeftAlt", 0xA4);
        Add("RightAlt", 0xA5);
        Add("LWin", 0x5B);
        Add("RWin", 0x5C);
        Add("Left", 0x25);
        Add("Up", 0x26);
        Add("Right", 0x27);
        Add("Down", 0x28);
        Add("Home", 0x24);
        Add("End", 0x23);
        Add("PageUp", 0x21);
        Add("PageDown", 0x22);
        Add("Insert", 0x2D);
        Add("Delete", 0x2E);
        Add("OemMinus", 0xBD);
        Add("OemPlus", 0xBB);
        Add("OemLeftBracket", 0xDB);
        Add("OemRightBracket", 0xDD);
        Add("OemSemicolon", 0xBA);
        Add("OemQuotes", 0xDE);
        Add("OemComma", 0xBC);
        Add("OemPeriod", 0xBE);
        Add("OemQuestion", 0xBF);
        Add("OemBackslash", 0xDC);
        Add("OemTilde", 0xC0);
        for (var n = 0; n <= 9; n++)
        {
            Add($"NumPad{n}", 0x60 + n);
        }

        Add("NumPadEnter", 0x0D);
        Add("Decimal", 0x6E);
        Add("Add", 0x6B);
        Add("Subtract", 0x6D);
        Add("Multiply", 0x6A);
        Add("Divide", 0x6F);
    }

    public static string NameOf(KeyId key) =>
        ByVirtualKey.TryGetValue(key.VirtualKey, out var name) ? name : $"Vk{key.VirtualKey}";

    public static bool TryGetVirtualKey(string name, out int virtualKey) =>
        ByName.TryGetValue(name, out virtualKey);

    public static readonly string[] SilentGroupIds = ["function", "modifiers", "numpad", "navigation"];

    public static bool InSilentGroup(int virtualKey, string group) => group.ToLowerInvariant() switch
    {
        "function" => virtualKey is >= 0x70 and <= 0x7B,
        "modifiers" => virtualKey is 0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C
            or >= 0xA0 and <= 0xA5,
        "numpad" => virtualKey is >= 0x60 and <= 0x6F,
        "navigation" => virtualKey is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
            or 0x2D or 0x2E,
        _ => false
    };

    private static void Add(string name, int virtualKey)
    {
        ByName[name] = virtualKey;
        ByVirtualKey.TryAdd(virtualKey, name);
    }
}
