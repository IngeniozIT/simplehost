using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ImportHostsFile;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class ImportHostsFileUseCaseTests
{
    [Fact]
    public void Imports_each_parsed_entry_into_the_chosen_category_in_order()
    {
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        };
        var importer = new StubImporter(entries);
        var repository = new RecordingRepository(
            new CategoryGroup(null, Array.Empty<HostEntry>()),
            new CategoryGroup("Development", Array.Empty<HostEntry>()),
            new CategoryGroup("Blocklist", Array.Empty<HostEntry>()));
        var useCase = new ImportHostsFileUseCase(importer, repository);

        useCase.Execute("/tmp/external.hosts", "Development");

        importer.RequestedPath.ShouldBe("/tmp/external.hosts");
        repository.Added.ShouldBe(new[]
        {
            (entries[0], (string?)"Development"),
            (entries[1], (string?)"Development"),
        });
    }

    [Fact]
    public void Imports_into_the_no_category_group_when_the_category_is_null()
    {
        var entry = new HostEntry("127.0.0.1", "localhost");
        var importer = new StubImporter(new[] { entry });
        var repository = new RecordingRepository();
        var useCase = new ImportHostsFileUseCase(importer, repository);

        useCase.Execute("/tmp/external.hosts", null);

        repository.Added.ShouldBe(new[] { (entry, (string?)null) });
    }

    [Fact]
    public void Preserves_each_entrys_status_and_inline_comment()
    {
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "staging.test", Status: HostEntryStatus.Disabled),
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        };
        var importer = new StubImporter(entries);
        var repository = new RecordingRepository(new CategoryGroup("Blocklist", Array.Empty<HostEntry>()));
        var useCase = new ImportHostsFileUseCase(importer, repository);

        useCase.Execute("/tmp/external.hosts", "Blocklist");

        repository.Added.Select(added => added.Entry).ShouldBe(entries);
    }

    [Fact]
    public void Returns_the_imported_entries()
    {
        var entries = new[]
        {
            new HostEntry("127.0.0.1", "api.test"),
            new HostEntry("127.0.0.1", "web.test"),
        };
        var useCase = new ImportHostsFileUseCase(new StubImporter(entries), new RecordingRepository());

        useCase.Execute("/tmp/external.hosts", null).ShouldBe(entries);
    }

    [Fact]
    public void Imports_nothing_from_a_file_with_no_entries()
    {
        var importer = new StubImporter(Array.Empty<HostEntry>());
        var repository = new RecordingRepository();
        var useCase = new ImportHostsFileUseCase(importer, repository);

        useCase.Execute("/tmp/empty.hosts", null).ShouldBeEmpty();

        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_category_that_does_not_exist()
    {
        var importer = new StubImporter(new[] { new HostEntry("127.0.0.1", "api.test") });
        var repository = new RecordingRepository(new CategoryGroup("Development", Array.Empty<HostEntry>()));
        var useCase = new ImportHostsFileUseCase(importer, repository);

        var exception = Should.Throw<InvalidOperationException>(
            () => useCase.Execute("/tmp/external.hosts", "Nonexistent"));
        exception.Message.ShouldContain("Nonexistent");
        importer.WasCalled.ShouldBeFalse();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Matches_the_category_name_case_sensitively()
    {
        var importer = new StubImporter(new[] { new HostEntry("127.0.0.1", "api.test") });
        var repository = new RecordingRepository(new CategoryGroup("Development", Array.Empty<HostEntry>()));
        var useCase = new ImportHostsFileUseCase(importer, repository);

        Should.Throw<InvalidOperationException>(() => useCase.Execute("/tmp/external.hosts", "development"));
        importer.WasCalled.ShouldBeFalse();
        repository.Added.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_source_path_that_is_empty_or_whitespace(string sourcePath)
    {
        var importer = new StubImporter(new[] { new HostEntry("127.0.0.1", "api.test") });
        var repository = new RecordingRepository();
        var useCase = new ImportHostsFileUseCase(importer, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(sourcePath, null));
        importer.WasCalled.ShouldBeFalse();
        repository.Added.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_source_path()
    {
        var importer = new StubImporter(new[] { new HostEntry("127.0.0.1", "api.test") });
        var repository = new RecordingRepository();
        var useCase = new ImportHostsFileUseCase(importer, repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!, null));
        importer.WasCalled.ShouldBeFalse();
        repository.Added.ShouldBeEmpty();
    }

    private sealed class StubImporter(IReadOnlyList<HostEntry> entries) : IHostsFileImporter
    {
        public bool WasCalled { get; private set; }

        public string? RequestedPath { get; private set; }

        public IReadOnlyList<HostEntry> ImportEntries(string sourcePath)
        {
            WasCalled = true;
            RequestedPath = sourcePath;
            return entries;
        }
    }

    private sealed class RecordingRepository(params CategoryGroup[] groups) : IHostsFileRepository
    {
        private readonly List<(HostEntry Entry, string? Category)> _added = new();

        public IReadOnlyList<(HostEntry Entry, string? Category)> Added => _added;

        public void AddEntry(HostEntry entry, string? category = null) => _added.Add((entry, category));

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => groups;

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();
    }
}
