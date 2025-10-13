namespace Application.Features.BookFeatures.Mappers;

using Application.Common.DTOs.Book;

public class BookToDto : IMapper<Book, BookDto>
{
    public BookDto Map(Book source)
    {
        return new BookDto
        {
            Id = source.Id,
            Title = source.Title,
            Author = source.Author.Name,
        };
    }

    public IEnumerable<BookDto> Map(IEnumerable<Book> source)
    {
        return source.Select(Map);
    }

    public void Map(Book source, BookDto destination)
    {
        throw new NotImplementedException();
    }
}
