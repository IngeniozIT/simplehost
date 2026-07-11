namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application whole-file access to the <c>hosts</c> file's raw text, as opposed to
/// the entry-by-entry operations of <see cref="IHostsFileRepository"/>. It exists for capturing and
/// restoring a verbatim snapshot of the file — reading the current content in one piece and replacing
/// it wholesale — so a "revert to the original file" can put back exactly what was there, comments
/// and blank lines included. Implemented by the same infrastructure adapter that backs the
/// repository, so a replace is preceded by the same automatic backup as any other write.
/// </summary>
public interface IHostsFileContentAccessor
{
    /// <summary>
    /// Reads and returns the entire current content of the <c>hosts</c> file as a single string,
    /// exactly as it is on disk, so it can be captured as a snapshot to restore later.
    /// </summary>
    /// <returns>The whole file content, verbatim.</returns>
    string ReadContent();

    /// <summary>
    /// Replaces the entire content of the <c>hosts</c> file with <paramref name="content"/>, written
    /// verbatim, after backing up the current file as every write does. Used to restore a captured
    /// snapshot so the file ends up byte for byte as it was when the snapshot was taken.
    /// </summary>
    /// <param name="content">The whole file content to write in place of the current content.</param>
    void ReplaceContent(string content);
}
