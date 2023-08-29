// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MoonTools.ECS;
using NakamaFNAPong.Engine;
using NakamaFNAPong.Engine.Extensions;
using NakamaFNAPong.Gameplay.Renderers;
using NakamaFNAPong.Gameplay.Systems;
using System.Collections.Generic;

namespace NakamaFNAPong.NakamaMultiplayer;

/// <summary>
/// Encapsulates management of the ECS
/// </summary>
public class ECSManager
{
    readonly World _world;

    //Systems
    readonly MoonTools.ECS.System[] _systems;

    //Renderers
    readonly SpriteRenderer _spriteRenderer;

    readonly PlayerEntityMapper _playerEntityMapper;
    readonly NetworkGameManager _networkGameManager;
    readonly MultiplayerGameState _gameState;

    readonly Queue<LocalPlayerSpawnMessage> _localPlayerSpawnMessages = new();
    readonly Queue<RemotePlayerSpawnMessage> _remotePlayerSpawnMessages = new();
    readonly Queue<ReceivedRemotePaddleStateMessage> _matchDataVelocityAndPositionMessage = new();
    readonly Queue<MatchDataDirectionAndPositionMessage> _matchDataDirectionAndPositionMessage = new();
    readonly Queue<DestroyEntityMessage> _destroyEntityMessage = new();

    public ECSManager(
        NetworkGameManager networkGameManager,
        PlayerEntityMapper playerEntityMapper,
        MultiplayerGameState gameState)
    {
        _networkGameManager = networkGameManager;
        _playerEntityMapper = playerEntityMapper;
        _gameState = gameState;

        _world = new World();

        _systems = new MoonTools.ECS.System[]
        {
            //Spawn the entities into the game world
            new LocalPlayerSpawnSystem(_world),
            new RemotePlayerSpawnSystem(_world, _playerEntityMapper),
            new BallSpawnSystem(_world),
            new ScoreSpawnSystem(_world),

            new PlayerInputSystem(_world),   //Get input from devices and turn into game actions...
            new PlayerActionsSystem(_world), //...then process the actions (e.g. do a jump, fire a gun, etc)

            //Turn directions into velocity!
            new DirectionalSpeedSystem(_world),

            //Collisions processors
            new WorldCollisionSystem(_world, _gameState, new Point(BaseGame.SCREEN_WIDTH, BaseGame.SCREEN_HEIGHT)),
            new EntityCollisionSystem(_world, BaseGame.SCREEN_WIDTH),

            //Move the entities in the world
            new MovementSystem(_world),
            new BounceSystem(_world),
            new AngledBounceSystem(_world),

            //LateUpdate
            //...handle sending data to remote clients
            new GoalScoredLocalSyncSystem(_world, _networkGameManager, _gameState),
            new BallNetworkLocalSyncSystem(_world, _networkGameManager),

            //Phase #1
            //This is UpdateLocal gamer from Nakama.Tank
            new PlayerNetworkSendLocalStateSystem(_world, _networkGameManager),

            //Phase #2
            //...handle receiving data from remote clients
            new PlayerNetworkRemoteResetSmoothingSystem(_world),  //Reset the smoothing factor
            new PlayerNetworkRemoteSyncSystem(_world),            //Update the 'simulation' state
            new PlayerNetworkRemoteApplyPredictionSystem(_world), //Apply client side predication to the 'simulation' state
            
            //Phase #3
            new PlayerNetworkRemoteUpdateRemoteSystem(_world),
            new PlayerNetworkRemoteApplySmoothingSystem(_world),
            
            new BallNetworkRemoteSyncSystem(_world),
            new LerpPositionSystem(_world),

            //Remove the dead entities
            new DestroyEntitySystem(_world)
        };

        _spriteRenderer = new SpriteRenderer(_world, BaseGame.Instance.SpriteBatch);

        var color = Color.Cyan;

        _world.Send(new BallSpawnMessage(
            Position: new Vector2(BaseGame.SCREEN_WIDTH, BaseGame.SCREEN_HEIGHT) / 2,
            color
        ));

        _world.Send(new ScoreSpawnMessage(
            PlayerIndex: PlayerIndex.One,
            Position: new Vector2(BaseGame.SCREEN_WIDTH * 0.25f, 21)
        ));

        _world.Send(new ScoreSpawnMessage(
            PlayerIndex: PlayerIndex.Two,
            Position: new Vector2(BaseGame.SCREEN_WIDTH * 0.75f, 21)
        ));
    }

