// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Gameplay.Components;
using System;

namespace NakamaFNAPong.Gameplay.Systems;

/// <summary>
/// Applies smoothing by interpolating the display state somewhere
/// in between the previous state and current simulation state.
/// </summary>
public sealed class PlayerNetworkRemoteUpdateRemoteSystem : UpdatePaddleStateSystem
{
    public bool EnablePrediction { get; set; } = true;

    readonly Filter _filter;

    public PlayerNetworkRemoteUpdateRemoteSystem(World world) : base(world)
    {
        _filter = FilterBuilder
            .Include<SmoothingComponent>()
            .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in _filter.Entities)
        {
            // Update the smoothing amount, which interpolates from the previous
            // state toward the current simultation state. The speed of this decay
            // depends on the number of frames between packets: we want to finish
            // our smoothing interpolation at the same time the next packet is due.
            //float smoothingDecay = 1.0f / framesBetweenPackets;
            float smoothingDecay = (float)BaseGame.Instance.TargetElapsedTime.TotalSeconds * PlayerNetworkSendLocalStateSystem.UPDATES_PER_SECOND;

            ref var smoothing = ref GetMutable<SmoothingComponent>(entity);

            smoothing.Value -= smoothingDecay;

            if (smoothing.Value < 0)
                smoothing.Value = 0;

            if (EnablePrediction)
            {
                // Predict how the remote paddle will move by updating our local copy of its simultation state.
                ref var simulationState = ref GetMutable<SimulationStateComponent>(entity);
                UpdateState(entity, ref simulationState.PaddleState);

                // If both smoothing and prediction are active, also apply prediction to the previous state.
                if (smoothing.Value > 0)
                {
                    ref var previousState = ref GetMutable<PreviousStateComponent>(entity);
                    UpdateState(entity, ref previousState.PaddleState);
                        
                }
            }

            if (smoothing.Value == 0)
            {
                ref var simulationState = ref GetMutable<SimulationStateComponent>(entity);
                Set(entity, new DisplayStateComponent
                {
                    PaddleState = simulationState.PaddleState
                });
            }
        }
    }
}
