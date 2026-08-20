using System.Threading.Tasks;
using UnityEngine;

public interface IAuthService
{
    Task<AuthResultData> RegisterAsync(string email, string password);
    Task<AuthResultData> LoginAsync(string email, string password);
    Task<AuthResultData> LoginWithGoogleAsync(string googleIdToken);
    Task<AuthResultData> LoginWithRefreshTokenAsync(string token);

    void Logout();

    bool IsLoggedIn { get; }
    string UserId { get; }
    string Email { get; }
    string IdToken { get; }
    string RefreshToken { get; }
}
