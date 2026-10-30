using UrlShortener.Domain;

namespace UrlShortener.UnitTests;

public sealed class UrlValidatorTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://192.168.1.10/private")]
    [InlineData("file:///etc/passwd")]
    public void RejectsUnsafeDestinations(string value)
    {
        Assert.False(UrlValidator.TryValidate(value, out _, out _));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.org/path?q=1")]
    public void AcceptsHttpDestinations(string value)
    {
        Assert.True(UrlValidator.TryValidate(value, out var destination, out _));
        Assert.NotNull(destination);
    }

    [Fact]
    public void RejectsUnsafeAliases()
    {
        Assert.False(UrlValidator.IsValidAlias("admin/redirect"));
        Assert.False(UrlValidator.IsValidAlias("аdmin"));
        Assert.True(UrlValidator.IsValidAlias("docs_v1"));
    }
}