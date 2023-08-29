// Copyright Pumpkin Games Ltd. All Rights Reserved.

//Based on code from the FishGame Unity sample from Herioc Labs.
//https://github.com/heroiclabs/fishgame-unity/blob/main/FishGame/Assets/Entities/Player/PlayerNetworkLocalSync.cs

using Microsoft.Xna.Framework;
using MoonTools.ECS;
using NakamaFNAPong.Engine.IO;
using NakamaFNAPong.Gameplay.Components;
using NakamaFNAPong.Gameplay.Players;
using NakamaFNAPong.NakamaMultiplayer;
using System;
using System.IO;

namespace NakamaFNAPong.Gameplay.Systems;

/// <summary>
/// Syncs the local player's state across the network by sending frequent network packets containing relevent 
/// information such as velocity, position and game actions (jump, shoot, crouch, etc).
/// </summary>
public sealed class PlayerNetworkSendLocalStateSystem : MoonTools.ECS.System
{
    readonly NetworkGameManager _networkGameManager;

    // How often to send the player's velocity and position across the network, in seconds.
    const int UPDATES_PER_SECOND = 10;
    readonly float StateFrequency = 1.0f / UPDATES_PER_SECOND;
    float _stateSyncTimer;

    //Packet writer to writer all paddle state required each tick - 44 bytes currently
    readonly PacketWriter _packetWriter = new(new MemoryStream(44));

    readonly Filter _filter;

    public PlayerNetworkSendLocalStateSystem(
        World world,
        NetworkGameManager networkGameManager
    ) : base(world)
    {
        _networkGameManager = networkGameManager;

        _filter = FilterBuilder
            .Include<PositionComponent>()
            .Include<VelocityComponent>()
            .Include<PlayerActionsComponent>()
            .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in _filter.Entities)
        {
            ref readonly var position = ref Get<PositionComponent>(entity);
            ref readonly var velocity = ref Get<VelocityComponent>(entity);
            ref readonly var playerActions = ref Get<PlayerActionsComponent>(entity);

            // Send the players current velocity and position every StateFrequency seconds.
            if (_stateSyncTimer <= 0)
            {
                _packetWriter.Reset();
                _packetWriter.Write((float)delta.TotalSeconds);

                // Send the current state of the paddle.
                _packetWriter.Write(position.Value);
                _packetWriter.Write(velocity.Value);

                // Also send our current inputs. These can be used to more accurately
                // predict how the paddle is likely to move in the future.
                _packetWriter.Write(playerActions.MoveUp);
                _packetWriter.Write(playerActions.MoveDown);

                // Send a network packet containing the player's state.
                _networkGameManager.SendMatchState(
                    OpCodes.PADDLE_PACKET,
                    _packetWriter.GetBuffer());

                _stateSyncTimer = StateFrequency;
            }

            _stateSyncTimer -= (float)delta.TotalSeconds;
        }
    }
}
