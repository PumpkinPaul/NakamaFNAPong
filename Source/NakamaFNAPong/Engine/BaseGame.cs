// Copyright Pumpkin Games Ltd. All Rights Reserved.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NakamaFNAPong.Engine.DebugTools;
using NakamaFNAPong.Engine.Threading;
using SpriteFontPlus;
using System;
using System.IO;

namespace NakamaFNAPong.Engine;

public abstract class BaseGame : Game
{
    public const int SCREEN_WIDTH = 800;
    public const int SCREEN_HEIGHT = 452;

    public static readonly string InternalName = Path.GetFileNameWithoutExtension(ApplicationInformation.ExecutingAssembly.Location);

    public static string LocalApplicationDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InternalName);

    public static BaseGame Instance { get; private set; }

    public static readonly Random Random = new();

    public GraphicsDeviceManager Graphics { get; }
    public SpriteBatch SpriteBatch { get; private set; }

    public Matrix ModelMatrix = Matrix.Identity;
    public Matrix ViewMatrix = Matrix.Identity;
    public Matrix ProjectionMatrix;
    public Matrix TextMatrix;

    public KeyboardState KeyboardState;
    public KeyboardState PreviousKeyboardState;

    public GamePadState GamePadState;
    public GamePadState PreviousGamePadState;

    protected MouseState MouseState;
    protected MouseState PreviousMouseState;

    public BasicEffect BasicEffect { get; private set; }

    protected Effect EntityShader;

    public bool IsPressed(Keys key, Buttons button)
    {
        return (KeyboardState.IsKeyDown(key) && PreviousKeyboardState.IsKeyUp(key))
            || (GamePadState.IsButtonDown(button) && PreviousGamePadState.IsButtonUp(button));
    }

    protected BaseGame()
    {
        Instance = this;

        Graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";

        Graphics.IsFullScreen = false;
        Graphics.PreferredBackBufferWidth = SCREEN_WIDTH;
        Graphics.PreferredBackBufferHeight = SCREEN_HEIGHT;

        Graphics.DeviceReset += OnGraphicsDeviceReset;
    }

    protected virtual void OnGraphicsDeviceReset(object sender, EventArgs e) { }

    // Basic Project matrix
    // --------------------
    // The x, y position of the world relates to the left, bottom properties of the screen and is located at 0, 0
    // Hopefully this will make working in a 2d world with origin at lower left much easier than trying to retrofit the XNA Rectangle class where bottom > top. <summary>
    // The x,y position of the world relates to the left, bottom properties of the screen
    //
    //                         width, height
    //     -------------------------
    //     |                       |
    //     |                       |
    //     |                       |
    //     |                       |
    //     |                       |
    //     |                       |
    //     -------------------------
    // 0, 0
    protected override void Initialize()
    {
        DebugSystem.Initialize(this, "SpriteFonts/Debug");

        ProjectionMatrix = Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, 0, GraphicsDevice.Viewport.Height, 0, 1);

        TextMatrix = Matrix.Identity;
        TextMatrix.Translation = new Vector3(0, GraphicsDevice.Viewport.Height, 1f);
        TextMatrix.Down = new Vector3(0, 1, 0);

        //Calls LoadContent
        base.Initialize();
    }

    protected override void LoadContent()
    {
        // Create a new SpriteBatch, which can be used to draw textures.
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        Resources.PixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        Resources.PixelTexture.SetData(new[] { Color.White });

        Resources.DefaultSpriteFont = Content.Load<SpriteFont>("SpriteFonts/debug");

        BasicEffect = new BasicEffect(GraphicsDevice)
        {
            World = ModelMatrix,
            View = ViewMatrix,
            Projection = ProjectionMatrix,
            TextureEnabled = false,
            VertexColorEnabled = true
        };

        var fontPath = Path.Combine(Path.GetFullPath("."), "Content", "Fonts", "SquaredDisplay.ttf");
        Resources.GameFont = TtfFontBaker.Bake(
            File.ReadAllBytes(fontPath),
            96,
            1024,
            1024,
            new[] {
                CharacterRange.BasicLatin,
                CharacterRange.Latin1Supplement,
                CharacterRange.LatinExtendedA,
                CharacterRange.Cyrillic
           }).CreateSpriteFont(GraphicsDevice);

        Resources.SmallFont = TtfFontBaker.Bake(
            File.ReadAllBytes(fontPath),
            32,
            1024,
            1024,
            new[] {
                CharacterRange.BasicLatin,
                CharacterRange.Latin1Supplement,
                CharacterRange.LatinExtendedA,
                CharacterRange.Cyrillic
           }).CreateSpriteFont(GraphicsDevice);

        OnLoadContent();
    }

    protected virtual void OnLoadContent() { }

    protected override void Update(GameTime gameTime)
    {
        DebugSystem.BeginUpdate();

        ConsoleSynchronizationContext.Update();
        ConsoleMainThreadDispatcher.Update();

        using (new CodeTimer("BaseGame.Update", FlatTheme.PeterRiver))
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            KeyboardState = Keyboard.GetState();
            MouseState = Mouse.GetState();
            GamePadState = GamePad.GetState(PlayerIndex.One);

            OnUpdate(gameTime);

            PreviousKeyboardState = KeyboardState;
            PreviousMouseState = MouseState;
            PreviousGamePadState = GamePadState;

            base.Update(gameTime);
        }
    }

    protected virtual void OnUpdate(GameTime gameTime) { }


    /// <summary>
    /// This is called when the game should draw itself.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Draw(GameTime gameTime)
    {
        using (new CodeTimer("BaseGame.", Color.Magenta))
        {
            GraphicsDevice.Clear(Color.Black);

            OnDraw(gameTime);

            base.Draw(gameTime);

            DebugSystem.Draw();
        }
    }

    protected virtual void OnDraw(GameTime gameTime) { }

    protected override void OnExiting(object sender, EventArgs args)
    {
        DebugSystem.Exiting();
    }
}
