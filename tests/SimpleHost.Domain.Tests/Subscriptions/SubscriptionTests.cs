using Shouldly;
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Domain.Tests.Subscriptions;

public class SubscriptionTests
{
    [Fact]
    public void Keeps_the_url_and_category_it_was_created_with()
    {
        var subscription = Subscription.Of("https://lists.example.com/adblock.txt", "Ad block");

        subscription.Url.ShouldBe("https://lists.example.com/adblock.txt");
        subscription.Category.ShouldBe("Ad block");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_url()
    {
        var subscription = Subscription.Of("  https://lists.example.com/adblock.txt  ", "Ad block");

        subscription.Url.ShouldBe("https://lists.example.com/adblock.txt");
    }

    [Fact]
    public void Trims_surrounding_whitespace_from_the_category()
    {
        var subscription = Subscription.Of("https://lists.example.com/adblock.txt", "  Ad block  ");

        subscription.Category.ShouldBe("Ad block");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_url_that_is_empty_or_whitespace(string url)
        => Should.Throw<ArgumentException>(() => Subscription.Of(url, "Ad block"));

    [Fact]
    public void Rejects_a_null_url()
        => Should.Throw<ArgumentException>(() => Subscription.Of(null!, "Ad block"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_category_that_is_empty_or_whitespace(string category)
        => Should.Throw<ArgumentException>(
            () => Subscription.Of("https://lists.example.com/adblock.txt", category));

    [Fact]
    public void Rejects_a_null_category()
        => Should.Throw<ArgumentException>(
            () => Subscription.Of("https://lists.example.com/adblock.txt", null!));

    [Fact]
    public void Two_subscriptions_with_the_same_url_and_category_are_equal()
    {
        var one = Subscription.Of("https://lists.example.com/adblock.txt", "Ad block");
        var other = Subscription.Of("https://lists.example.com/adblock.txt", "Ad block");

        one.ShouldBe(other);
    }
}
