namespace KeyFXBoard.Core.Abstractions;

public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault)
{
    public const string DefaultId = "default";
}
