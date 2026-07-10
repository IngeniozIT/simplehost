using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ExportCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ExportCategoryUseCaseTests
{
    [Fact]
    public void Exports_the_chosen_category_group_to_the_destination_path()
    {
        var development = new CategoryGroup("Development", new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        });
        var repository = new FakeHostsFileRepository(
            new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") }),
            development,
            new CategoryGroup("Blocklist", new[] { new HostEntry("0.0.0.0", "ads.example.com") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        useCase.Execute("Development", "/tmp/dev.hosts");

        exporter.ExportedGroup.ShouldBe(development);
        exporter.ExportedPath.ShouldBe("/tmp/dev.hosts");
    }

    [Fact]
    public void Exports_the_no_category_group_when_the_category_is_null()
    {
        var noCategory = new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "localhost") });
        var repository = new FakeHostsFileRepository(
            noCategory,
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        useCase.Execute(null, "/tmp/no-category.hosts");

        exporter.ExportedGroup.ShouldBe(noCategory);
        exporter.ExportedPath.ShouldBe("/tmp/no-category.hosts");
    }

    [Fact]
    public void Exports_an_empty_category_as_just_its_marker()
    {
        var empty = new CategoryGroup("Empty", Array.Empty<HostEntry>());
        var repository = new FakeHostsFileRepository(empty);
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        useCase.Execute("Empty", "/tmp/empty.hosts");

        exporter.ExportedGroup.ShouldBe(empty);
    }

    [Fact]
    public void Rejects_a_category_that_does_not_exist()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        Should.Throw<InvalidOperationException>(() => useCase.Execute("Nonexistent", "/tmp/x.hosts"));
        exporter.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Matches_the_category_name_case_sensitively()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        Should.Throw<InvalidOperationException>(() => useCase.Execute("development", "/tmp/x.hosts"));
        exporter.WasCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_destination_path_that_is_empty_or_whitespace(string destinationPath)
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        Should.Throw<ArgumentException>(() => useCase.Execute("Development", destinationPath));
        exporter.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_null_destination_path()
    {
        var repository = new FakeHostsFileRepository(
            new CategoryGroup("Development", new[] { new HostEntry("127.0.0.1", "api.test") }));
        var exporter = new RecordingExporter();
        var useCase = new ExportCategoryUseCase(repository, exporter);

        Should.Throw<ArgumentException>(() => useCase.Execute("Development", null!));
        exporter.WasCalled.ShouldBeFalse();
    }

    private sealed class RecordingExporter : IHostsFileExporter
    {
        public bool WasCalled { get; private set; }

        public CategoryGroup? ExportedGroup { get; private set; }

        public string? ExportedPath { get; private set; }

        public void ExportCategory(CategoryGroup group, string destinationPath)
        {
            WasCalled = true;
            ExportedGroup = group;
            ExportedPath = destinationPath;
        }

        public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
            => throw new NotSupportedException();
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
