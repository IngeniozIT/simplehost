using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryAddEntryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Writes_the_new_entry_as_an_ip_and_hostname_line_in_the_file()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");
    }

    [Fact]
    public void The_added_entry_appears_among_the_active_entries()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");
        var repository = NewRepository();

        repository.AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        repository.GetActiveEntries().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("10.0.0.1", "api.myapp.test"),
        });
    }

    [Fact]
    public void The_added_entry_appears_among_all_entries_in_order()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");
        var repository = NewRepository();

        repository.AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        repository.GetEntries().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "localhost"),
            new HostEntry("10.0.0.1", "api.myapp.test"),
        });
    }

    [Fact]
    public void Starts_the_new_entry_on_its_own_line_when_the_file_has_no_trailing_newline()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost");

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("127.0.0.1 localhost\n10.0.0.1 api.myapp.test\n");
    }

    [Fact]
    public void Adds_the_entry_to_an_empty_file()
    {
        File.WriteAllText(_path, string.Empty);

        NewRepository().AddEntry(new HostEntry("10.0.0.1", "api.myapp.test"));

        File.ReadAllText(_path).ShouldBe("10.0.0.1 api.myapp.test\n");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
