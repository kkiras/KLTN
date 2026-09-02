public sealed class AuthResultData
{
    #region Result State

    public bool Success;
    public string UserId;
    public string Email;
    public string IdToken;
    public string RefreshToken;
    public string ErrorCode;
    public string ErrorMessage;

    #endregion

    #region Factories

    public static AuthResultData Failure(string errorCode, string errorMessage)
    {
        return new AuthResultData
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }

    #endregion
}
