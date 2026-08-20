using UnityEngine;

public class AuthUIManager : MonoBehaviour
{
    [SerializeField] private GameObject loginMethod;
    [SerializeField] private GameObject loginForm;
    [SerializeField] private GameObject registerForm;

    public void ShowLoginMethods()
    {
        loginMethod.SetActive(true);
        loginForm.SetActive(false);
        registerForm.SetActive(false);
    }

    public void ShowLogin()
    {
        loginMethod.SetActive(false);
        loginForm.SetActive(true);
        registerForm.SetActive(false);
    }

    public void ShowRegister()
    {
        loginMethod.SetActive(false);
        loginForm.SetActive(false);
        registerForm.SetActive(true);
    }
}
