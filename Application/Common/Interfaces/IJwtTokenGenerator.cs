namespace Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    public TokenResponse? GenerateToken(IUser user);
}
