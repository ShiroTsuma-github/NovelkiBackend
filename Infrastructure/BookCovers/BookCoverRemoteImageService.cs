namespace Infrastructure.BookCovers;

using System.Net;

public sealed class BookCoverRemoteImageService : IBookCoverRemoteImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBookCoverStorage _storage;

    public BookCoverRemoteImageService(IHttpClientFactory httpClientFactory, IBookCoverStorage storage)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
    }

    public async Task<BookCoverStoredFiles> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
            imageUri.Scheme is not ("http" or "https"))
        {
            throw new FluentValidation.ValidationException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        await EnsureRemoteHostAllowedAsync(imageUri, cancellationToken);

        using var client = _httpClientFactory.CreateClient("BookCoverImages");
        using var response =
            await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new FluentValidation.ValidationException($"Image URL returned HTTP {(int)response.StatusCode}.");
        }

        await using var imageStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await _storage.SaveAsync(
            ownerId,
            bookId,
            imageStream,
            Path.GetFileName(imageUri.LocalPath),
            response.Content.Headers.ContentType?.MediaType,
            cancellationToken);
    }

    private static async Task EnsureRemoteHostAllowedAsync(Uri imageUri, CancellationToken cancellationToken)
    {
        if (imageUri.IsLoopback)
        {
            throw new FluentValidation.ValidationException("Image URL host is not allowed.");
        }

        var addresses = IPAddress.TryParse(imageUri.Host, out var literalAddress)
            ? [literalAddress]
            : await Dns.GetHostAddressesAsync(imageUri.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
        {
            throw new FluentValidation.ValidationException("Image URL host is not allowed.");
        }
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.Broadcast) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.IPv6None) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast)
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xfe) == 0xfc;
        }

        return true;
    }
}
