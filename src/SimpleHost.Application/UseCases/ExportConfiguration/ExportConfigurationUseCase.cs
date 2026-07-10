using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.ExportConfiguration;

/// <summary>
/// Exports the entire hosts configuration — every category and its entries — as a single valid,
/// standalone <c>hosts</c> file on disk, so the whole set can be used elsewhere. The configuration
/// is read through the repository as its groups (the "No category" group first, then each category
/// in file order, carrying active and disabled entries alike in order) and handed to the exporter,
/// which writes them out mirroring the managed <c>hosts</c> file's own layout.
/// </summary>
public sealed class ExportConfigurationUseCase(IHostsFileRepository repository, IHostsFileExporter exporter)
{
    /// <summary>
    /// Exports the complete set of categories and entries to <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="destinationPath">
    /// The path of the standalone <c>hosts</c> file to write. Must not be empty or whitespace only.
    /// Any existing file at the destination is overwritten.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destinationPath"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public void Execute(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        exporter.ExportConfiguration(repository.GetGroupedEntries(), destinationPath);
    }
}
