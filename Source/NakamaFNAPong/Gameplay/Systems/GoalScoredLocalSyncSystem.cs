// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;
using NakamaFNAPong.Gameplay.Players;
using NakamaFNAPong.NakamaMultiplayer;
using System;

namespace NakamaFNAPong.Gameplay.Systems;

public readonly record struct GoalScoredMessage(
    int Player1Increment,
    int Player2Increment
);

/// <summary>
/// Updates local game state and send updates to the other clients.
/// </summary>
public class GoalScoredLocalSyncSystem : MoonTools.ECS.System
{
    readonly NetworkGameManager _networkGameManager;
    readonly MultiplayerGameState _gameState;

    public GoalScoredLocalSyncSystem(
        World world,
        NetworkGameManager networkGameManager,
        MultiplayerGameState gameState
    ) : base(world)
    {
        _networkGameManager = networkGameManager;
        _gameState = gameState;
    }

    public override void Update(TimeSpan delta)
    {
        if (!_networkGameManager.IsHost)
            return;

        var sendScores = false;

        foreach (var message in ReadMessages<GoalScoredMessage>())
        {
            sendScores = true;

            _gameState.Player1Score += message.Player1Increment;
            _gameState.Player2Score += message.Player2Increment;
        }

        if (!sendScores)
            return;

        // Send a network packet containing the latest scores
        _networkGameManager.SendMatchState(
            OpCodes.SCORE_EVENT,
            MatchDataJson.Score(_gameState.Player1Score, _gameState.Player2Score));
    }
}