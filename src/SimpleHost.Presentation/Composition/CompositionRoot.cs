using System.Net.Http;
using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Application.Startup;
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
using SimpleHost.Application.UseCases.SeedDefaultProfile;
using SimpleHost.Application.UseCases.SearchEntriesByHostname;
using SimpleHost.Application.UseCases.SearchEntriesByIpAddress;
using SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;
using SimpleHost.Application.UseCases.ViewDuplicateEntries;
using SimpleHost.Application.UseCases.ViewGroupedEntries;
using SimpleHost.Application.UseCases.ViewHostnameConflicts;
using SimpleHost.Infrastructure.Dns;
using SimpleHost.Infrastructure.Elevation;
using SimpleHost.Infrastructure.Hosts;
using SimpleHost.Infrastructure.Profiles;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.Composition;

/// <summary>
/// The application's composition root: the single place where the concrete object graph behind the
/// main window is assembled. From the process command line (and environment) it resolves which
/// <c>hosts</c> file the app operates on, wires the file parser, serializer and repository at that
/// path, builds every use case the <see cref="MainViewModel"/> needs, and hands back a ready
/// view-model for the window to bind to.
/// </summary>
/// <remarks>
/// Elevation is deliberately <b>path-sensitive</b>. Writing the protected system <c>hosts</c> file
/// requires administrator rights, so for that path the real <see cref="WindowsElevationService"/>
/// gates the single write step. When the app is pointed at any other file — a local sample given via
/// <c>--hosts-file</c> or the <c>SIMPLEHOST_HOSTS_FILE</c> environment variable — the file is a
/// user-writable location, so no elevation is required and no elevation gate is imposed
/// (<see cref="WritableFileElevationService"/>). This is what lets the app run against a sample file
/// with no UAC prompt.
///
/// The root owns the <see cref="HttpClient"/> used to fetch remote subscription lists and disposes
/// it when the application exits.
/// </remarks>
internal sealed class CompositionRoot : IDisposable
{
    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Assembles the <see cref="MainViewModel"/> for the <c>hosts</c> file resolved from the given
    /// process arguments (falling back to the <c>SIMPLEHOST_HOSTS_FILE</c> environment variable and
    /// then the protected system file). The returned view-model is not yet loaded; the caller loads
    /// it once the UI is ready.
    /// </summary>
    /// <param name="commandLineArguments">
    /// The process command-line arguments (for example WPF's <c>StartupEventArgs.Args</c>), scanned
    /// for a <c>--hosts-file &lt;path&gt;</c> option.
    /// </param>
    /// <returns>A fully wired main view-model bound to the resolved <c>hosts</c> file.</returns>
    public MainViewModel BuildMainViewModel(IReadOnlyList<string> commandLineArguments)
    {
        ArgumentNullException.ThrowIfNull(commandLineArguments);

        var resolvedHostsFile = new HostsFilePathResolver()
            .Resolve(commandLineArguments, Environment.GetEnvironmentVariable);

        var parser = new HostsFileParser();
        var serializer = new HostsFileSerializer();
        var repository = new HostsFileRepository(resolvedHostsFile.Path, parser, serializer);

        IElevationService elevation = resolvedHostsFile.IsProtectedSystemFile
            ? new WindowsElevationService()
            : new WritableFileElevationService();

        var dnsCacheFlusher = new WindowsDnsCacheFlusher();
        var remoteFetcher = new RemoteHostsListFetcher(_httpClient, parser);
        var exporter = new HostsFileExporter(serializer);
        var importer = new HostsFileImporter(parser);
        var profileStore = new FileProfileStore(ProfileStorePath(), parser);
        var subscriptionCatalog = new InMemorySubscriptionCatalog();

        // Capture the original file as the "Default" snapshot the first time the app ever runs, before
        // anything is changed, so there is always a one-click way back to the genuinely original file.
        new SeedDefaultProfileUseCase(profileStore, repository, repository).Execute();

        var viewModel = new MainViewModel(
            new ViewGroupedEntriesUseCase(repository),
            new ViewHostnameConflictsUseCase(repository),
            new ViewDuplicateEntriesUseCase(repository),
            new PreviewPendingChangesUseCase(repository),
            new ApplyPendingChangesUseCase(repository, elevation, dnsCacheFlusher),
            new DiscardPendingChangesUseCase(repository),
            new SearchEntriesByHostnameUseCase(repository),
            new SearchEntriesByIpAddressUseCase(repository),
            new FilterEntriesByCategoryUseCase(repository),
            new SaveProfileUseCase(profileStore),
            new SubscribeToRemoteHostsListUseCase(remoteFetcher, repository),
            new RefreshSubscriptionUseCase(remoteFetcher, repository),
            new RefreshSubscriptionsUseCase(subscriptionCatalog, remoteFetcher, repository),
            new ExportCategoryUseCase(repository, exporter),
            new ExportConfigurationUseCase(repository, exporter),
            new ImportHostsFileUseCase(importer, repository),
            new PendingChangeSet());

        viewModel.LoadPersistedProfiles(profileStore.GetAll());
        return viewModel;
    }

    /// <summary>
    /// The path of the JSON file the persistent profile store keeps profiles in: a
    /// <c>profiles.json</c> under a per-user <c>SimpleHost</c> folder in local application data, so the
    /// "Default" snapshot and any saved presets survive a restart without touching the system file.
    /// </summary>
    private static string ProfileStorePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleHost",
            "profiles.json");

    public void Dispose() => _httpClient.Dispose();
}
