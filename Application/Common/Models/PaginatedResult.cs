namespace Application.Common.Models;

public class PaginatedResult<T>
{
    public int Take { get; set; }
    public int Skip { get; set; }
    public int Total { get; set; }
    public List<T> Data { get; set; } = new();

    public static PaginatedResult<T> Create(int skip, int take, int total, IEnumerable<T> data)
    {
        return new PaginatedResult<T> { Skip = skip, Take = take, Total = total, Data = data.ToList() };
    }
}
