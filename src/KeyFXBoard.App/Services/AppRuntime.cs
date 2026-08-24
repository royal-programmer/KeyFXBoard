using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Hosting;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Profiles;
using KeyFXBoard.Core.Storage;
using KeyFXBoard.Windows;
using KeyFXBoard.Windows.Audio;
using KeyFXBoard.Windows.Autostart;
using KeyFXBoard.Windows.Hook;

namespace KeyFXBoard.App.Services;

public sealed class AppRuntime : IDisposable
{
    private readonly JsonSettingsStore _store;
    private readonly FilePackStore _packs;
    private readonly JsonProfileStore _profiles;
    private readonly IAutostart _autostart = new RunKeyAutostart();
    private readonly Dictionary<string, PackRuntime> _resident = new(StringComparer.OrdinalIgnoreCase);
    private ProfileDocument? _working;
    private ProfileDocument? _checkpoint;
    private string? _appliedPackFingerprint;
    private int _appliedPolyphony;
    private PackRuntime? _pianoPreview;

    public AppRuntime()
    {
        Paths = new AppPaths();
        _store = new JsonSettingsStore(Paths);
        _packs = new FilePackStore(Paths);
        _profiles = new JsonProfileStore(Paths);
        Settings = _store.Load();
        Engine = new Engine();
        Keyboard = new LowLevelKeyboardSource();
        Audio = new WasapiOutput();
        Audio.DevicesChanged += OnDevicesChanged;
    }

    public IAppPaths Paths { get; }
    public AppSettings Settings { get; }
    public Engine Engine { get; }
    public IKeyboardSource Keyboard { get; }
    public IAudioOutput Audio { get; }
    public string? HookError { get; private set; }
    public string? AudioError { get; private set; }
    public string? PackError { get; private set; }
    public string? AudioWarning { get; private set; }
    public bool IsElevated { get; } = ProcessElevation.IsElevated();

    public event Action? MuteChanged;
    public event Action? PacksChanged;
    public event Action? ProfilesChanged;
    public event Action? WorkingChanged;
    public event Action? AudioChanged;
    public event Action? InstrumentChanged;

    public bool PianoMode { get; private set; }

    public IReadOnlyList<InstalledPack> Packs
    {
        get
        {
            var list = _packs.List()
                .Where(p => !PackPathRules.IsHiddenLibraryPack(p.Id))
                .ToList();
            list.Insert(0, CustomSampleLibrary.CatalogEntry(Settings.ArmedSampleFile));
            return list;
        }
    }

    public IEnumerable<InstalledPack> PacksForPicker(string? selectedId)
    {
        foreach (var pack in Packs)
        {
            if (IsPackEnabled(pack.Id) || pack.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
            {
                yield return pack;
            }
        }
    }

    public bool IsPackEnabled(string id) =>
        !Settings.DisabledPackIds.Any(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));

    public void SetPackEnabled(string id, bool enabled)
    {
        Settings.DisabledPackIds.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (!enabled)
        {
            Settings.DisabledPackIds.Add(id);
        }

        Save();
        PacksChanged?.Invoke();
    }

