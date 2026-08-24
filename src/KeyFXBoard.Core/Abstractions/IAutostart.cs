namespace KeyFXBoard.Core.Abstractions;

public interface IAutostart
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}
