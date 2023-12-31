// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Gameplay.Components;
using System;

namespace NakamaFNAPong.Gameplay.Systems;

/// <summary>
/// Responsible for turning directional speed into a velocity.
/// </summary>
public class DirectionalSpeedSystem : MoonTools.ECS.System
{
    readonly Filter _filter;

    public DirectionalSpeedSystem(World world) : base(world)
    {
        _filter = FilterBuilder
           .Include<DirectionalSpeedComponent>()
           .Build();
    }

    public override void Update(TimeSpan delta)
    {
        foreach (var entity in _filter.Entities)
        {
            ref readonly var component = ref Get<DirectionalSpeedComponent>(entity);

            Set(entity, new VelocityComponent(VectorHelper.Polar(component.DirectionInRadians, component.Speed)));
        }
    }
}