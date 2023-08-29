// Copyright Pumpkin Games Ltd. All Rights Reserved.

using MoonTools.ECS;

namespace NakamaFNAPong.Gameplay.Components;

public readonly record struct AngledBounceResponseComponent(
    Entity BouncedBy
);