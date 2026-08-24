using System.Diagnostics;
using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Filtering;

/// <summary>Pure policy. Classification of Down/Repeat/Up happens in the keyboard source.</summary>
public sealed class KeyFilter
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long? _lastRepeatFireTicks;

    public FilterSettings Settings { get; } = new();

    public bool ShouldPlay(in KeyEvent e)
    {
        if (Settings.IgnoreInjected && e.Injected)
        {
            return false;
        }

        if (Settings.ModifierPolicy == ModifierPolicy.Ignore && (e.Control || e.Alt || e.Win))
        {
            return false;
        }

        if (IsSilenced(in e))
        {
            return false;
        }

        return e.Kind switch
        {
            KeyKind.Down => Settings.PlayOn is PlayOn.Down or PlayOn.Both,
            KeyKind.Up => Settings.PlayOn is PlayOn.Up or PlayOn.Both,
            KeyKind.Repeat => AllowsRepeat() && Settings.PlayOn is PlayOn.Down or PlayOn.Both,
            _ => false
        };
    }

    public bool IsSilenced(in KeyEvent e)
    {
        var name = KeyCatalog.NameOf(e.Key);
        if (Settings.SilentKeys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var vk = e.Key.VirtualKey;
        return Settings.SilentGroups.Any(group => KeyCatalog.InSilentGroup(vk, group));
    }

    private bool AllowsRepeat()
    {
        switch (Settings.Repeat)
        {
            case RepeatMode.Off:
                return false;
            case RepeatMode.On:
                return true;
            case RepeatMode.RateLimit:
                var minInterval = Stopwatch.Frequency / Math.Max(Settings.RepeatRateLimitHz, 0.1f);
                var now = _clock.ElapsedTicks;
                if (_lastRepeatFireTicks is { } last && now - last < minInterval)
                {
                    return false;
                }

                _lastRepeatFireTicks = now;
                return true;
            default:
                return false;
        }
    }
}
