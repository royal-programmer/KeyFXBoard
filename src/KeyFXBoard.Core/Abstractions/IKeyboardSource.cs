using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Abstractions;

public interface IKeyboardSource : IDisposable
{
    bool IsAttached { get; }
    void Start(Action<KeyEvent> onEvent);
    void Stop();
}
