using System;
using System.Threading.Tasks;
using Firebase.Auth;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    private IAuthService authService;
    public IAuthService Auth => authService;

    private const string PREF_LOGIN_METHOD = "login_method";
    private const string PREF_FIREBASE_REFRESH_TOKEN = "firebase_refresh_token";

    public static event Action OnLoginSuccess;
    public static event Action OnLogoutSuccess;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeAuth();
    }

	private void InitializeAuth()
    {
#if UNITY_STANDALONE
        authService = new FirebaseRestAuthService();
        Debug.Log("Initialized Firebase REST Auth for Standalone platform.");

#elif UNITY_ANDROID || UNITY_IOS
        authService = new FirebaseSDKAuthService();
        Debug.Log("Initialized Firebase SDK Auth for Mobile platform.");

#else
        Debug.LogWarning("Current platform is not configured for Firebase REST Auth.");
        authService = new FirebaseRestAuthService();

#endif
    }
	public async Task ProcessLogin(string email, string password)
    {
        // AuthUIManager.Instance.ShowLoading(true); 

		try
        {
            // Đăng nhập Firebase
            AuthResultData result = await authService.LoginAsync(email, password);

            if (!result.Success || string.IsNullOrEmpty(result.IdToken))
            {
                Debug.LogError($"Firebase Login failed: {result.ErrorMessage}");
                return;
            }

            Debug.Log($"Firebase Login success. User ID: {result.UserId}");

            // Link sang UGS bằng Firebase Token
            bool ugsSuccess = await LoginFirebaseToUGS(result.IdToken);
            if (!ugsSuccess) return;

            // 3. Mọi thứ hoàn tất -> Kích hoạt Event chuyển Scene
            Debug.Log("Xác thực toàn bộ thành công! Kích hoạt event chuyển scene.");

            PlayerPrefs.SetString(PREF_LOGIN_METHOD, "email");
            PlayerPrefs.SetString(PREF_FIREBASE_REFRESH_TOKEN, result.RefreshToken);
            PlayerPrefs.Save();

            OnLoginSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi hệ thống khi đăng nhập: {ex.Message}");
        }
        finally
        {
            // AuthUIManager.Instance.ShowLoading(false);
        }
    }

    public async Task<bool> ProcessGoogleLogin(string googleIdToken)
    {
        try
        {
            Debug.Log("Bắt đầu đăng nhập Google qua Firebase...");
            
            // Đăng nhập Firebase bằng Google Token
            AuthResultData result = await authService.LoginWithGoogleAsync(googleIdToken);

            if (!result.Success || string.IsNullOrEmpty(result.IdToken))
            {
                Debug.LogError($"Firebase Google Login failed: {result.ErrorMessage}");
                return false;
            }

            Debug.Log($"Firebase Google Login success. User ID: {result.UserId}");

            // Link sang UGS bằng Firebase Token
            bool ugsSuccess = await LoginFirebaseToUGS(result.IdToken);
            if (!ugsSuccess) return false;

            Debug.Log("Xác thực Google -> Firebase -> UGS thành công!");

            PlayerPrefs.SetString(PREF_LOGIN_METHOD, "google");
            PlayerPrefs.Save();

            OnLoginSuccess?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi hệ thống khi đăng nhập Google: {ex.Message}");
            return false;
        }
    }

    public async void TryAutoLogin()
    {
        string loginMethod = PlayerPrefs.GetString(PREF_LOGIN_METHOD, "");

        if (loginMethod == "google" && GoogleAuthService.Instance != null)
        {
            Debug.Log("Bạn đang được đăng nhập lại với tài khoản Google...");
            await GoogleAuthService.Instance.TryAutoLogin(); 
        }
        else if (loginMethod == "email")
        {
            string savedToken = PlayerPrefs.GetString(PREF_FIREBASE_REFRESH_TOKEN, "");
            if (!string.IsNullOrEmpty(savedToken))
            {
                Debug.Log("Phát hiện phiên Email, tiến hành Auto-Login...");
                await ProcessAutoLoginEmail(savedToken);
            }
        }
        else
        {
            Debug.Log("Không có phiên đăng nhập cũ, yêu cầu đăng nhập thủ công.");
        }
    }

    private async Task ProcessAutoLoginEmail(string refreshToken)
    {
        try
        {
            AuthResultData result = await authService.LoginWithRefreshTokenAsync(refreshToken);

            if (!result.Success || string.IsNullOrEmpty(result.IdToken))
            {
                Debug.LogWarning("Auto-Login Email thất bại (Token hết hạn), vui lòng đăng nhập lại.");
                SignOut();
                return;
            }

            bool ugsSuccess = await LoginFirebaseToUGS(result.IdToken);
            if (!ugsSuccess) return;

            Debug.Log("Auto-Login Email & UGS thành công!");
            OnLoginSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi hệ thống khi Auto Login Email: {ex.Message}");
        }
    }

	private async Task<bool> LoginFirebaseToUGS(string firebaseIdToken)
    {
		if (string.IsNullOrEmpty(firebaseIdToken))
        {
            Debug.LogError("Firebase ID Token is null or empty.");
            return false;
        }

        try
        {
            await UGSInitializer.Initialize();

			if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.LogError("UGS initialization failed.");
                return false;
            }

            await AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-firebase", firebaseIdToken);
            Debug.Log($"UGS login successful. Player ID: {AuthenticationService.Instance.PlayerId}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("UGS OIDC login failed: " + e.Message);
            return false;
        }
    }
    public void SignOut()
    {
        PlayerPrefs.DeleteKey(PREF_LOGIN_METHOD);
        PlayerPrefs.DeleteKey(PREF_FIREBASE_REFRESH_TOKEN);
        PlayerPrefs.Save();

        //Đăng xuất Firebase (Email/Pass và Google)
        authService?.Logout();

        //Đăng xuất Unity Gaming Services
        if (AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
        }

        if (GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.ClearSession();
        }

        Debug.Log("Đã đăng xuất toàn bộ hệ thống thành công.");
        OnLogoutSuccess?.Invoke(); 
    }
}
