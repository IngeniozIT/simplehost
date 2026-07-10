using Shouldly;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.ApplyPendingChanges;
using SimpleHost.Application.UseCases.DiscardPendingChanges;
using SimpleHost.Application.UseCases.ExportCategory;
using SimpleHost.Application.UseCases.ExportConfiguration;
using SimpleHost.Application.UseCases.FilterEntriesByCategory;
using SimpleHost.Application.UseCases.ImportHostsFile;
using SimpleHost.Application.UseCases.PreviewPendingChanges;
using SimpleHost.Application.UseCases.RefreshSubscription;
using SimpleHost.Application.UseCases.RefreshSubscriptions;
using SimpleHost.Application.UseCases.SaveProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;
using SimpleHost.Domain.Subscriptions;
using SimpleHost.Presentation.ViewModels;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation.ViewModels.Tests.DeepLinks;

public class MainViewModelDeepLinkTests
{
    [Fact]
    public void Stages_an_add_deep_link_and_surfaces_it_as_a_pending_change()
    {
        var viewModel = BuildViewModel(new SeededHostsFileRepository());

        var outcome = viewModel.HandleDeepLink("simplehost://add?ip=127.0.0.1&hostname=web.test");

        outcome.Staged.ShouldBeTrue();
        viewModel.PendingChangeCount.ShouldBe(1);
        viewModel.HasPendingChanges.ShouldBeTrue();
    }

    [Fact]
    public void Resolves_a_deep_link_against_the_current_entries_from_the_repository()
    {
        var repository = new SeededHostsFileRepository
        {
            GroupedEntries = new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }) },
        };
        var viewModel = BuildViewModel(repository);
        viewModel.DeepLinkConfirmation = _ => true;

        var outcome = viewModel.HandleDeepLink("simplehost://delete?hostname=web.test");

        outcome.Staged.ShouldBeTrue();
        outcome.Action.ShouldBe(DeepLinkAction.Delete);
        viewModel.PendingChangeCount.ShouldBe(1);
    }

    [Fact]
    public void Asks_the_deep_link_confirmation_before_staging_a_destructive_link()
    {
        var repository = new SeededHostsFileRepository
        {
            GroupedEntries = new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }) },
        };
        var viewModel = BuildViewModel(repository);
        DeepLinkConfirmationRequest? asked = null;
        viewModel.DeepLinkConfirmation = request => { asked = request; return false; };

        var outcome = viewModel.HandleDeepLink("simplehost://delete?hostname=web.test");

        asked.ShouldNotBeNull();
        asked!.Action.ShouldBe(DeepLinkAction.Delete);
        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.ConfirmationDeclined);
        viewModel.HasPendingChanges.ShouldBeFalse();
    }

    [Fact]
    public void Declines_a_destructive_link_when_no_confirmation_callback_is_wired()
    {
        var repository = new SeededHostsFileRepository
        {
            GroupedEntries = new[] { new CategoryGroup(null, new[] { new HostEntry("127.0.0.1", "web.test") }) },
        };
        var viewModel = BuildViewModel(repository);

        var outcome = viewModel.HandleDeepLink("simplehost://delete?hostname=web.test");

        outcome.Kind.ShouldBe(DeepLinkOutcomeKind.ConfirmationDeclined);
        viewModel.HasPendingChanges.ShouldBeFalse();
    }

    private static MainViewModel BuildViewModel(SeededHostsFileRepository repository) =>
        new(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            new PreviewPendingChangesUseCase(repository),
            new ApplyPendingChangesUseCase(
                repository, new FakeElevationService(), new FakeDnsCacheFlusher()),
            new DiscardPendingChangesUseCase(repository),
            new SearchEntriesByHostnameUseCase(repository),
            new SearchEntriesByIpAddressUseCase(repository),
            new FilterEntriesByCategoryUseCase(repository),
            new SaveProfileUseCase(new StubProfileStore()),
            new SubscribeToRemoteHostsListUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionsUseCase(
                new StubSubscriptionCatalog(), new StubRemoteHostsListFetcher(), repository),
            new ExportCategoryUseCase(repository, new StubHostsFileExporter()),
            new ExportConfigurationUseCase(repository, new StubHostsFileExporter()),
            new ImportHostsFileUseCase(new StubHostsFileImporter(), repository),
            new PendingChangeSet());

    private sealed class StubRemoteHostsListFetcher : IRemoteHostsListFetcher
    {
        public IReadOnlyList<HostEntry> Fetch(string url) => throw new NotSupportedException();
    }

    private sealed class StubSubscriptionCatalog : ISubscriptionCatalog
    {
        public IReadOnlyList<Subscription> GetSubscriptions() => Array.Empty<Subscription>();
    }

    private sealed class StubHostsFileExporter : IHostsFileExporter
    {
        public void ExportCategory(CategoryGroup group, string destinationPath)
            => throw new NotSupportedException();

        public void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath)
            => throw new NotSupportedException();
    }

    private sealed class StubHostsFileImporter : IHostsFileImporter
    {
        public IReadOnlyList<HostEntry> ImportEntries(string sourcePath) => throw new NotSupportedException();
    }

    private sealed class StubProfileStore : IProfileStore
    {
        public void Save(Profile profile)
        {
        }

        public Profile? Find(string name) => null;
    }

    private sealed class FakeElevationService : IElevationService
    {
        public bool IsElevated => true;
    }

    private sealed class FakeDnsCacheFlusher : IDnsCacheFlusher
    {
        public void Flush()
        {
        }
    }

    private sealed class SeededHostsFileRepository : IHostsFileRepository
    {
        public IReadOnlyList<CategoryGroup> GroupedEntries { get; set; } = Array.Empty<CategoryGroup>();

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => GroupedEntries;

        public IReadOnlyList<HostEntry> GetActiveEntries() => Array.Empty<HostEntry>();

        public IReadOnlyList<HostEntry> GetEntries() => Array.Empty<HostEntry>();

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

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
    }
}
