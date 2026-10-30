using System.Net;
using System.Net.Sockets;

namespace UrlShortener.Domain;

public static class UrlValidator
{
    public static bool TryValidate(string? value, out Uri? destination, out string? error)
    {
        destination = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            error = "A valid absolute URL is required.";
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            error = "Only HTTP and HTTPS URLs are allowed.";
            return false;
        }

        if (parsed.IsLoopback || IsPrivateHost(parsed.Host))
        {
            error = "Local and private network destinations are not allowed.";
            return false;
        }

        destination = parsed;
        return true;
    }

    public static bool IsValidAlias(string? alias) =>
        !string.IsNullOrWhiteSpace(alias) && alias.Length <= 64 && alias.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool IsPrivateHost(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 169 && bytes[1] == 254;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
    }
}