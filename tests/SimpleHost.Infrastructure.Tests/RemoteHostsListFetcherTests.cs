using System.Net;
using System.Text;
using Shouldly;
using SimpleHost.Domain.Entries;
using SimpleHost.Infrastructure.Hosts;

namespace SimpleHost.Infrastructure.Tests;

public sealed class RemoteHostsListFetcherTests
{
    [Fact]
    public void Fetches_the_list_from_the_given_url()
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 ads.example.com\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch("https://lists.example.com/adblock.txt");

        handler.RequestedUri.ShouldBe(new Uri("https://lists.example.com/adblock.txt"));
    }

    [Fact]
    public void Reads_the_active_and_disabled_entries_in_list_order()
    {
        var handler = new StubHttpMessageHandler(
            "0.0.0.0 ads.example.com\n" +
            "# 0.0.0.0 staging.example.com\n" +
            "127.0.0.1 local.example.com\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch("https://lists.example.com/adblock.txt").ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "staging.example.com", Status: HostEntryStatus.Disabled),
            new HostEntry("127.0.0.1", "local.example.com"),
        });
    }

    [Fact]
    public void Captures_trailing_inline_comments()
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 ads.example.com # blocked\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch("https://lists.example.com/adblock.txt").ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com", "blocked"),
        });
    }

    [Fact]
    public void Flattens_entries_regardless_of_category_markers_blank_lines_and_plain_comments()
    {
        var handler = new StubHttpMessageHandler(
            "# Title: Example ad-block list\n" +
            "0.0.0.0 ads.example.com\n" +
            "\n" +
            "# [SimpleHost:Category] Ignored\n" +
            "0.0.0.0 tracker.example.com\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch("https://lists.example.com/adblock.txt").ShouldBe(new[]
        {
            new HostEntry("0.0.0.0", "ads.example.com"),
            new HostEntry("0.0.0.0", "tracker.example.com"),
        });
    }

    [Fact]
    public void Returns_no_entries_for_a_list_with_only_comments_and_blank_lines()
    {
        var handler = new StubHttpMessageHandler("# just a header\n\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch("https://lists.example.com/adblock.txt").ShouldBeEmpty();
    }

    [Fact]
    public void Propagates_a_failure_status_from_the_remote_server()
    {
        var handler = new StubHttpMessageHandler(string.Empty, HttpStatusCode.NotFound);
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        Should.Throw<HttpRequestException>(() => fetcher.Fetch("https://lists.example.com/missing.txt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_url_that_is_empty_or_whitespace(string url)
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 ads.example.com\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        Should.Throw<ArgumentException>(() => fetcher.Fetch(url));
        handler.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_null_url()
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 ads.example.com\n");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        Should.Throw<ArgumentException>(() => fetcher.Fetch(null!));
        handler.WasCalled.ShouldBeFalse();
    }

    [Fact]
    public void Reads_the_entries_from_a_local_file_path_without_touching_the_network()
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 unused.example.com\n");
        var path = Path.Combine(Path.GetTempPath(), $"simplehost-fetch-{Guid.NewGuid():N}.txt");
        File.WriteAllText(
            path,
            "0.0.0.0 ads.example.com\n" +
            "# 0.0.0.0 staging.example.com\n" +
            "127.0.0.1 local.example.com # dev\n");
        try
        {
            var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

            fetcher.Fetch(path).ShouldBe(new[]
            {
                new HostEntry("0.0.0.0", "ads.example.com"),
                new HostEntry("0.0.0.0", "staging.example.com", Status: HostEntryStatus.Disabled),
                new HostEntry("127.0.0.1", "local.example.com", "dev"),
            });
            handler.WasCalled.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Yields_no_entries_for_a_local_file_path_that_does_not_exist_yet()
    {
        var handler = new StubHttpMessageHandler("0.0.0.0 unused.example.com\n");
        var path = Path.Combine(Path.GetTempPath(), $"simplehost-missing-{Guid.NewGuid():N}.txt");
        var fetcher = new RemoteHostsListFetcher(new HttpClient(handler), new HostsFileParser());

        fetcher.Fetch(path).ShouldBeEmpty();
        handler.WasCalled.ShouldBeFalse();
    }

    private sealed class StubHttpMessageHandler(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8),
            });
        }
    }
}
