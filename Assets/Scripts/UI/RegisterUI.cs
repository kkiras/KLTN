using TMPro;
using UnityEngine;

public class RegisterUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;

    [SerializeField] private TMP_InputField passwordInput;

    [SerializeField] private TMP_InputField confirmPasswordInput;


    public async void OnRegisterButtonClicked()
    {
        string email = emailInput.text.Trim();

        string password = passwordInput.text;

        string confirmPassword = confirmPasswordInput.text;


        if (string.IsNullOrEmpty(email))
        {
            Debug.Log("Email không được để trống.");
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            Debug.Log("Password không được để trống.");
            return;
        }

        if (password != confirmPassword)
        {
            Debug.Log("Password nhập lại không khớp.");
            return;
        }


        AuthResultData result = await AuthManager.Instance.Auth.RegisterAsync(
                                            email,
                                            password
                                        );


        if (result.Success)
        {
            Debug.Log( $"Register success: {result.Email}");

            Debug.Log($"User ID: {result.UserId}");

            // AuthUIManager SetActive(LoginEmail)
        }
        else
        {
            Debug.LogError($"Register failed: {result.ErrorMessage}");
        }
    }
}