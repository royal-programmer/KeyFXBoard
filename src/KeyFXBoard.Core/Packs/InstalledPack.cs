namespace KeyFXBoard.Core.Packs;

public sealed class InstalledPack
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required string License { get; init; }
    public required string Directory { get; init; }
    public string? Description { get; init; }
    public bool IsFactory => PackPathRules.IsFactoryId(Id);
}
