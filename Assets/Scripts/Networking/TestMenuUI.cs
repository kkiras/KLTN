using TMPro;
using UnityEngine;

public class TestMenuUI : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private TMP_InputField joinCodeField;

    #endregion

    #region UI Events

    public async void StartHost()
    {
        await HostSingleton.Instance.GameManager.StartHostAsync();
    }

    public async void StartClient()
    {
        await ClientSingleton.Instance.GameManager.StartClientAsync(joinCodeField.text);
    }

    #endregion
}
