namespace Application.Common.Interfaces;

public interface IUser
{
    public Guid? Id { get; }
    public Guid RequiredId { get; }
    public string? Email { get; }
    public string? Username { get; }
    public IEnumerable<string> Roles { get; }
    public bool IsAuthenticated { get; }
    public bool Valid { get; }
}
