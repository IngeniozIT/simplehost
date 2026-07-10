using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.DeleteCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class DeleteCategoryUseCaseTests
{
    [Fact]
    public void Deletes_the_category_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteCategoryUseCase(repository);

        useCase.Execute("Work");

        repository.DeletedCategories.ShouldBe(new[] { "Work" });
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_name_before_deleting()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteCategoryUseCase(repository);

        useCase.Execute("  Work  ");

        repository.DeletedCategories.ShouldBe(new[] { "Work" });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_name_that_is_empty_or_whitespace(string name)
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(name));
        repository.DeletedCategories.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_name()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new DeleteCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(null!));
        repository.DeletedCategories.ShouldBeEmpty();
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        private readonly List<string> _deletedCategories = new();

        public IReadOnlyList<string> DeletedCategories => _deletedCategories;

        public void DeleteCategory(string name) => _deletedCategories.Add(name);

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();
        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