    public void SpawnLocalPlayer(Vector2 position, int bounceDirection)
    {
        //Queue entity creation in the ECS
        _localPlayerSpawnMessages.Enqueue(new LocalPlayerSpawnMessage(
            PlayerIndex: PlayerIndex.One,
            MoveUpKey: Keys.Q,
            MoveDownKey: Keys.A,
            Position: position,
            Color.Cyan,
            BounceDirection: bounceDirection
        ));
    }

    public void SpawnRemotePlayer(Vector2 position, int bounceDirection)
    {
        //Queue entity creation in the ECS
        _remotePlayerSpawnMessages.Enqueue(new RemotePlayerSpawnMessage(
            PlayerIndex: PlayerIndex.Two,
            Position: position,
            Color.Cyan * 0.25f,
            BounceDirection: bounceDirection
        ));
    }

    public void ReceivedRemotePaddleState(ReceivedRemotePaddleStateEventArgs e, string sessionId)
    {
        var entity = _playerEntityMapper.GetEntityFromSessionId(sessionId);

        if (entity == PlayerEntityMapper.INVALID_ENTITY)
            return;

        //Queue entity to begin lerping to the corrected position.
        _matchDataVelocityAndPositionMessage.Enqueue(new ReceivedRemotePaddleStateMessage(
            entity,
            e.TotalSeconds,
            e.Position,
            e.Velocity,
            e.MoveUp,
            e.MoveDown
        ));
    }

    public void ReceivedRemoteBallState(float direction, Vector2 position)
    {
        _matchDataDirectionAndPositionMessage.Enqueue(new MatchDataDirectionAndPositionMessage(
            direction,
            position
        ));
    }

    public void DestroyEntity(string sessionId)
    {
        var entity = _playerEntityMapper.GetEntityFromSessionId(sessionId);

        if (entity == PlayerEntityMapper.INVALID_ENTITY)
            return;

        _playerEntityMapper.RemovePlayerBySessionId(sessionId);

        //Queue entity to begin lerping to the corrected position.
        _destroyEntityMessage.Enqueue(new DestroyEntityMessage(
            Entity: entity
        ));
    }

    public void Update()
    {
        SendAllQueuedMessages();

        foreach (var system in _systems)
            system.Update(BaseGame.Instance.TargetElapsedTime);

        _world.FinishUpdate();
    }

    private void SendAllQueuedMessages()
    {
        SendMessages(_localPlayerSpawnMessages);
        SendMessages(_remotePlayerSpawnMessages);
        SendMessages(_matchDataVelocityAndPositionMessage);
        SendMessages(_matchDataDirectionAndPositionMessage);
        SendMessages(_destroyEntityMessage);
    }

    private void SendMessages<T>(Queue<T> messages) where T : unmanaged
    {
        while (messages.Count > 0)
            _world.Send(messages.Dequeue());
    }

    public void Draw()
    {
        var spriteBatch = BaseGame.Instance.SpriteBatch;

        spriteBatch.BeginTextRendering();

        //Draw the world

        //...all the entities
        _spriteRenderer.Draw();

        //...play area
        spriteBatch.DrawLine(new Vector2(BaseGame.SCREEN_WIDTH / 2, 0), new Vector2(BaseGame.SCREEN_WIDTH / 2, BaseGame.SCREEN_HEIGHT), Color.Cyan);

        //..."HUD"
        spriteBatch.DrawText(Resources.GameFont, _gameState.Player1Score.ToString(), new Vector2(BaseGame.SCREEN_WIDTH * 0.25f, BaseGame.SCREEN_HEIGHT - 48), Color.Cyan, Alignment.Centre);
        spriteBatch.DrawText(Resources.GameFont, _gameState.Player2Score.ToString(), new Vector2(BaseGame.SCREEN_WIDTH * 0.75f, BaseGame.SCREEN_HEIGHT - 48), Color.Cyan, Alignment.Centre);

        spriteBatch.End();
    }
}
