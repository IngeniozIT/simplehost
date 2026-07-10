using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryBackupTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private string BackupPath => $"{_path}.bak";

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Backs_up_the_current_contents_before_adding_an_entry()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        File.ReadAllText(BackupPath).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Backs_up_the_current_contents_before_updating_an_entry()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");

        NewRepository().UpdateEntry(
            new HostEntry("10.0.0.1", "api.myapp.test"),
            new HostEntry("192.168.1.1", "api.myapp.test"));

        File.ReadAllText(BackupPath).ShouldBe("127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");
    }

    [Fact]
    public void Backs_up_the_current_contents_before_deleting_an_entry()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n10.0.0.1 web.work\n");

        NewRepository().DeleteEntry(new HostEntry("10.0.0.1", "web.work"));

        File.ReadAllText(BackupPath).ShouldBe("127.0.0.1 localhost\n10.0.0.1 web.work\n");
    }

    [Fact]
    public void Backs_up_the_current_contents_before_moving_an_entry_between_categories()
    {
        File.WriteAllText(_path, "10.0.0.1 web.work\n# [SimpleHost:Category] Work\n");

        NewRepository().MoveEntryToCategory(new HostEntry("10.0.0.1", "web.work"), "Work");

        File.ReadAllText(BackupPath).ShouldBe("10.0.0.1 web.work\n# [SimpleHost:Category] Work\n");
    }

    [Fact]
    public void Refreshes_the_backup_before_every_write_so_it_holds_the_latest_previous_version()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");
        var repository = NewRepository();

        repository.AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));
        repository.AddEntry(new HostEntry("10.0.0.2", "api2.myapp.test"));

        File.ReadAllText(BackupPath).ShouldBe("127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }

        if (File.Exists(BackupPath))
        {
            File.Delete(BackupPath);
        }
    }
}
