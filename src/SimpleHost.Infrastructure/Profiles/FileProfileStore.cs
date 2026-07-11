using System.Text.Json;
using SimpleHost.Application.Ports;
using SimpleHost.Domain.Profiles;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Profiles;

/// <summary>
/// A persistent <see cref="IProfileStore"/> that keeps saved profiles in a JSON file, so they
/// survive a restart. This is what lets the built-in <c>"Default"</c> snapshot go on pointing at the
/// genuinely original <c>hosts</c> content across runs, and lets category presets the user saves be
/// offered again next time the app starts.
/// </summary>
/// <remarks>
/// The store keeps the profiles in memory for the life of the process and rewrites the whole file on
/// every save. Profile names are matched exactly (case-sensitively), and re-saving under an existing
/// name replaces the earlier profile in place. Only the raw snapshot content is persisted for a
/// snapshot profile; its grouping is rebuilt by parsing that content back when the store is loaded,
/// so the file on disk stays small and there is one source of truth for what the snapshot restores.
/// </remarks>
public sealed class FileProfileStore : IProfileStore
{
    private readonly string _filePath;
    private readonly HostsFileParser _parser;
    private readonly List<Profile> _profiles;

    /// <summary>
    /// Opens the store backed by the JSON file at <paramref name="filePath"/>, loading any profiles
    /// already saved there. A missing or empty file simply starts an empty store. Snapshot profiles
    /// are rebuilt from their persisted content using <paramref name="parser"/>.
    /// </summary>
    /// <param name="filePath">The path of the JSON file profiles are persisted to.</param>
    /// <param name="parser">The parser used to rebuild a snapshot profile's grouping from its content.</param>
    public FileProfileStore(string filePath, HostsFileParser parser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(parser);

        _filePath = filePath;
        _parser = parser;
        _profiles = Load();
    }

    public void Save(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profiles.RemoveAll(existing => existing.Name == profile.Name);
        _profiles.Add(profile);
        Persist();
    }

    public Profile? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _profiles.FirstOrDefault(profile => profile.Name == name);
    }

    public IReadOnlyList<Profile> GetAll() => _profiles.ToList();

    public void Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_profiles.RemoveAll(existing => existing.Name == name) > 0)
        {
            Persist();
        }
    }

    private List<Profile> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new List<Profile>();
        }

        var content = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<Profile>();
        }

        var stored = JsonSerializer.Deserialize<List<StoredProfile>>(content) ?? new List<StoredProfile>();
        return stored.Select(Rehydrate).ToList();
    }

    private Profile Rehydrate(StoredProfile stored)
        => stored.Snapshot is not null
            ? Profile.FromSnapshot(stored.Name, stored.Snapshot, _parser.ParseGroupedEntries(stored.Snapshot))
            : Profile.Of(stored.Name, stored.EnabledCategories);

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);

        var stored = _profiles
            .Select(profile => new StoredProfile(profile.Name, profile.EnabledCategories, profile.Snapshot))
            .ToList();
        File.WriteAllText(_filePath, JsonSerializer.Serialize(stored));
    }

    private sealed record StoredProfile(
        string Name, IReadOnlyList<string?> EnabledCategories, string? Snapshot);
}
