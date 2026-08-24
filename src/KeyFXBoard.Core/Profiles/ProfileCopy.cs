using System.Text.Json;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Profiles;

public static class ProfileCopy
{
    public static ProfileDocument Clone(ProfileDocument source) =>
        JsonSerializer.Deserialize<ProfileDocument>(
            JsonSerializer.Serialize(source, JsonOptions.File), JsonOptions.File)
        ?? throw new InvalidOperationException("Could not clone the profile.");
}

public static class ProfileDirty
{
    public static bool IsSaveDirty(ProfileDocument working, ProfileDocument checkpoint) =>
        Signature(working, includeName: true) != Signature(checkpoint, includeName: true);

    public static bool IsResetDirty(ProfileDocument working, ProfileDocument checkpoint) =>
        Signature(working, includeName: false) != Signature(checkpoint, includeName: false);

    private static string Signature(ProfileDocument source, bool includeName)
    {
        var copy = ProfileCopy.Clone(source);
        copy.Id = "";
        if (!includeName)
        {
            copy.Name = "";
        }

        return JsonSerializer.Serialize(copy, JsonOptions.File);
    }
}
