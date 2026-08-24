using KeyFXBoard.Core.Abstractions;

namespace KeyFXBoard.Core.Packs;

public sealed class FilePackStore
{
    private readonly IAppPaths _paths;

    public FilePackStore(IAppPaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(_paths.PacksDirectory);
    }

    public IReadOnlyList<InstalledPack> List()
    {
        if (!Directory.Exists(_paths.PacksDirectory))
        {
            return [];
        }

        var packs = new List<InstalledPack>();
        foreach (var dir in Directory.GetDirectories(_paths.PacksDirectory))
        {
            try
            {
                packs.Add(ReadInstalled(dir));
            }
            catch (PackException)
            {
                // Skip corrupt folders; the UI can still install a replacement.
            }
        }

        return packs
            .OrderByDescending(p => p.IsFactory)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public InstalledPack? Get(string id) =>
        List().FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public InstalledPack Install(string packFile, bool replaceExisting)
    {
        if (!File.Exists(packFile))
        {
            throw new PackException("Pack file was not found.");
        }

        if (!packFile.EndsWith(".kfxpack", StringComparison.OrdinalIgnoreCase) &&
            !packFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new PackException("Choose a .kfxpack file.");
        }

        var staging = Path.Combine(_paths.PacksDirectory, $".partial-{Guid.NewGuid():N}");
        try
        {
            PackArchive.Extract(packFile, staging);
            var manifest = PackArchive.ReadManifest(staging);
            PackManifestValidator.Validate(manifest, staging);

            var dest = Path.Combine(_paths.PacksDirectory, manifest.Id);
            if (Directory.Exists(dest))
            {
                if (!replaceExisting)
                {
                    throw new PackException($"Pack “{manifest.Id}” is already installed.");
                }

                DeleteDirectory(dest);
            }

            Directory.Move(staging, dest);
            return ReadInstalled(dest);
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                DeleteDirectory(staging);
            }

            throw;
        }
    }

    public void Uninstall(string id)
    {
        if (PackPathRules.IsFactoryId(id))
        {
            throw new PackException("Factory packs cannot be uninstalled.");
        }

        var dest = Path.Combine(_paths.PacksDirectory, id);
        if (!Directory.Exists(dest))
        {
            return;
        }

        DeleteDirectory(dest);
    }

    public InstalledPack ReadInstalled(string directory)
    {
        var manifest = PackArchive.ReadManifest(directory);
        PackManifestValidator.Validate(manifest, directory);
        return new InstalledPack
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            Author = manifest.Author,
            License = manifest.License,
            Description = manifest.Description,
            Directory = directory
        };
    }

    private static void DeleteDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
