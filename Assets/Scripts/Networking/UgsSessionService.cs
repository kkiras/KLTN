using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;

public sealed class UgsSessionService
{
    #region Constants

    private const string SessionType = "kltn-game";

    #endregion

    #region Fields and Singleton

    private ISession session;
    public static UgsSessionService Instance { get; } = new UgsSessionService();

    #endregion

    #region Events

    public event Action<int> PlayerCountChanged;
    public event Action HostSessionEnded;

    #endregion

    #region Properties

    public bool HasSession => session != null;
    public bool IsHost => session != null && session.IsHost;
    public int PlayerCount => session?.PlayerCount ?? 0;
    public string SessionId => session?.Id ?? string.Empty;
    public string JoinCode => session?.Code ?? string.Empty;

    #endregion

    #region Construction

    private UgsSessionService()
    {
    }

    #endregion

    #region Session Lifecycle

    public async Task<string> HostAsync()
    {
        await LeaveAsync();

        var options = new SessionOptions
        {
            Type = SessionType,
            Name = "KLTN Game",
            MaxPlayers = 2,
            IsPrivate = true,
            IsLocked = false,
        }.WithRelayNetwork();
        session = await MultiplayerService.Instance.CreateSessionAsync(options);
        Attach(session);
        PlayerCountChanged?.Invoke(session.PlayerCount);
        return session.Code;
    }

    public async Task JoinByCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode)) { throw new ArgumentException("Session join code is required.", nameof(joinCode)); }

        await LeaveAsync();

        var options = new JoinSessionOptions
        {
            Type = SessionType,
        };
        session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim(), options);
        Attach(session);
        PlayerCountChanged?.Invoke(session.PlayerCount);
    }

    public async Task LeaveAsync()
    {
        var leavingSession = session;
        if (leavingSession == null) { return; }

        Detach(leavingSession);
        session = null;

        if (leavingSession.IsHost) { await leavingSession.AsHost().DeleteAsync(); }
        else
        {
            await leavingSession.LeaveAsync();
        }

        PlayerCountChanged?.Invoke(0);
    }

    #endregion

    #region Session Event Wiring

    private void Attach(ISession activeSession)
    {
        activeSession.PlayerJoined += OnPlayerChanged;
        activeSession.PlayerHasLeft += OnPlayerChanged;
        activeSession.Deleted += OnSessionEnded;
        activeSession.RemovedFromSession += OnSessionEnded;
    }

    private void Detach(ISession activeSession)
    {
        activeSession.PlayerJoined -= OnPlayerChanged;
        activeSession.PlayerHasLeft -= OnPlayerChanged;
        activeSession.Deleted -= OnSessionEnded;
        activeSession.RemovedFromSession -= OnSessionEnded;
    }

    #endregion

    #region Session Events

    private void OnPlayerChanged(string _)
    {
        PlayerCountChanged?.Invoke(PlayerCount);
    }

    private void OnSessionEnded()
    {
        if (session != null) { Detach(session); }

        session = null;
        PlayerCountChanged?.Invoke(0);
        HostSessionEnded?.Invoke();
    }

    #endregion
}
