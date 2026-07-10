using Shouldly;
using SimpleHost.Application.Startup;

namespace SimpleHost.Application.Tests;

public class HostsFilePathResolverTests
{
    private const string SystemPath = @"C:\Windows\System32\drivers\etc\hosts";

    private static Func<string, string?> NoEnvironment => _ => null;

    private static Func<string, string?> Environment(string name, string? value)
        => key => key == name ? value : null;

    private static ResolvedHostsFile Resolve(Func<string, string?> environment, params string[] args)
        => new HostsFilePathResolver().Resolve(args, environment);

    [Fact]
    public void Falls_back_to_the_default_windows_system_path_when_nothing_is_specified()
    {
        var resolved = Resolve(NoEnvironment);

        resolved.Path.ShouldBe(SystemPath);
    }

    [Fact]
    public void Marks_the_default_system_path_as_the_protected_system_file()
    {
        var resolved = Resolve(NoEnvironment);

        resolved.IsProtectedSystemFile.ShouldBeTrue();
    }

    [Fact]
    public void Exposes_the_default_windows_system_path_as_a_constant()
    {
        HostsFilePathResolver.DefaultWindowsHostsFilePath.ShouldBe(SystemPath);
    }

    [Fact]
    public void Resolves_the_path_given_by_the_hosts_file_argument()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", @"D:\samples\hosts");

        resolved.Path.ShouldBe(@"D:\samples\hosts");
    }

    [Fact]
    public void Treats_a_custom_file_from_the_argument_as_not_the_protected_system_file()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", @"D:\samples\hosts");

        resolved.IsProtectedSystemFile.ShouldBeFalse();
    }

    [Fact]
    public void Resolves_the_path_given_by_the_environment_variable_when_no_argument_is_present()
    {
        var resolved = Resolve(Environment("SIMPLEHOST_HOSTS_FILE", @"D:\env\hosts"));

        resolved.Path.ShouldBe(@"D:\env\hosts");
    }

    [Fact]
    public void Treats_a_custom_file_from_the_environment_as_not_the_protected_system_file()
    {
        var resolved = Resolve(Environment("SIMPLEHOST_HOSTS_FILE", @"D:\env\hosts"));

        resolved.IsProtectedSystemFile.ShouldBeFalse();
    }

    [Fact]
    public void Prefers_the_argument_over_the_environment_variable_when_both_are_present()
    {
        var resolved = Resolve(
            Environment("SIMPLEHOST_HOSTS_FILE", @"D:\env\hosts"),
            "--hosts-file", @"D:\arg\hosts");

        resolved.Path.ShouldBe(@"D:\arg\hosts");
    }

    [Fact]
    public void Prefers_the_environment_variable_over_the_default_path()
    {
        var resolved = Resolve(Environment("SIMPLEHOST_HOSTS_FILE", @"D:\env\hosts"));

        resolved.Path.ShouldNotBe(SystemPath);
    }

    [Fact]
    public void Ignores_environment_variables_other_than_the_hosts_file_one()
    {
        var resolved = Resolve(Environment("PATH", @"D:\other\hosts"));

        resolved.Path.ShouldBe(SystemPath);
    }

    [Fact]
    public void Recognises_the_system_path_reached_through_the_argument_as_protected()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", SystemPath);

        resolved.IsProtectedSystemFile.ShouldBeTrue();
    }

    [Fact]
    public void Recognises_the_system_path_case_insensitively_as_protected()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", @"c:\windows\system32\drivers\etc\hosts");

        resolved.IsProtectedSystemFile.ShouldBeTrue();
    }

    [Fact]
    public void Recognises_the_system_path_written_with_forward_slashes_as_protected()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", "C:/Windows/System32/drivers/etc/hosts");

        resolved.IsProtectedSystemFile.ShouldBeTrue();
    }

    [Fact]
    public void Matches_the_hosts_file_option_name_case_insensitively()
    {
        var resolved = Resolve(NoEnvironment, "--Hosts-File", @"D:\samples\hosts");

        resolved.Path.ShouldBe(@"D:\samples\hosts");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_argument_path()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", @"  D:\samples\hosts  ");

        resolved.Path.ShouldBe(@"D:\samples\hosts");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_environment_path()
    {
        var resolved = Resolve(Environment("SIMPLEHOST_HOSTS_FILE", @"  D:\env\hosts  "));

        resolved.Path.ShouldBe(@"D:\env\hosts");
    }

    [Fact]
    public void Treats_a_blank_argument_value_as_absent_and_falls_through_to_the_environment()
    {
        var resolved = Resolve(
            Environment("SIMPLEHOST_HOSTS_FILE", @"D:\env\hosts"),
            "--hosts-file", "   ");

        resolved.Path.ShouldBe(@"D:\env\hosts");
    }

    [Fact]
    public void Treats_a_blank_environment_value_as_absent_and_falls_through_to_the_default()
    {
        var resolved = Resolve(Environment("SIMPLEHOST_HOSTS_FILE", "   "));

        resolved.Path.ShouldBe(SystemPath);
    }

    [Fact]
    public void Treats_a_trailing_hosts_file_option_with_no_value_as_absent()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file");

        resolved.Path.ShouldBe(SystemPath);
    }

    [Fact]
    public void Treats_a_hosts_file_option_immediately_followed_by_another_option_as_absent()
    {
        var resolved = Resolve(NoEnvironment, "--hosts-file", "--verbose");

        resolved.Path.ShouldBe(SystemPath);
    }

    [Fact]
    public void Ignores_tokens_that_are_not_the_hosts_file_option()
    {
        var resolved = Resolve(NoEnvironment, "--verbose", "x", "--hosts-file", @"D:\samples\hosts");

        resolved.Path.ShouldBe(@"D:\samples\hosts");
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(
            () => new HostsFilePathResolver().Resolve(null!, NoEnvironment));
    }

    [Fact]
    public void Rejects_a_null_environment_lookup()
    {
        Should.Throw<ArgumentNullException>(
            () => new HostsFilePathResolver().Resolve(Array.Empty<string>(), null!));
    }
}
