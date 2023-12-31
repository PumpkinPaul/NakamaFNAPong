// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Engine.Extensions;
using NakamaFNAPong.NakamaMultiplayer;

namespace NakamaFNAPong.Gameplay.GamePhases;

/// <summary>
/// Main Menu Processing
/// </summary>
public class MainMenuPhase : GamePhase
{
    enum Phase
    {
        Ready,
        FindMatch
    }

    Phase _phase = Phase.Ready;

    readonly NakamaConnection _nakamaConnection;

    public MainMenuPhase(
        NakamaConnection nakamaConnection)
    {
        _nakamaConnection = nakamaConnection;
    }

    protected override void OnCreate()
    {
        base.OnCreate();

        _phase = Phase.Ready;
    }

    protected async override void OnUpdate(GameTime gameTime)
    {
        base.OnUpdate(gameTime);

        if (BaseGame.Instance.KeyboardState.IsKeyDown(Keys.Space) && BaseGame.Instance.PreviousKeyboardState.IsKeyUp(Keys.Space))
        {
            if (_phase == Phase.Ready)
            {
                _phase = Phase.FindMatch;
                await _nakamaConnection.FindMatch();
            }
            else
            {
                _phase = Phase.Ready;
                await _nakamaConnection.CancelMatchmaking();
            }
        }
    }

    protected override void OnDraw()
    {
        base.OnDraw();

        var spriteBatch = BaseGame.Instance.SpriteBatch;

        var centreX = BaseGame.SCREEN_WIDTH * 0.5f;

        //Draw the UI
        spriteBatch.BeginTextRendering();

        spriteBatch.DrawText(Resources.GameFont, "Pong", new Vector2(centreX, BaseGame.SCREEN_HEIGHT * 0.65f), Color.Cyan, Alignment.Centre);

        switch (_phase)
        {
            case Phase.Ready:
                spriteBatch.DrawText(Resources.SmallFont, "Press SPACE to play!", new Vector2(centreX, 220), Color.Cyan, Alignment.Centre);
                break;

            case Phase.FindMatch:
                spriteBatch.DrawText(Resources.SmallFont, "Searching for match", new Vector2(centreX, 220), Color.Cyan, Alignment.Centre);
                spriteBatch.DrawText(Resources.SmallFont, "Press SPACE to cancel", new Vector2(centreX, 180), Color.Cyan, Alignment.Centre);
                break;
        }

        spriteBatch.End();
    }
}
