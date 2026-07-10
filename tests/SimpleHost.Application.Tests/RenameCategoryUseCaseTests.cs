using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RenameCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class RenameCategoryUseCaseTests
{
    [Fact]
    public void Renames_the_category_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        useCase.Execute("Work", "Office");

        repository.Renames.ShouldBe(new[] { ("Work", "Office") });
    }

    [Fact]
    public void Returns_the_new_category_name()
    {
        var useCase = new RenameCategoryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("Work", "Office").ShouldBe("Office");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_new_name_before_renaming()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        useCase.Execute("Work", "  Office  ");

        repository.Renames.ShouldBe(new[] { ("Work", "Office") });
    }

    [Fact]
    public void Returns_the_trimmed_new_name()
    {
        var useCase = new RenameCategoryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("Work", "  Office  ").ShouldBe("Office");
    }

    [Fact]
    public void Passes_the_current_name_through_unchanged()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        useCase.Execute("Work", "Office");

        repository.Renames.ShouldBe(new[] { ("Work", "Office") });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_new_name_that_is_empty_or_whitespace(string newName)
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("Work", newName));
        repository.Renames.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_new_name()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute("Work", null!));
        repository.Renames.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_current_name_that_is_empty_or_whitespace(string currentName)
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(currentName, "Office"));
        repository.Renames.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_current_name()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new RenameCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!, "Office"));
        repository.Renames.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<(string CurrentName, string NewName)> _renames = new();

        public IReadOnlyList<(string CurrentName, string NewName)> Renames => _renames;

        public void RenameCategory(string currentName, string newName)
            => _renames.Add((currentName, newName));

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
