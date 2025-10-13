namespace Application.Common.DTOs.User;

public sealed record LoginDto(string? username, string? email, string password);
