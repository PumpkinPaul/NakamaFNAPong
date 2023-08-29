// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;
using NakamaFNAPong.Gameplay.Components;
using NakamaFNAPong.NakamaMultiplayer;
using System;
using System.Collections.Generic;

namespace NakamaFNAPong.Gameplay.Systems;

/// <summary>
/// Reads remote match data received messages and applies prediction to the 'simulation state' - e.g. the normal component data
/// </summary>
public sealed class PlayerNetworkRemoteApplyPredictionSystem : UpdatePaddleStateSystem
{
    public bool EnablePrediction { get; set; } = true;

    readonly Dictionary<Entity, RollingAverage> _clockDeltas = new();

    public PlayerNetworkRemoteApplyPredictionSystem(World world) : base(world)
    {
    }

    public override void Update(TimeSpan delta)
    {
        if (EnablePrediction == false)
            return;

        var oneFrame = TimeSpan.FromSeconds(1.0 / 60.0);

        foreach (var message in ReadMessages<ReceivedRemotePaddleStateMessage>())
        {
            //Estimate how long this packet took to arrive.
            //TODO! figure out how to do latency simulation using Nakama.
            var latency = TimeSpan.FromSeconds(1 / 20.0f);

            if (_clockDeltas.TryGetValue(message.Entity, out var clockDelta) == false)
            {
                clockDelta = new RollingAverage();
                _clockDeltas[message.Entity] = clockDelta;
            }

            // Work out the difference between our current local time
            // and the remote time at which this packet was sent.
            float localTime = (float)delta.TotalSeconds;

            float timeDelta = localTime - message.TotalSeconds;

            // Maintain a rolling average of time deltas from the last 100 packets.
            clockDelta.AddValue(timeDelta);

            // The caller passed in an estimate of the average network latency, which
            // is provided by the XNA Framework networking layer. But not all packets
            // will take exactly that average amount of time to arrive! To handle
            // varying latencies per packet, we include the send time as part of our
            // packet data. By comparing this with a rolling average of the last 100
            // send times, we can detect packets that are later or earlier than usual,
            // even without having synchronized clocks between the two machines. We
            // then adjust our average latency estimate by this per-packet deviation.

            float timeDeviation = timeDelta - clockDelta.AverageValue;

            latency += TimeSpan.FromSeconds(timeDeviation);

            // Apply prediction by updating our simulation state however
            // many times is necessary to catch up to the current time.
            ref var simulationState = ref GetMutable<SimulationStateComponent>(message.Entity);
            while (latency >= oneFrame)
            {
                UpdateState(message.Entity, ref simulationState.PaddleState);

                latency -= oneFrame;
            }
        }
    }
}
