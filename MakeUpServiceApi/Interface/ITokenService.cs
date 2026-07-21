namespace MakeUpServiceApi.Interface
{
    public interface ITokenService
    {
        Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(int adminID);
        Task<(string AccessToken, string RefreshToken)> RefreshTokenGenerateAsync(string accessToken, string refreshToken);
        Task<bool> RevokeTokenAsync(string accessToken, string refreshToken);
    }
}
