using Shouldly;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class HostsFileRepositoryContentTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"simplehost-{Guid.NewGuid():N}.hosts");

    private string BackupPath => $"{_path}.bak";

    private HostsFileRepository NewRepository()
        => new(_path, new HostsFileParser(), new HostsFileSerializer());

    [Fact]
    public void Reads_the_whole_file_content_verbatim()
    {
        File.WriteAllText(_path, "# header\n\n127.0.0.1 localhost\n");

        NewRepository().ReadContent().ShouldBe("# header\n\n127.0.0.1 localhost\n");
    }

    [Fact]
    public void Replaces_the_whole_file_content_verbatim()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().ReplaceContent("# restored\n10.0.0.1 api.test\n");

        File.ReadAllText(_path).ShouldBe("# restored\n10.0.0.1 api.test\n");
    }

    [Fact]
    public void Backs_up_the_current_content_before_replacing_it()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        NewRepository().ReplaceContent("# restored\n");

        File.ReadAllText(BackupPath).ShouldBe("127.0.0.1 localhost\n");
    }

    [Fact]
    public void Rejects_a_null_replacement_content()
    {
        File.WriteAllText(_path, "127.0.0.1 localhost\n");

        Should.Throw<ArgumentNullException>(() => NewRepository().ReplaceContent(null!));
    }

    public void Dispose()
    {
        File.Delete(_path);
        File.Delete(BackupPath);
    }
}
