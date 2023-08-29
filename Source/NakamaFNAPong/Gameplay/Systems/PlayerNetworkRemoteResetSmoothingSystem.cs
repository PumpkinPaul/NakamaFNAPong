// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;
using NakamaFNAPong.Gameplay.Components;
using System;

namespace NakamaFNAPong.Gameplay.Systems;

/// <summary>
/// Reads remote match data received messages and applies prediction to the 'simulation state' - e.g. the normal component data
/// </summary>
public sealed class PlayerNetworkRemoteResetSmoothingSystem : UpdatePaddleStateSystem
{
    public bool EnableSmoothing { get; set; } = true;

    public PlayerNetworkRemoteResetSmoothingSystem(World world) : base(world)
    {
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var message in ReadMessages<ReceivedRemotePaddleStateMessage>())
        {
            if (EnableSmoothing)
            {
                // Start a new smoothing interpolation from our current state toward this new state we just received.
                ref readonly var displayState = ref Get<DisplayStateComponent>(message.Entity);

                Set(message.Entity, new PreviousStateComponent
                {
                    PaddleState = displayState.PaddleState
                });

                Set(message.Entity, new SmoothingComponent(1));
            }
            else
            {
                Set(message.Entity, new SmoothingComponent(0));
            }
        }
    }
}
