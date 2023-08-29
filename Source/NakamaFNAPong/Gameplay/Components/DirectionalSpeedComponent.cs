// Copyright Pumpkin Games Ltd. All Rights Reserved.

namespace NakamaFNAPong.Gameplay.Components;

public readonly record struct DirectionalSpeedComponent(
    float DirectionInRadians,
    float Speed
);