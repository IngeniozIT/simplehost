using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ExportConfiguration;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ExportConfigurationUseCaseTests
{
    [Fact]
    public void Exports_every_group_read_from_the_repository_in_order_to_the_destination_path()
    {
        var noCategory = new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") });
        var development = new CategoryGroup("Development", new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        });
        var blocklist = new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.example.com") });
        var repository = new FakeHostsFileRepository(noCategory, development, blocklist);
        var exporter = new RecordingExporter();
        var useCase = new ExportConfigurationUseCase(repository, exporter);

        useCase.Execute("/tmp/all.hosts");

        exporter.ExportedGroups.ShouldBe(new[] { noCategory, development, blocklist });
        exporter.ExportedPath.ShouldBe("/tmp/all.hosts");
    }

    [Fact]
    public void Exports_the_empty_configuration_the_repository_reports()
    {
        var repository = new FakeHostsFileRepository(new CategoryGroup(null, Array.Empty<HostEntry>()));
        var exporter = new RecordingExporter();
        var useCase = new ExportConfigurationUseCase(repository, exporter);

        useCase.Execute("/tmp/empty.hosts");

        exporter.ExportedGroups.ShouldBe(new[] { new CategoryGroup(null, Array.Empty<HostEntry>()) });
        exporter.ExportedPath.ShouldBe("/tmp/empty.hosts");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_destination_path_that_is_empty_or_whitespace(string destinationPath)
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportConfigurationUseCase(repository, exporter);

        Should.Throw<ArgumentException>(() => useCase.Execute(destinationPath));
        exporter.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_null_destination_path()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportConfigurationUseCase(repository, exporter);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!));
        exporter.WasCalled.ShouldBeFalse();
    }

    private sealed class RecordingExporter : IHostsFileExporter
    {
        public bool WasCalled { get; private set; }

        public IReadOnlyList<CategoryGroup>? ExportedGroups { get; private set; }

        public string? ExportedPath { get; private set; }

        public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
        {
            WasCalled = true;
            ExportedGroups = groups;
            ExportedPath = destinationPath;
        }
    }

    private sealed class FakeHostsFileRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null)
            => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();
    }
}
