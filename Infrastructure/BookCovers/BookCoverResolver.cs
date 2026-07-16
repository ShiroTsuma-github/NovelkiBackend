namespace Infrastructure.BookCovers;

using System.Text.Json;

public sealed class BookCoverResolver
{
    private readonly IEnumerable<IBookCoverProvider> _providers;

    public BookCoverResolver(IEnumerable<IBookCoverProvider> providers)
    {
        _providers = providers;
    }

    public async Task<BookCoverCandidate?> FindAsync(Book book, CancellationToken cancellationToken)
    {
        foreach (var provider in _providers)
        {
            try
            {
                var candidate = await provider.FindAsync(book, cancellationToken);
                if (candidate != null)
                {
                    return candidate;
                }
            }
            catch (HttpRequestException)
            {
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                continue;
            }
            catch (JsonException)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        return null;
    }
}
