using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Profiles;

namespace KeyFXBoard.Core.Tests;

public sealed class ProfileDirtyTests
{
    [Fact]
    public void Name_only_is_save_dirty_not_reset_dirty()
    {
        var checkpoint = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
        var working = ProfileCopy.Clone(checkpoint);
        working.Name = "Renamed";
        Assert.True(ProfileDirty.IsSaveDirty(working, checkpoint));
        Assert.False(ProfileDirty.IsResetDirty(working, checkpoint));
    }

    [Fact]
    public void Fx_change_is_reset_dirty()
    {
        var checkpoint = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
        var working = ProfileCopy.Clone(checkpoint);
        working.Fx.Reverb.Enabled = true;
        working.Fx.Reverb.Mix = 0.4f;
        Assert.True(ProfileDirty.IsResetDirty(working, checkpoint));
        Assert.True(ProfileDirty.IsSaveDirty(working, checkpoint));
    }

    [Fact]
    public void Catalog_clone_is_not_dirty()
    {
        var catalog = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
        var working = ProfileCopy.Clone(catalog);
        var checkpoint = ProfileCopy.Clone(catalog);
        Assert.False(ProfileDirty.IsSaveDirty(working, checkpoint));
        Assert.False(ProfileDirty.IsResetDirty(working, checkpoint));
    }

    [Fact]
    public void Apply_room_replaces_fx()
    {
        var profile = FactoryProfileSeeder.TryCatalog("factory-bass")!;
        Assert.True(profile.Fx.DynamicBass.Enabled);
        VirtualRoomCatalog.ApplyTo(profile, VirtualRoomCatalog.Hall);
        Assert.Equal(VirtualRoomCatalog.Hall, profile.VirtualRoomId);
        Assert.False(profile.Fx.DynamicBass.Enabled);
        Assert.True(profile.Fx.Reverb.Enabled);
    }
}

public sealed class FxGraphTests
{
    [Fact]
    public void Process_stays_finite_with_full_chain()
    {
        var fx = VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Surround);
        fx.DynamicBass.Enabled = true;
        fx.DynamicBass.Mix = 0.6f;
        fx.Chorus.Enabled = true;
        fx.Flanger.Enabled = true;
        fx.Phaser.Enabled = true;
        fx.Convolver.Enabled = true;
        var graph = FxGraph.Create(fx);
        var buffer = new float[512];
        buffer[0] = 0.2f;
        buffer[1] = -0.1f;
        graph.Process(buffer);
        Assert.All(buffer, v => Assert.True(float.IsFinite(v)));
    }

    [Fact]
    public void Every_virtual_room_processes_without_throwing()
    {
        foreach (var (id, _) in VirtualRoomCatalog.Rooms)
        {
            var graph = FxGraph.Create(VirtualRoomCatalog.CreateFx(id));
            var dest = new float[256];
            dest[0] = 0.2f;
            dest[1] = 0.2f;
            graph.Process(dest);
            Assert.Contains(dest, s => s != 0);
        }
    }
}
