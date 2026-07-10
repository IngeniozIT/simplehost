using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Exports a category to a standalone <c>hosts</c> file on disk. Renders the category's
/// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker (omitted for the "No category" group)
/// followed by its entries — using the same <see cref="HostsFileSerializer"/> the managed
/// <c>hosts</c> file is written with — so the exported file round-trips back through
/// <see cref="HostsFileParser"/> and can be used elsewhere.
/// </summary>
public sealed class HostsFileExporter(HostsFileSerializer serializer) : IHostsFileExporter
{
    public void ExportCategory(CategoryGroup group, string destinationPath)
    {
        var lines = new List<string>();

        if (group.Name is not null)
        {
            lines.Add(serializer.SerializeCategoryMarker(group.Name));
        }

        lines.AddRange(group.Entries.Select(serializer.SerializeEntry));

        File.WriteAllText(destinationPath, string.Concat(lines.Select(line => $"{line}\n")));
    }
}
