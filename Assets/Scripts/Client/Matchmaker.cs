using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class Matchmaker : MonoBehaviour
{
    #region Configuration

    // private string POOL_NAME = "abc";
    private string QUEUE_NAME = "boardgame-queue";

    #endregion

    #region UI References

    [Header("UI References")]
    public TMP_Text textButtonMatchmaking;
    public TMP_Text textTimer;
    public TMP_Text textStatus;
    public TMP_Text textStatusFinding;

    #endregion

    #region Runtime State

    private bool isMatchmaking;
    private float matchmakeTimer;
    private CancellationTokenSource cts;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Matchmaking UI is client-only and never runs on a dedicated server.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { return; }

        Application.runInBackground = true;
        textTimer.gameObject.SetActive(false);
        textStatus.gameObject.SetActive(false);
        textStatusFinding.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isMatchmaking)
        {
            ShowTimerText();
            matchmakeTimer += Time.deltaTime;

            // Format elapsed matchmaking time.
            int minutes = Mathf.FloorToInt(matchmakeTimer / 60F);
            int seconds = Mathf.FloorToInt(matchmakeTimer - minutes * 60);
            textTimer.text = $"{minutes:00}:{seconds:00}";
        }
    }

    #endregion

    #region UI Events

    public async void OnFindMatchButtonClicked()
    {
        if (isMatchmaking) { CancelMatchMaking(); }
        else
        {

            await FindMatch();
        }
    }

    #endregion

    #region Matchmaking Lifecycle

    private void CancelMatchMaking()
    {
        if (cts != null)
        {
            ShowStatusText();
            textStatus.text = "Hủy tìm trận...";
            cts.Cancel();
            ResetUI();
        }
    }

    private void ResetUI()
    {
        isMatchmaking = false;
        textButtonMatchmaking.text = "Tìm trận";
    }

    public async Task FindMatch()
    {
        Debug.Log("Đang tìm trận..");
        isMatchmaking = true;
        matchmakeTimer = 0f;
        textButtonMatchmaking.text = "Hủy bỏ";
        cts = new CancellationTokenSource();

        try
        {
            var players = new List<Player>
            {
                new Player(AuthenticationService.Instance.PlayerId)
            };
            var ticket = await MatchmakerService.Instance.CreateTicketAsync(
                players,
                new CreateTicketOptions(QUEUE_NAME));
            Debug.Log($"Đã tạo Ticket thành công: {ticket.Id}");

            while (!cts.Token.IsCancellationRequested)
            {
                var ticketStatus = await MatchmakerService.Instance.GetTicketAsync(ticket.Id);
                Debug.Log($"Ticket Type: {ticketStatus.Type}");
                if (ticketStatus.Type == typeof(IpPortAssignment))
                {
                    var assignment = (IpPortAssignment)ticketStatus.Value;
                    isMatchmaking = false;
                    ShowStatusText();
                    textStatus.text = "Đang kết nối...";
                    Debug.Log($"TÌM THẤY SERVER EDGEGAP! IP: {assignment.Ip}, Port: {assignment.Port}");

                    if (NetworkManager.Singleton == null)
                    {
                        Debug.LogError(
                            "KHÔNG TÌM THẤY NetworkManager! Bạn cần tạo GameObject NetworkManager " +
                            "trong Scene FindMatch (nhớ tick DontDestroyOnLoad) kèm UnityTransport.");
                        textStatus.text = "Error: No NetManager";
                        ResetUI();
                        break;
                    }

                    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                    if (transport == null)
                    {
                        Debug.LogError("NetworkManager không có UnityTransport component!");
                        textStatus.text = "Error: No Transport";
                        ResetUI();
                        break;
                    }

                    transport.SetConnectionData(assignment.Ip, (ushort)assignment.Port);
                    NetworkManager.Singleton.StartClient();
                    textButtonMatchmaking.text = "Đã kết nối";
                    textStatus.text = "Chờ đối thủ...";
                    break;
                }
                else if (ticketStatus.Type == typeof(MultiplayAssignment))
                {
                    var assignment = (MultiplayAssignment)ticketStatus.Value;

                    if (assignment.Status == MultiplayAssignment.StatusOptions.Timeout ||
                        assignment.Status == MultiplayAssignment.StatusOptions.Failed)
                    {
                        Debug.LogError($"Lỗi Matchmaker hoặc hết thời gian! Status: {assignment.Status}");
                        textStatus.text = "Không tìm thấy đối thủ.";
                        ResetUI();
                        break;
                    }
                    // Continue polling while the assignment remains in progress.
                }

                await Task.Delay(1500, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("Đã huỷ tìm trận.");
            textStatus.text = "Hủy tìm trận...";
            await Task.Delay(1000);
            textStatus.text = "";
            ResetUI();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Lỗi trong quá trình tìm trận: {ex.Message}");
            textStatus.text = "No match found";
            ResetUI();
        }
        finally
        {
            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }
        }
    }

    #endregion

    #region UI Rendering

    private void ShowTimerText()
    {
        textTimer.gameObject.SetActive(true);
        textStatus.gameObject.SetActive(false);
        textStatusFinding.gameObject.SetActive(true);
    }

    private void ShowStatusText()
    {
        textTimer.gameObject.SetActive(false);
        textStatus.gameObject.SetActive(true);
        textStatusFinding.gameObject.SetActive(false);
    }

    #endregion
}
