// Copyright Pumpkin Games Ltd. All Rights Reserved.

//https://github.com/heroiclabs/fishgame-unity/blob/main/FishGame/Assets/Managers/GameManager.cs

using Microsoft.Xna.Framework;
using Nakama;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Engine.IO;
using NakamaFNAPong.Engine.Threading;
using NakamaFNAPong.Gameplay.Players;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NakamaFNAPong.NakamaMultiplayer;

public record SpawnedRemotePlayerEventArgs(
    string SessionId
);

public record ReceivedRemotePaddleStateEventArgs(
    float TotalSeconds,
    Vector2 Position,
    Vector2 Velocity,
    bool MoveUp,
    bool MoveDown,
    string SessionId
);

public record ReceivedRemoteBallStateEventArgs(
    float Direction,
    Vector2 Position
);

public record ReceivedRemoteScoreEventArgs(
    int Player1Score,
    int Player2Score
);

public record RemovedPlayerEventArgs(
    string SessionId
);

/// <summary>
/// Responsible for managing a networked game
/// </summary>
public class NetworkGameManager
{
    public event EventHandler SpawnedLocalPlayer;
    public event EventHandler<SpawnedRemotePlayerEventArgs> SpawnedRemotePlayer;
    public event EventHandler<ReceivedRemotePaddleStateEventArgs> ReceivedRemotePaddleState;
    public event EventHandler<ReceivedRemoteBallStateEventArgs> ReceivedRemoteBallState;
    public event EventHandler<ReceivedRemoteScoreEventArgs> ReceivedRemoteScore;
    public event EventHandler<RemovedPlayerEventArgs> RemovedPlayer;

    //Multiplayer
    readonly NakamaConnection _nakamaConnection;

    IUserPresence _hostPresence;
    IUserPresence _localUserPresence;
    IMatch _currentMatch;

    readonly IDictionary<string, Player> _players;
    Player _localPlayer;

    public bool IsHost => (_hostPresence?.SessionId ?? "host") == (_localUserPresence?.SessionId ?? "user");

    readonly PacketReader _packetReader = new();

    public NetworkGameManager(
        NakamaConnection nakamaConnection)
    {
        _nakamaConnection = nakamaConnection;

        _players = new Dictionary<string, Player>();
    }

    public async Task Connect()
    {
        await _nakamaConnection.Connect();

        // Bounce events to resolve on main thread - makes things simpler while introducing a _bit_ of latency.

        _nakamaConnection.Socket.ReceivedMatchmakerMatched += matched =>
            ConsoleMainThreadDispatcher.Enqueue(() => OnReceivedMatchmakerMatched(matched));

        _nakamaConnection.Socket.ReceivedMatchPresence += matchPresenceEvent =>
            ConsoleMainThreadDispatcher.Enqueue(() => OnReceivedMatchPresence(matchPresenceEvent));

        _nakamaConnection.Socket.ReceivedMatchState += matchState =>
            ConsoleMainThreadDispatcher.Enqueue(() => OnReceivedMatchState(matchState));
    }

    /// <summary>
    /// Called when a MatchmakerMatched event is received from the Nakama server.
    /// </summary>
    /// <param name="matched">The MatchmakerMatched data.</param>
    public async void OnReceivedMatchmakerMatched(IMatchmakerMatched matched)
    {
        Logger.WriteLine($"{nameof(NetworkGameManager)}.{nameof(OnReceivedMatchmakerMatched)}");

        //Set the host - hosts will be responsible for sending non-player data (e.g. like the ball's position)
        _hostPresence = matched.Users.OrderByDescending(x => x.Presence.SessionId).First().Presence;

        // Cache a reference to the local user.
        _localUserPresence = matched.Self.Presence;

        // Join the match.
        var match = await _nakamaConnection.Socket.JoinMatchAsync(matched);

        // Spawn a player instance for each connected user.
        foreach (var user in match.Presences)
            SpawnPlayer(match.Id, user);

        // Cache a reference to the current match.
        _currentMatch = match;
    }

    /// <summary>
    /// Called when a player/s joins or leaves the match.
    /// </summary>
    /// <param name="matchPresenceEvent">The MatchPresenceEvent data.</param>
    public void OnReceivedMatchPresence(IMatchPresenceEvent matchPresenceEvent)
    {
        Logger.WriteLine($"{nameof(NetworkGameManager)}.{nameof(OnReceivedMatchPresence)}");

        //Set a new host if current host leaves
        if (matchPresenceEvent.Leaves.Any(x => x.UserId == _hostPresence.UserId))
            _hostPresence = _currentMatch.Presences.OrderBy(x => x.SessionId).First();

        // For each new user that joins, spawn a player for them.
        foreach (var user in matchPresenceEvent.Joins)
            SpawnPlayer(matchPresenceEvent.MatchId, user);

        // For each player that leaves, despawn their player.
        foreach (var user in matchPresenceEvent.Leaves)
            RemovePlayer(user.SessionId);
    }

