using System.Threading.Tasks;

public interface IAuthService
{
    #region Commands

    Task<AuthResultData> RegisterAsync(string email, string password);
    Task<AuthResultData> LoginAsync(string email, string password);
    Task<AuthResultData> LoginWithGoogleAsync(string googleIdToken);
    Task<AuthResultData> LoginWithRefreshTokenAsync(string token);
    void Logout();

    #endregion

    #region State

    bool IsLoggedIn { get; }
    string UserId { get; }
    string Email { get; }
    string IdToken { get; }
    string RefreshToken { get; }

    #endregion
}
