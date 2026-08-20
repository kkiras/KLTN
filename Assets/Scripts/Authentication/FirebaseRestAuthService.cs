using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class FirebaseRestAuthService : IAuthService
{
    private const string API_KEY = "AIzaSyBhSMPFFlAza7Sb-5mK7UZR8r4YQJ3bXuc";
    private const string SIGN_UP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string SIGN_IN_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=";
    private const string SIGN_IN_IDP_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=";
    private const string SECURE_TOKEN_URL = "https://securetoken.googleapis.com/v1/token?key=";

    private string userId;
    private string email;
    private string idToken;
    private string refreshToken;

    public bool IsLoggedIn => !string.IsNullOrEmpty(idToken);
    public string UserId => userId;
    public string Email => email;
    public string IdToken => idToken;
    public string RefreshToken => refreshToken;

    public async Task<AuthResultData> RegisterAsync(string email, string password)
    {
        FirebaseAuthRequest request = new FirebaseAuthRequest
        {
            email = email,
            password = password,
            returnSecureToken = true
        };

        string json = JsonUtility.ToJson(request);

        UnityWebRequest webRequest = CreatePostRequest(
                                        SIGN_UP_URL + API_KEY,
                                        json
                                    );

        try
        {
            await SendRequest(webRequest);

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                FirebaseAuthResponse response = 
                    JsonUtility.FromJson<FirebaseAuthResponse>(webRequest.downloadHandler.text);

                SaveUserData(response);

                return new AuthResultData{
                    Success = true,
                    UserId = userId,
                    Email = this.email,
                    IdToken = idToken,
                    RefreshToken = refreshToken
                };
            }

            return CreateErrorResult(webRequest);
        }
        catch (Exception e)
        {
            return new AuthResultData{
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = e.Message
            };
        }
        finally
        {
            webRequest.Dispose();
        }
    }


    public async Task<AuthResultData> LoginAsync(string email, string password)
    {
        FirebaseAuthRequest request = new FirebaseAuthRequest
        {
            email = email,
            password = password,
            returnSecureToken = true
        };

        string json = JsonUtility.ToJson(request);

        UnityWebRequest webRequest =
            CreatePostRequest(
                SIGN_IN_URL + API_KEY,
                json
            );

        try
        {
            await SendRequest(webRequest);

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                FirebaseAuthResponse response =
                    JsonUtility.FromJson<FirebaseAuthResponse>(webRequest.downloadHandler.text);

                SaveUserData(response);

                return new AuthResultData{
                    Success = true,
                    UserId = userId,
                    Email = this.email,
                    IdToken = idToken,
                    RefreshToken = refreshToken
                };
            }

            return CreateErrorResult(webRequest);
        }
        catch (Exception e)
        {
            return new AuthResultData{
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = e.Message
            };
        }
        finally
        {
            webRequest.Dispose();
        }
    }

    public async Task<AuthResultData> LoginWithGoogleAsync(string googleIdToken)
    {
        // Payload theo chuẩn REST API của Firebase cho Identity Providers
        FirebaseIdpRequest request = new FirebaseIdpRequest
        {
            postBody = $"id_token={googleIdToken}&providerId=google.com",
            requestUri = "http://localhost",
            returnSecureToken = true
        };

        string json = JsonUtility.ToJson(request);
        UnityWebRequest webRequest = CreatePostRequest(SIGN_IN_IDP_URL + API_KEY, json);

        try
        {
            await SendRequest(webRequest);

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // Response của signInWithIdp tương tự signInWithPassword nên dùng chung struct được
                FirebaseAuthResponse response = JsonUtility.FromJson<FirebaseAuthResponse>(webRequest.downloadHandler.text);
                SaveUserData(response);

                return new AuthResultData {
                    Success = true,
                    UserId = userId,
                    Email = this.email,
                    IdToken = idToken,
                    RefreshToken = refreshToken
                };
            }

            return CreateErrorResult(webRequest);
        }
        catch (Exception e)
        {
            return new AuthResultData {
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = e.Message
            };
        }
        finally
        {
            webRequest.Dispose();
        }
    }

    public async Task<AuthResultData> LoginWithRefreshTokenAsync(string savedRefreshToken)
    {
        WWWForm form = new WWWForm();
        form.AddField("grant_type", "refresh_token");
        form.AddField("refresh_token", savedRefreshToken);

        UnityWebRequest request = UnityWebRequest.Post(SECURE_TOKEN_URL + API_KEY, form);

        try
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Parse kết quả trả về
                FirebaseRefreshTokenResponse response = JsonUtility.FromJson<FirebaseRefreshTokenResponse>(request.downloadHandler.text);
                
                // Cập nhật biến bộ nhớ
                this.idToken = response.id_token;
                this.refreshToken = response.refresh_token;
                this.userId = response.user_id;

                return new AuthResultData {
                    Success = true,
                    UserId = this.userId,
                    IdToken = this.idToken,
                    RefreshToken = this.refreshToken
                };
            }

            return new AuthResultData { Success = false, ErrorMessage = request.error };
        }
        catch (Exception e)
        {
            return new AuthResultData { Success = false, ErrorMessage = e.Message };
        }
        finally
        {
            request.Dispose();
        }
    }

    public void Logout()
    {
        userId = null;
        email = null;
        idToken = null;
        refreshToken = null;

        Debug.Log("Firebase logout successful.");
    }


    private void SaveUserData(FirebaseAuthResponse response)
    {
        userId = response.localId;
        email = response.email;
        idToken = response.idToken;
        refreshToken = response.refreshToken;
    }


    private UnityWebRequest CreatePostRequest(string url, string json)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);

        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type", 
            "application/json"
        );

        return request;
    }


    private async Task SendRequest(UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }
    }


    private AuthResultData CreateErrorResult(UnityWebRequest request)
    {
        string errorCode = "UNKNOWN_ERROR";
        string errorMessage = "Unknown error.";

        if (!string.IsNullOrEmpty(
            request.downloadHandler.text))
        {
            try
            {
                FirebaseErrorResponse errorResponse =
                    JsonUtility.FromJson<FirebaseErrorResponse>(request.downloadHandler.text);

                if (errorResponse != null &&
                    errorResponse.error != null)
                {
                    errorCode = errorResponse.error.message;

                    errorMessage = ConvertFirebaseErrorToMessage(errorCode);
                }
            }
            catch
            {
                errorMessage = request.error;
            }
        }

        return new AuthResultData
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }


    private string ConvertFirebaseErrorToMessage(
        string errorCode)
    {
        switch (errorCode)
        {
            case "EMAIL_EXISTS":
                return "Email này đã được đăng ký.";

            case "EMAIL_NOT_FOUND":
                return "Email không tồn tại.";

            case "INVALID_PASSWORD":
                return "Mật khẩu không chính xác.";

            case "INVALID_LOGIN_CREDENTIALS":
                return "Email hoặc mật khẩu không chính xác.";

            case "INVALID_EMAIL":
                return "Email không hợp lệ.";

            case "WEAK_PASSWORD":
                return "Mật khẩu quá yếu.";

            case "USER_DISABLED":
                return "Tài khoản đã bị vô hiệu hóa.";

            case "OPERATION_NOT_ALLOWED":
                return "Phương thức đăng nhập này chưa được bật.";

            case "TOO_MANY_ATTEMPTS_TRY_LATER":
                return "Có quá nhiều lần thử. Vui lòng thử lại sau.";

            case "NETWORK_REQUEST_FAILED":
                return "Không thể kết nối đến máy chủ.";

            default:
                return $"Đăng nhập thất bại: {errorCode}";
        }
    }

    [Serializable] private class FirebaseAuthRequest
    {
        public string email;
        public string password;
        public bool returnSecureToken;
    }

    [Serializable] private class FirebaseIdpRequest
    {
        public string postBody;
        public string requestUri;
        public bool returnSecureToken;
    }

    [Serializable] 
    private class FirebaseRefreshTokenResponse
    {
        public string access_token;
        public string expires_in;
        public string token_type;
        public string refresh_token;
        public string id_token;
        public string user_id;
        public string project_id;
    }

    [Serializable] private class FirebaseAuthResponse
    {
        public string idToken;
        public string email;
        public string refreshToken;
        public string expiresIn;
        public string localId;
    }


    [Serializable] private class FirebaseErrorResponse
    {
        public FirebaseError error;
    }


    [Serializable] private class FirebaseError
    {
        public int code;
        public string message;
    }
}