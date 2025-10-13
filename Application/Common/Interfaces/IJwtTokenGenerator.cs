namespace Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    TokenResponse? GenerateToken(IUser user);
}
