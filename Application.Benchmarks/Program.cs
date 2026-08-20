using Application.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<BookSearchQueryParserBenchmarks>();

[MemoryDiagnoser]
[ShortRunJob]
public class BookSearchQueryParserBenchmarks
{
    [ParamsSource(nameof(Queries))]
    public string Query { get; set; } = string.Empty;

    public IEnumerable<string> Queries =>
    [
        "devil sword king",
        "devi*king type:Manhwa status:\"Plan To Read\"",
        "genre:Fantasy,Action tag:\"must read\" rating:>=4 -tag:backlog",
        "\"The Beginning After The End\" author:\"TurtleMe\" updated:>=2026-01-01"
    ];

    [Benchmark]
    public object Parse() => BookSearchQueryParser.Parse(Query);
}