    public IReadOnlyList<ProfileDocument> Profiles
    {
        get
        {
            var list = _profiles.List().Select(ProfileCopy.Clone).ToList();
            if (_working is null)
            {
                return list;
            }

            var index = list.FindIndex(p => p.Id.Equals(_working.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                list[index] = ProfileCopy.Clone(_working);
            }

            return list;
        }
    }

    public ProfileDocument? ActiveProfile => _working;
    public string ActivePackId => ActiveProfile?.PrimaryPackId ?? Settings.ActivePackId;
    public bool HasUnsavedChanges =>
        _working is not null && _checkpoint is not null &&
        (_working.IsFactory
            ? ProfileDirty.IsResetDirty(_working, _checkpoint)
            : ProfileDirty.IsSaveDirty(_working, _checkpoint));
    public bool CanSave => _working is { IsFactory: false } && HasUnsavedChanges;
    public bool CanReset =>
        _working is not null && _checkpoint is not null && ProfileDirty.IsResetDirty(_working, _checkpoint);
    public bool CanSaveAs => _working is not null;

    public void Start()
    {
        FactoryPackSeeder.Ensure(Paths);
        FactoryProfileSeeder.Ensure(Paths, _profiles);
        MigrateRetiredPacks();
        Settings.ActiveProfileId = FactoryProfileSeeder.MapId(Settings.ActiveProfileId);
        if (_profiles.Get(Settings.ActiveProfileId) is null)
        {
            Settings.ActiveProfileId = FactoryProfileSeeder.DefaultId;
        }

        try
        {
            FileAssociation.RegisterCurrentUser();
        }
        catch
        {
            // Association is optional.
        }

        Engine.Muted = Settings.GlobalMute;
        Engine.AppTrim = Settings.Volume;
        Engine.OutputBoost = MathF.Pow(10f, Math.Clamp(Settings.OutputBoostDb, 0f, AppSettings.MaxOutputBoostDb) / 20f);
        LoadSession(Settings.ActiveProfileId, notify: false);
        StartAudio();

        try
        {
            Keyboard.Start(Engine.Handle);
        }
        catch (Exception ex)
        {
            HookError = ex.Message;
        }
    }

    public IReadOnlyList<AudioDeviceInfo> ListAudioDevices() => Audio.ListDevices();

    private void OnDevicesChanged()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var devices = Audio.ListDevices();
                var wanted = string.IsNullOrWhiteSpace(Settings.AudioDeviceId)
                    ? AudioDeviceInfo.DefaultId
                    : Settings.AudioDeviceId;
                var followDefault = wanted.Equals(AudioDeviceInfo.DefaultId, StringComparison.OrdinalIgnoreCase);
                var missing = !devices.Any(d => d.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (followDefault || missing)
                {
                    StartAudio();
                }

                AudioChanged?.Invoke();
            }
            catch
            {
                // Device enumeration can race with unplug.
            }
        });
    }

    public void SetAudioDevice(string deviceId)
    {
        Settings.AudioDeviceId = string.IsNullOrWhiteSpace(deviceId) ? AudioDeviceInfo.DefaultId : deviceId;
        Save();
        StartAudio();
        AudioChanged?.Invoke();
    }

    public void SetPrimaryPack(string packId)
    {
        if (_working is null)
        {
            return;
        }

        _working.PrimaryPackId = packId;
        NotifyWorkingChanged();
        PacksChanged?.Invoke();
    }

    public void ArmSample(string fileName)
    {
        Settings.ArmedSampleFile = Path.GetFileName(fileName);
        _resident.Remove(CustomSampleLibrary.PackId);
        Save();
        ApplyWorking(forceReload: true);
        PacksChanged?.Invoke();
    }

    public void PreviewCustomSample(string fileName)
    {
        var path = CustomSampleLibrary.ResolveArmed(Paths, fileName);
        if (path is null)
        {
            throw new PackException("That file is not in the custom samples folder.");
        }

        Engine.Play(WavDecoder.DecodeAny(path, $"{CustomSampleLibrary.PackId}:{Path.GetFileName(path)}"));
    }

    public void StartPiano()
    {
        PianoMode = true;
        Engine.PianoVisualEnabled = true;
        ApplyWorking(forceReload: true);
        InstrumentChanged?.Invoke();
    }

    public void StopPiano()
    {
        PianoMode = false;
        Engine.PianoVisualEnabled = false;
        ApplyWorking(forceReload: true);
        InstrumentChanged?.Invoke();
    }

    private void StartAudio()
    {
        AudioError = null;
        AudioWarning = null;
        try
        {
            Audio.Start(Engine.FillBuffer, Settings.AudioDeviceId);
            AudioWarning = Audio.StartWarning;
        }
        catch (Exception ex)
        {
            AudioError = ex.Message;
        }
    }

    public void SetMuted(bool muted)
    {
        Settings.GlobalMute = muted;
        Engine.Muted = muted;
        Save();
        MuteChanged?.Invoke();
    }

    public void SetVolume(float volume)
    {
        Settings.Volume = volume;
        Engine.AppTrim = volume;
        Save();
    }

    public void SetOutputBoostDb(float db)
    {
        Settings.OutputBoostDb = Math.Clamp(db, 0f, AppSettings.MaxOutputBoostDb);
        Engine.OutputBoost = DbToGain(Settings.OutputBoostDb);
        Save();
    }

    public void ResetAppSettings()
    {
        Settings.Autostart = false;
        Settings.GlobalMute = false;
        Settings.MinimizeToTrayOnClose = true;
        Settings.StartMinimized = true;
        Settings.Volume = 0.7f;
        Settings.AudioDeviceId = AudioDeviceInfo.DefaultId;
        Settings.OutputBoostDb = 0;
        Engine.Muted = false;
        Engine.AppTrim = Settings.Volume;
        Engine.OutputBoost = 1f;
        SetAutostart(false);
        StartAudio();
        Save();
        MuteChanged?.Invoke();
        AudioChanged?.Invoke();
    }

    public void RemoveLocalData()
    {
        Engine.StopAllVoices();
        try
        {
            SetAutostart(false);
        }
        catch
        {
            // Autostart cleanup is best-effort.
        }

        if (Directory.Exists(Paths.Root))
        {
            Directory.Delete(Paths.Root, recursive: true);
        }
    }

    private static float DbToGain(float db) => MathF.Pow(10f, Math.Clamp(db, 0f, AppSettings.MaxOutputBoostDb) / 20f);

    public void SetVelocityRandom(float value)
    {
        if (_working is null)
        {
            return;
        }

        _working.Behavior.VelocityRandom = value;
        NotifyWorkingChanged();
    }

    public void SetAutostart(bool enabled)
    {
        Settings.Autostart = enabled;
        _autostart.SetEnabled(enabled);
        Save();
    }

    public void SetMinimizeToTrayOnClose(bool enabled)
    {
        Settings.MinimizeToTrayOnClose = enabled;
        Save();
    }

    public void SetStartMinimized(bool enabled)
    {
        Settings.StartMinimized = enabled;
        Save();
    }

    public void CompleteFirstRun(bool autostart)
    {
        Settings.FirstRunCompleted = true;
        Settings.StartMinimized = true;
        Save();
        SetAutostart(autostart);
    }

    public InstalledPack InstallPack(string packFile, bool replaceExisting)
    {
        var installed = _packs.Install(packFile, replaceExisting);
        try
        {
            _ = PackLoader.Load(installed.Directory, WavDecoder.Decode);
        }
        catch
        {
            _packs.Uninstall(installed.Id);
            throw;
        }

        PacksChanged?.Invoke();
        ApplyWorking(forceReload: true);
        return installed;
    }

    public void UninstallPack(string id)
    {
        Engine.StopAllVoices();
        _packs.Uninstall(id);
        _resident.Remove(id);
        _profiles.RewritePackReferences(id, FactoryPackSeeder.FactoryId);
        if (_working is not null)
        {
            RewritePack(_working, id, FactoryPackSeeder.FactoryId);
        }

        if (_checkpoint is { IsFactory: false })
        {
            var saved = _profiles.Get(_checkpoint.Id);
            if (saved is not null)
            {
                _checkpoint = ProfileCopy.Clone(saved);
            }
        }

        ApplyWorking(forceReload: true);
        PacksChanged?.Invoke();
        ProfilesChanged?.Invoke();
        WorkingChanged?.Invoke();
    }

    public void ActivateProfile(string id)
    {
        LoadSession(id, notify: true);
    }

    public void ApplyVirtualRoom(string roomId)
    {
        if (_working is null || _working.FxLocked)
        {
            return;
        }

        VirtualRoomCatalog.ApplyTo(_working, roomId);
        NotifyWorkingChanged();
    }

    public void NotifyWorkingChanged()
    {
        if (_working is null)
        {
            return;
        }

        ApplyWorking(forceReload: false);
        WorkingChanged?.Invoke();
    }

    public void SaveWorking()
    {
        if (_working is not { IsFactory: false })
        {
            throw new InvalidOperationException("System profiles cannot be saved. Save as a new profile.");
        }

        _profiles.Save(_working);
        _checkpoint = ProfileCopy.Clone(_working);
        ProfilesChanged?.Invoke();
        WorkingChanged?.Invoke();
    }

    public ProfileDocument SaveWorkingAs(string name)
    {
        var source = _working ?? throw new InvalidOperationException("No active profile.");
        var error = FactoryProfileSeeder.ValidateUserProfileName(name, _profiles.List());
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var copy = _profiles.Duplicate(source, name.Trim());
        LoadSession(copy.Id, notify: true);
        return copy;
    }

    public void RenameProfile(string id, string name)
    {
        var error = FactoryProfileSeeder.ValidateUserProfileName(name, _profiles.List(), id);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }

        var trimmed = name.Trim();
        if (_working is not null && _working.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        {
            if (_working.IsFactory)
            {
                throw new InvalidOperationException("System profiles cannot be renamed. Save as a new profile.");
            }

            _working.Name = trimmed;
            NotifyWorkingChanged();
            return;
        }

        var doc = _profiles.Get(id) ?? throw new InvalidOperationException("Profile not found.");
        if (doc.IsFactory)
        {
            throw new InvalidOperationException("System profiles cannot be renamed.");
        }

        doc.Name = trimmed;
        _profiles.Save(doc);
        ProfilesChanged?.Invoke();
    }

    public void ResetWorking()
    {
        if (_checkpoint is null)
        {
            return;
        }

        _working = ProfileCopy.Clone(_checkpoint);
        ApplyWorking(forceReload: true);
        ProfilesChanged?.Invoke();
        WorkingChanged?.Invoke();
    }

    public ProfileDocument DuplicateActive(string? name = null)
    {
        var source = _working ?? throw new InvalidOperationException("No active profile.");
        var copy = _profiles.Duplicate(source, name ?? source.Name + " copy");
        LoadSession(copy.Id, notify: true);
        return copy;
    }

    public ProfileDocument Duplicate(string id)
    {
        var source = id.Equals(_working?.Id, StringComparison.OrdinalIgnoreCase)
            ? _working!
            : _profiles.Get(id) ?? throw new InvalidOperationException("Profile not found.");
        var copy = _profiles.Duplicate(source, source.Name + " copy");
        ProfilesChanged?.Invoke();
        return copy;
    }

    public void DeleteProfile(string id)
    {
        _profiles.Delete(id);
        if (_working is not null && _working.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        {
            LoadSession(FactoryProfileSeeder.DefaultId, notify: true);
            return;
        }

        ProfilesChanged?.Invoke();
    }

    public void PreviewPack(string id)
    {
        var pack = _packs.Get(id) ?? throw new PackException("That pack is not installed.");
        var runtime = PackLoader.Load(pack.Directory, WavDecoder.Decode);
        var sample = runtime.Preview
                     ?? runtime.GetNote("C4")
                     ?? runtime.GetNote("C3")
                     ?? runtime.Resolve(
                         new KeyFXBoard.Core.Keys.KeyEvent(
                             KeyFXBoard.Core.Keys.KeyId.FromVirtualKey(0x51),
                             KeyFXBoard.Core.Keys.KeyKind.Down,
                             false, false, false, false),
                         Engine.Filter.Settings.VariantMode)
                     ?? runtime.Resolve(
                         new KeyFXBoard.Core.Keys.KeyEvent(
                             KeyFXBoard.Core.Keys.KeyId.FromVirtualKey(0x41),
                             KeyFXBoard.Core.Keys.KeyKind.Down,
                             false, false, false, false),
                         Engine.Filter.Settings.VariantMode);
        if (sample is not null)
        {
            Engine.Play(sample);
        }
    }

    public void PlayPianoNote(string note)
    {
        var sample = PianoPack().GetNote(note);
        if (sample is not null)
        {
            Engine.Play(sample);
        }
    }

    private PackRuntime PianoPack()
    {
        if (_resident.TryGetValue(ThemePackSeeder.PianoId, out var resident))
        {
            return resident;
        }

        if (_pianoPreview is not null)
        {
            return _pianoPreview;
        }

        var pack = _packs.Get(ThemePackSeeder.PianoId)
                   ?? throw new PackException("Piano is not installed.");
        _pianoPreview = PackLoader.Load(pack.Directory, WavDecoder.Decode);
        return _pianoPreview;
    }

    public bool TryConsumePendingInstall(out string? packFile)
    {
        packFile = null;
        if (!File.Exists(Paths.PendingInstallFile))
        {
            return false;
        }

        packFile = File.ReadAllText(Paths.PendingInstallFile).Trim();
        File.Delete(Paths.PendingInstallFile);
        return !string.IsNullOrWhiteSpace(packFile) && File.Exists(packFile);
    }

    public void Save() => _store.Save(Settings);

    public void Dispose()
    {
        Audio.DevicesChanged -= OnDevicesChanged;
        Keyboard.Dispose();
        Engine.StopAllVoices();
        Audio.Dispose();
    }

    private void LoadSession(string id, bool notify)
    {
        var doc = _profiles.Get(id) ?? _profiles.Get(FactoryProfileSeeder.DefaultId);
        if (doc is null)
        {
            _working = null;
            _checkpoint = null;
            Engine.SetClick(ClickSampleFactory.Create());
            PackError = "No profile is available.";
            return;
        }

        _working = ProfileCopy.Clone(doc);
        if (doc.IsFactory)
        {
            var catalog = FactoryProfileSeeder.TryCatalog(doc.Id) ?? doc;
            _working = ProfileCopy.Clone(catalog);
            _checkpoint = ProfileCopy.Clone(catalog);
        }
        else
        {
            _checkpoint = ProfileCopy.Clone(doc);
        }

        Settings.ActiveProfileId = doc.Id;
        Save();
        ApplyWorking(forceReload: true);
        if (notify)
        {
            ProfilesChanged?.Invoke();
            WorkingChanged?.Invoke();
        }
    }

    private void ApplyWorking(bool forceReload)
    {
        if (_working is null)
        {
            return;
        }

        PackError = null;
        var source = PianoMode ? InstrumentDocument(_working) : _working;
        var fingerprint = (PianoMode ? "piano|" : "") + PackFingerprint(source) + "|" + (Settings.ArmedSampleFile ?? "");
        var polyphony = Math.Clamp(source.Behavior.Polyphony, 1, 64);
        if (!forceReload && fingerprint == _appliedPackFingerprint && polyphony == _appliedPolyphony)
        {
            Engine.ApplyLive(source);
            Engine.AppTrim = Settings.Volume;
            Engine.OutputBoost = MathF.Pow(10f, Math.Clamp(Settings.OutputBoostDb, 0f, AppSettings.MaxOutputBoostDb) / 20f);
            Engine.PianoVisualEnabled = PianoMode;
            return;
        }

        _resident.Clear();
        var fallback = LoadResident(FactoryPackSeeder.FactoryId)
                       ?? PackRuntime.SingleSample("factory-click", "Factory Click", ClickSampleFactory.Create());
        var snapshot = ProfileCompiler.Compile(source, LoadResident, fallback, out var warning);
        Engine.ApplyProfile(snapshot);
        Engine.PianoVisualEnabled = PianoMode;
        Engine.AppTrim = Settings.Volume;
        Engine.OutputBoost = MathF.Pow(10f, Math.Clamp(Settings.OutputBoostDb, 0f, AppSettings.MaxOutputBoostDb) / 20f);
        Settings.ActivePackId = _working.PrimaryPackId;
        _appliedPackFingerprint = fingerprint;
        _appliedPolyphony = snapshot.Polyphony;
        Save();
        PackError = warning;
    }

    private static ProfileDocument InstrumentDocument(ProfileDocument source)
    {
        var doc = ProfileCopy.Clone(source);
        doc.PrimaryPackId = ThemePackSeeder.PianoId;
        doc.Overlays = [];
        doc.Behavior.HoldSustain = true;
        doc.Behavior.SilenceUnmapped = true;
        doc.Behavior.ForceSampleKey = null;
        if (doc.Behavior.SilentGroups.Count == 0)
        {
            doc.Behavior.SilentGroups = ["function", "modifiers", "numpad", "navigation"];
        }

        return doc;
    }

    private void MigrateRetiredPacks()
    {
        foreach (var id in PackPathRules.RetiredPackIds.Append(ThemePackSeeder.PianoId))
        {
            _profiles.RewritePackReferences(id, FactoryPackSeeder.FactoryId);
        }

        Settings.DisabledPackIds.RemoveAll(PackPathRules.IsHiddenLibraryPack);
        if (PackPathRules.IsHiddenLibraryPack(Settings.ActivePackId) ||
            Settings.ActivePackId.Equals(ThemePackSeeder.PianoId, StringComparison.OrdinalIgnoreCase))
        {
            Settings.ActivePackId = FactoryPackSeeder.FactoryId;
        }
    }

    private static void RewritePack(ProfileDocument profile, string removedPackId, string fallbackPackId)
    {
        if (profile.PrimaryPackId.Equals(removedPackId, StringComparison.OrdinalIgnoreCase))
        {
            profile.PrimaryPackId = fallbackPackId;
        }

        profile.Overlays = profile.Overlays
            .Where(o => !o.PackId.Equals(removedPackId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string PackFingerprint(ProfileDocument doc)
    {
        var overlays = string.Join(';', doc.Overlays.Select(o =>
            o.PackId + ":" + string.Join(',', o.Keys)));
        return doc.PrimaryPackId + "|" + overlays;
    }

    private PackRuntime? LoadResident(string id)
    {
        if (_resident.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (id.Equals(CustomSampleLibrary.PackId, StringComparison.OrdinalIgnoreCase))
        {
            var path = CustomSampleLibrary.ResolveArmed(Paths, Settings.ArmedSampleFile);
            SampleBuffer? sample = null;
            if (path is not null)
            {
                try
                {
                    sample = WavDecoder.DecodeAny(path, $"{CustomSampleLibrary.PackId}:{Path.GetFileName(path)}");
                    Settings.ArmedSampleFile = Path.GetFileName(path);
                }
                catch (Exception ex)
                {
                    PackError = ex.Message;
                }
            }

            var custom = CustomSampleLibrary.CreatePack(path, sample);
            _resident[id] = custom;
            return custom;
        }

        var installed = _packs.Get(id);
        if (installed is null)
        {
            return null;
        }

        var runtime = PackLoader.Load(installed.Directory, WavDecoder.Decode);
        _resident[id] = runtime;
        return runtime;
    }
}
