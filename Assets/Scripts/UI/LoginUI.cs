using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using Unity.Services.Authentication;
using UnityEngine;

public class LoginUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;

    [SerializeField] private TMP_InputField passwordInput;


    public async void OnLoginButtonClicked()
    {
        string email = emailInput.text.Trim();

        string password = passwordInput.text;


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

        await AuthManager.Instance.ProcessLogin(email, password);

    }

}