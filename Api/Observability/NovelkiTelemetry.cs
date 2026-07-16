namespace Api.Observability;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class NovelkiTelemetry
{
    public const string ActivitySourceName = "NovelkiBackend.Api";
    public const string MeterName = "NovelkiBackend.Api";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> BooksCreated = Meter.CreateCounter<long>("novelki.book.created");
    public static readonly Counter<long> BooksUpdated = Meter.CreateCounter<long>("novelki.book.updated");

    public static readonly Counter<long> BookProgressUpdated =
        Meter.CreateCounter<long>("novelki.book.progress_updated");

    public static readonly Counter<long> BookSearchRequests = Meter.CreateCounter<long>("novelki.search.requests");

    public static readonly Counter<long> AdminDictionaryCreated =
        Meter.CreateCounter<long>("novelki.admin.dictionary.created");
}
