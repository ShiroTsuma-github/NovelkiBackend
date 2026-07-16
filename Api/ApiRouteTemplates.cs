namespace Api;

internal static class ApiRouteTemplates
{
    public const string Id = "{id:guid}";
    public const string IdDetails = Id + "/details";
    public const string ByName = "by-name/{name}";
    public const string ByNameDetails = ByName + "/details";
    public const string BookCover = Id + "/cover";
    public const string ImportSession = "import/sessions/{sessionId:guid}";
    public const string ImportSessionRow = ImportSession + "/rows/{rowId:guid}";
    public const string AdminBookById = "books/{id:guid}";
}
