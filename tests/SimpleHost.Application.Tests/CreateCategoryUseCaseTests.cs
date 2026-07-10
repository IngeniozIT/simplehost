using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.CreateCategory;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Tests;

public class CreateCategoryUseCaseTests
{
    [Fact]
    public void Creates_a_category_with_the_given_name_through_the_repository()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new CreateCategoryUseCase(repository);

        useCase.Execute("Work");

        repository.CreatedCategories.ShouldBe(new[] { "Work" });
    }

    [Fact]
    public void Returns_the_created_category_name()
    {
        var useCase = new CreateCategoryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("Home").ShouldBe("Home");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_name_before_creating_it()
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new CreateCategoryUseCase(repository);

        useCase.Execute("  Work  ");

        repository.CreatedCategories.ShouldBe(new[] { "Work" });
    }

    [Fact]
    public void Returns_the_trimmed_name()
    {
        var useCase = new CreateCategoryUseCase(new RecordingHostsFileRepository());

        useCase.Execute("  Home  ").ShouldBe("Home");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_name_that_is_empty_or_whitespace(string name)
    {
        var repository = new RecordingHostsFileRepository();
        var useCase = new CreateCategoryUseCase(repository);

        Should.Throw<ArgumentException>(() => useCase.Execute(name));
        repository.CreatedCategories.ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_a_null_name()
    {
        var useCase = new CreateCategoryUseCase(new RecordingHostsFileRepository());

        Should.Throw<ArgumentException>(() => useCase.Execute(null!));
    }

    private sealed class RecordingHostsFileRepository : IHostsFileRepository
    {
        private readonly List<string> _createdCategories = new();

        public IReadOnlyList<string> CreatedCategories => _createdCategories;

        public void CreateCategory(string name) => _createdCategories.Add(name);

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void UpdateEntry(HostEntry target, HostEntry replacement) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetActiveEntries() => throw new NotSupportedException();

        public IReadOnlyList<HostEntry> GetEntries() => throw new NotSupportedException();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => throw new NotSupportedException();
    }
}
