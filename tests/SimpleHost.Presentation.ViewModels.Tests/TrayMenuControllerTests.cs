using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SimpleHost.Application.Ports;
using SimpleHost.Application.Tray;
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
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;
using SimpleHost.Domain.Subscriptions;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class TrayMenuControllerTests
{
    private static readonly HostEntry ActiveApi = new("10.0.0.1", "api.test", null, HostEntryStatus.Active);
    private static readonly HostEntry DisabledApp = new("10.0.0.2", "app.test", null, HostEntryStatus.Disabled);
    private static readonly HostEntry ActiveLocalhost = new("127.0.0.1", "localhost", null, HostEntryStatus.Active);

    [Fact]
    public void The_menu_offers_a_toggle_per_category_then_a_switch_per_profile_then_open_and_exit()
    {
        var viewModel = LoadedViewModel(
            new CategoryGroup(null, new[] { ActiveLocalhost }),
            new CategoryGroup("Work", new[] { ActiveApi }));
        viewModel.SaveProfile("Focus");
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.BuildMenu().Items.ShouldBe(new[]
        {
            new TrayMenuItem("Disable No category", TrayMenuItemKind.DisableCategory, null),
            new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"),
            new TrayMenuItem("Switch to Focus", TrayMenuItemKind.ActivateProfile, "Focus"),
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
    }

    [Fact]
    public void The_menu_offers_only_open_and_exit_when_there_is_nothing_to_toggle()
    {
        var controller = new TrayMenuController(LoadedViewModel(), new TrayMenuFactory());

        controller.BuildMenu().Items.ShouldBe(new[]
        {
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
    }

    [Fact]
    public void The_menu_names_the_running_app_as_its_tooltip()
    {
        var controller = new TrayMenuController(LoadedViewModel(), new TrayMenuFactory());

        controller.BuildMenu().Tooltip.ShouldBe("SimpleHost");
    }

    [Fact]
    public void A_category_with_a_disabled_entry_is_offered_an_enable_toggle_from_the_live_state()
    {
        var viewModel = LoadedViewModel(new CategoryGroup("Work", new[] { ActiveApi, DisabledApp }));
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.BuildMenu().Items.ShouldContain(
            new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"));
    }

    [Fact]
    public void Choosing_a_disable_toggle_disables_every_entry_of_that_category_straight_away()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup("Work", new[] { ActiveApi }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"));

        repository.EntriesIn("Work").ShouldAllBe(entry => entry.Status == HostEntryStatus.Disabled);
    }

    [Fact]
    public void Choosing_an_enable_toggle_enables_every_entry_of_that_category_straight_away()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup("Work", new[] { DisabledApp }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"));

        repository.EntriesIn("Work").ShouldAllBe(entry => entry.Status == HostEntryStatus.Active);
    }

    [Fact]
    public void Choosing_a_toggle_leaves_nothing_pending_because_it_is_applied_directly()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup("Work", new[] { ActiveApi }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"));

        viewModel.HasPendingChanges.ShouldBeFalse();
    }

    [Fact]
    public void Choosing_a_toggle_for_the_no_category_group_targets_the_uncategorized_entries()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup(null, new[] { ActiveLocalhost }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Disable No category", TrayMenuItemKind.DisableCategory, null));

        repository.EntriesIn(null).ShouldAllBe(entry => entry.Status == HostEntryStatus.Disabled);
    }

    [Fact]
    public void Choosing_a_profile_switch_enables_its_categories_and_disables_the_rest_straight_away()
    {
        var disabledHome = new HostEntry("10.0.0.3", "home.test", null, HostEntryStatus.Disabled);
        var repository = new InMemoryHostsFileRepository(
            new CategoryGroup("Work", new[] { ActiveApi }),
            new CategoryGroup("Home", new[] { disabledHome }));
        var viewModel = LoadedViewModel(repository);
        // Save a profile from the current state, where only "Work" is enabled — so recalling it must
        // re-enable "Work" and disable everything else.
        viewModel.SaveProfile("Focus");
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());
        // Diverge from the profile by turning "Home" on, so the switch has something to undo.
        controller.Apply(new TrayMenuItem("Enable Home", TrayMenuItemKind.EnableCategory, "Home"));

        controller.Apply(new TrayMenuItem("Switch to Focus", TrayMenuItemKind.ActivateProfile, "Focus"));

        repository.EntriesIn("Work").ShouldAllBe(entry => entry.Status == HostEntryStatus.Active);
        repository.EntriesIn("Home").ShouldAllBe(entry => entry.Status == HostEntryStatus.Disabled);
    }

    [Fact]
    public void After_disabling_a_category_the_menu_now_offers_to_enable_it()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup("Work", new[] { ActiveApi }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"));

        controller.BuildMenu().Items.ShouldContain(
            new TrayMenuItem("Enable Work", TrayMenuItemKind.EnableCategory, "Work"));
    }

    [Fact]
    public void Applying_a_toggle_updates_the_shared_view_model_the_window_binds_to()
    {
        var repository = new InMemoryHostsFileRepository(new CategoryGroup("Work", new[] { ActiveApi }));
        var viewModel = LoadedViewModel(repository);
        var controller = new TrayMenuController(viewModel, new TrayMenuFactory());

        controller.Apply(new TrayMenuItem("Disable Work", TrayMenuItemKind.DisableCategory, "Work"));

        viewModel.Groups.Single(group => group.Name == "Work")
            .Entries.ShouldAllBe(entry => !entry.IsActive);
    }

    [Fact]
    public void Applying_an_unsupported_menu_item_is_rejected()
    {
        var controller = new TrayMenuController(LoadedViewModel(), new TrayMenuFactory());

        Should.Throw<NotSupportedException>(() =>
                controller.Apply(new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow)))
            .Message.ShouldContain("Unsupported tray action");
    }

    [Fact]
    public void A_controller_needs_a_view_model()
    {
        Should.Throw<ArgumentNullException>(() => new TrayMenuController(null!, new TrayMenuFactory()));
    }

    [Fact]
    public void A_controller_needs_a_menu_factory()
    {
        Should.Throw<ArgumentNullException>(() => new TrayMenuController(LoadedViewModel(), null!));
    }

    [Fact]
    public void Applying_needs_a_menu_item()
    {
        var controller = new TrayMenuController(LoadedViewModel(), new TrayMenuFactory());

        Should.Throw<ArgumentNullException>(() => controller.Apply(null!));
    }

    private static MainViewModel LoadedViewModel(params CategoryGroup[] groups)
        => LoadedViewModel(new InMemoryHostsFileRepository(groups));

    private static MainViewModel LoadedViewModel(InMemoryHostsFileRepository repository)
    {
        var viewModel = new MainViewModel(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            new PreviewPendingChangesUseCase(repository),
            new ApplyPendingChangesUseCase(
                repository, new FakeElevationService(isElevated: true), new NoOpDnsCacheFlusher()),
            new DiscardPendingChangesUseCase(repository),
            new SearchEntriesByHostnameUseCase(repository),
            new SearchEntriesByIpAddressUseCase(repository),
            new FilterEntriesByCategoryUseCase(repository),
            new SaveProfileUseCase(new FakeProfileStore()),
            new SubscribeToRemoteHostsListUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionUseCase(new StubRemoteHostsListFetcher(), repository),
            new RefreshSubscriptionsUseCase(
                new StubSubscriptionCatalog(), new StubRemoteHostsListFetcher(), repository),
            new ExportCategoryUseCase(repository, new StubHostsFileExporter()),
            new ExportConfigurationUseCase(repository, new StubHostsFileExporter()),
            new ImportHostsFileUseCase(new StubHostsFileImporter(), repository),
            new PendingChangeSet());
        viewModel.Load();
        return viewModel;
    }

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

    private sealed class FakeProfileStore : IProfileStore
    {
        private readonly List<Profile> _saved = new();

        public void Save(Profile profile)
        {
            _saved.RemoveAll(existing => existing.Name == profile.Name);
            _saved.Add(profile);
        }

        public Profile? Find(string name) => _saved.FirstOrDefault(profile => profile.Name == name);
    }

    private sealed class FakeElevationService(bool isElevated) : IElevationService
    {
        public bool IsElevated { get; } = isElevated;
    }

    private sealed class NoOpDnsCacheFlusher : IDnsCacheFlusher
    {
        public void Flush()
        {
        }
    }

    // A real-enough hosts repository that keeps its grouped entries in memory and honours in-place
    // status replacements, so applying a tray toggle actually flips the stored entries and a later
    // read reflects it — letting these tests assert the observable outcome rather than a recorded call.
    private sealed class InMemoryHostsFileRepository : IHostsFileRepository
    {
        private List<CategoryGroup> _groups;

        public InMemoryHostsFileRepository(params CategoryGroup[] groups)
            => _groups = groups.ToList();

        public IReadOnlyList<HostEntry> EntriesIn(string? category)
            => _groups.Single(group => group.Name == category).Entries;

        public IReadOnlyList<CategoryGroup> GetGroupedEntries() => _groups;

        public IReadOnlyList<HostEntry> GetActiveEntries()
            => _groups.SelectMany(group => group.Entries)
                .Where(entry => entry.Status == HostEntryStatus.Active)
                .ToList();

        public IReadOnlyList<HostEntry> GetEntries()
            => _groups.SelectMany(group => group.Entries).ToList();

        public void UpdateEntry(HostEntry target, HostEntry replacement)
            => _groups = _groups
                .Select(group => new CategoryGroup(
                    group.Name,
                    group.Entries.Select(entry => entry == target ? replacement : entry).ToList()))
                .ToList();

        public void AddEntry(HostEntry entry, string? category = null) => throw new NotSupportedException();

        public void CreateCategory(string name) => throw new NotSupportedException();

        public void RenameCategory(string currentName, string newName) => throw new NotSupportedException();

        public void DeleteCategory(string name) => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory)
            => throw new NotSupportedException();

        public void MoveEntryToCategory(HostEntry entry, string? targetCategory, int targetIndex)
            => throw new NotSupportedException();

        public void ReorderEntry(HostEntry entry, int targetIndex) => throw new NotSupportedException();

        public void DeleteEntry(HostEntry entry) => throw new NotSupportedException();
    }
}
