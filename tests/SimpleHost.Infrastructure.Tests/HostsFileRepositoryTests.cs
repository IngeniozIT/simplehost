using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    [Fact]
    public void Reads_the_active_entries_from_the_file_on_disk()
    {
        File.WriteAllText(_path,
            "# [SimpleHost:Category] Development\n" +
            "127.0.0.1 api.myapp.test\n" +
            "# 127.0.0.1 staging.myapp.test\n" +
            "0.0.0.0 ads.example.com # blocked\n");

        var repository = new HostsFileRepository(_path, new HostsFileParser());

        repository.GetActiveEntries().ShouldBe(new[]
        {
            new HostEntry("127.0.0.1", "api.myapp.test"),
            new HostEntry("0.0.0.0", "ads.example.com"),
        });
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
