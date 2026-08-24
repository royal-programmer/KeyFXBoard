using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Tests;

public sealed class PackPathRulesTests
{
    [Theory]
    [InlineData("../secret.wav")]
    [InlineData("C:\\abs.wav")]
    [InlineData("/etc/passwd")]
    [InlineData("samples/../../x.wav")]
    public void Rejects_unsafe_paths(string path)
    {
        Assert.Throws<PackException>(() => PackPathRules.EnsureSafeRelativePath(path));
    }

    [Fact]
    public void Accepts_nested_relative_wav()
    {
        PackPathRules.EnsureSafeRelativePath("samples/default_down.wav");
    }

    [Theory]
    [InlineData("factory-click", true)]
    [InlineData("piano-classic", true)]
    [InlineData("custom-sample", true)]
    [InlineData("guns-arcade", false)]
    [InlineData("soft-tick", false)]
    public void Factory_ids_are_reserved(string id, bool expected)
    {
        Assert.Equal(expected, PackPathRules.IsFactoryId(id));
    }

    [Theory]
    [InlineData("piano-classic", true)]
    [InlineData("guns-arcade", true)]
    [InlineData("factory-click", false)]
    [InlineData("custom-sample", false)]
    public void Hidden_library_packs(string id, bool expected)
    {
        Assert.Equal(expected, PackPathRules.IsHiddenLibraryPack(id));
    }
}
