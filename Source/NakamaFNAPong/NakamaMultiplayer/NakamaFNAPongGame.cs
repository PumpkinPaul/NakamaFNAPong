// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Nakama;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Gameplay.GamePhases;

namespace NakamaFNAPong.NakamaMultiplayer;

/// <summary>
/// Very simple multiplayer implementation of the game, PONG using the Nakama framework, MoonTools.ECS and client-side prediction.
/// </summary>
/// <remarks>
/// Basing a solution using the Nakama documentation...
/// https://dotnet.docs.heroiclabs.com/html/index.html
/// https://heroiclabs.com/blog/unity-fishgame/
/// </remarks>
public class NakamaFNAPongGame : BaseGame
{
    public readonly GamePhaseManager GamePhaseManager;

    readonly PlayerProfile _playerProfile;

    //------------------------------------------------------------------------------------------------------------------------------------------------------ 
    //------------------------------------------------------------------------------------------------------------------------------------------------------ 
    //------------------------------------------------------------------------------------------------------------------------------------------------------ 
    //Multiplayer
    readonly NakamaConnection _nakamaConnection;
    readonly NetworkGameManager _networkGameManager;

    public NakamaFNAPongGame()
    {
        Window.Title = "Nakama Pong - ECS & Clientside Prediction";

        _playerProfile = PlayerProfile.LoadOrCreate(LocalApplicationDataPath);

        _nakamaConnection = new NakamaConnection(_playerProfile);
        _networkGameManager = new NetworkGameManager(_nakamaConnection);

        GamePhaseManager = new GamePhaseManager();
        GamePhaseManager.Add(new MainMenuPhase(_nakamaConnection));
        GamePhaseManager.Add(new PlayGamePhase(_networkGameManager));

        // Show the main menu, hide the in-game menu when player quits the match
        GamePhaseManager.Get<PlayGamePhase>().ExitedMatch += (sender, e) => GamePhaseManager.ChangePhase<MainMenuPhase>();
    }

    protected async override void Initialize()
    {
        base.Initialize();

        await _networkGameManager.Connect();
        _nakamaConnection.Socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;

        GamePhaseManager.Initialise();
        GamePhaseManager.ChangePhase<MainMenuPhase>();
    }

    /// <summary>
    /// Called when a MatchmakerMatched event is received from the Nakama server.
    /// </summary>
    /// <param name="matched">The MatchmakerMatched data.</param>
    public void OnReceivedMatchmakerMatched(IMatchmakerMatched matched)
    {
        Logger.WriteLine($"{nameof(NakamaFNAPongGame)}.{nameof(OnReceivedMatchmakerMatched)}");
        Logger.WriteLine($"Changing game phase to begin a new play session");

        GamePhaseManager.ChangePhase<PlayGamePhase>();
    }

    protected override void OnUpdate(GameTime gameTime)
    {
        if (KeyboardState.IsKeyDown(Keys.Escape) && PreviousKeyboardState.IsKeyUp(Keys.Escape))
            Exit();

        GamePhaseManager.Update(gameTime);
    }

    protected override void OnDraw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        GamePhaseManager.Draw();
    }
}
