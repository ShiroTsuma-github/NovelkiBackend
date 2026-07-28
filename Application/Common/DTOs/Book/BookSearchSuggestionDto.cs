namespace Application.Common.DTOs.Book;

public sealed record BookSearchSuggestionDto(string Value, int Count, bool IsExact);
