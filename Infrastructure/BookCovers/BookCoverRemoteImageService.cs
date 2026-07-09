namespace Infrastructure.BookCovers;

public sealed class BookCoverRemoteImageService : IBookCoverRemoteImageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBookCoverStorage _storage;

    public BookCoverRemoteImageService(IHttpClientFactory httpClientFactory, IBookCoverStorage storage)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
    }

    public async Task<BookCoverStoredFile> SaveFromUrlAsync(Guid ownerId, Guid bookId, string imageUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
            imageUri.Scheme is not ("http" or "https"))
        {
            throw new FluentValidation.ValidationException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        using var client = _httpClientFactory.CreateClient("BookCoverImages");
        using var response = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
}
