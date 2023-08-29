// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Nakama;

namespace NakamaFNAPong.Gameplay.Players;

public class RemotePlayerNetworkData
{
    public string MatchId { get; init; }
    public IUserPresence User { get; init; }
}