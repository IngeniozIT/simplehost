using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Exports the whole configuration to a standalone <c>hosts</c> file on disk. Renders each
/// category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker (omitted for the "No category"
/// group) followed by its entries — using the same <see cref="HostsFileSerializer"/> the managed
/// <c>hosts</c> file is written with — so the exported file round-trips back through
/// <see cref="HostsFileParser"/> and can be used elsewhere.
/// </summary>
public sealed class HostsFileExporter(HostsFileSerializer serializer) : IHostsFileExporter
{
    public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
        => Write(groups.SelectMany(RenderGroup), destinationPath);

    /// <summary>
    /// Renders <paramref name="group"/> as the <c>hosts</c>-file lines it occupies: its
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker (omitted when the group is the "No category"
    /// group, whose <see cref="CategoryGroup.Name"/> is <c>null</c>) followed by each of its entries.
    /// </summary>
    private IEnumerable<string> RenderGroup(CategoryGroup group)
    {
        if (group.Name is not null)
        {
            yield return serializer.SerializeCategoryMarker(group.Name);
        }

        foreach (var entry in group.Entries)
        {
            yield return serializer.SerializeEntry(entry);
        }
    }

    private static void Write(IEnumerable<string> lines, string destinationPath)
        => File.WriteAllText(destinationPath, string.Concat(lines.Select(line => $"{line}\n")));
}