    /// <summary>
    /// Called when new match state is received.
    /// </summary>
    /// <param name="matchState">The MatchState data.</param>
    public void OnReceivedMatchState(IMatchState matchState)
    {
        //Logger.WriteLine($"{nameof(NetworkGameManager)}.{nameof(OnReceivedMatchState)}: {matchState.OpCode}");

        if (!_players.TryGetValue(matchState.UserPresence.SessionId, out var player))
            return;

        //a If the incoming data is not related to this remote player, ignore it and return early.
        var networkPlayer = player as NetworkPlayer;
        if (matchState.UserPresence.SessionId != networkPlayer?.NetworkData?.User?.SessionId)
            return;

        // Decide what to do based on the Operation Code of the incoming state data as defined in OpCodes.
        switch (matchState.OpCode)
        {
            case OpCodes.PADDLE_PACKET:
                UpdateRemotePaddleStateFromPacket(matchState.State, networkPlayer);
                break;

            case OpCodes.BALL_PACKET:
                UpdateDirectionAndPositionFromState(matchState.State);
                break;

            case OpCodes.SCORE_EVENT:
                UpdateScoreFromState(matchState.State);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Quits the current match.
    /// </summary>
    public async Task QuitMatch()
    {
        Logger.WriteLine($"QuitMatch");

        // Ask Nakama to leave the match.
        await _nakamaConnection.Socket.LeaveMatchAsync(_currentMatch);

        // Reset the currentMatch and localUser variables.
        _currentMatch = null;
        _localUserPresence = null;

        // Destroy all existing player.
        foreach (var player in _players.Values)
            player.Destroy();

        // Clear the players array.
        _players.Clear();
    }

    void SpawnPlayer(string matchId, IUserPresence userPresence)
    {
        Logger.WriteLine($"SpawnPlayer: {userPresence}");

        // If the player has already been spawned, return early.
        if (_players.ContainsKey(userPresence.SessionId))
        {
            return;
        }

        // Set a variable to check if the player is the local player or not based on session ID.
        var isLocal = userPresence.SessionId == _localUserPresence.SessionId;

        Player player;

        // Setup the appropriate network data values if this is a remote player.
        // If this is our local player, add a listener for the PlayerDied event.
        if (isLocal)
        {
            player = new LocalPlayer();
            _localPlayer = player;

            SpawnedLocalPlayer?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            player = new NetworkPlayer
            {
                NetworkData = new RemotePlayerNetworkData
                {
                    MatchId = matchId,
                    User = userPresence
                }
            };

            SpawnedRemotePlayer?.Invoke(this, new SpawnedRemotePlayerEventArgs(userPresence.SessionId));
        }

        // Add the player to the players array.
        _players.Add(userPresence.SessionId, player);
    }

    void RemovePlayer(string sessionId)
    {
        if (!_players.ContainsKey(sessionId))
            return;

        _players.Remove(sessionId);

        RemovedPlayer?.Invoke(this, new RemovedPlayerEventArgs(sessionId));
    }

    /// <summary>
    /// Updates the player's velocity and position based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateRemotePaddleStateFromPacket(byte[] state, NetworkPlayer networkPlayer)
    {
        //TODO: fix the allocation here
        _packetReader.SetState(state);

        var totalSeconds = _packetReader.ReadSingle();

        var position = _packetReader.ReadVector2();
        var velocity = _packetReader.ReadVector2();

        var moveUp = _packetReader.ReadBoolean();
        var moveDown = _packetReader.ReadBoolean();

        ReceivedRemotePaddleState?.Invoke(
            this,
            new ReceivedRemotePaddleStateEventArgs(
                totalSeconds,
                position,
                velocity,
                moveUp,
                moveDown,
                networkPlayer.NetworkData.User.SessionId));
    }

    /// <summary>
    /// Updates the ball's direction and position based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateDirectionAndPositionFromState(byte[] state)
    {
        _packetReader.SetState(state);

        var direction = _packetReader.ReadSingle();
        var position = _packetReader.ReadVector2();

        ReceivedRemoteBallState?.Invoke(
            this,
            new ReceivedRemoteBallStateEventArgs(direction, position));
    }

    /// <summary>
    /// Updates the score based on incoming state data.
    /// </summary>
    /// <param name="state">The incoming state byte array.</param>
    private void UpdateScoreFromState(byte[] state)
    {
        _packetReader.SetState(state);

        var player1Score = _packetReader.ReadInt32();
        var player2Score = _packetReader.ReadInt32();

        ReceivedRemoteScore?.Invoke(
            this,
            new ReceivedRemoteScoreEventArgs(player1Score, player2Score));
    }

    /// <summary>
    /// Sends a match state message across the network.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="state">The stringified JSON state data.</param>
    public void SendMatchState(long opCode, string state)
    {
        _nakamaConnection.Socket.SendMatchStateAsync(_currentMatch.Id, opCode, state);
    }

    /// <summary>
    /// Sends a match state message across the network.
    /// </summary>
    /// <param name="opCode">The operation code.</param>
    /// <param name="state">The stringified JSON state data.</param>
    public void SendMatchState(long opCode, byte[] state)
    {
        _nakamaConnection.Socket.SendMatchStateAsync(_currentMatch.Id, opCode, state);
    }
}
