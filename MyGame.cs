using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Input;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.UI;
using SpaceTrafficController.Utilities;

namespace SpaceTrafficController
{
    public class MyGame : Game
    {
        private GraphicsDeviceManager Graphics;
        private SpriteBatch SpriteBatch;
        private Camera2D Camera;
        private InputHandler InputHandler;

        private GameState GameState;
        private SimulationRenderer SimulationRenderer;


        public MyGame()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            GameState = new GameState();
        }

        protected override void Initialize()
        {
            SetWindowToNearlyFullscreen();

            GameState.Init();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            Camera = new Camera2D(GraphicsDevice);
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            SimulationRenderer = new SimulationRenderer(SpriteBatch, Camera);            
            InputHandler = new InputHandler(Camera, GameState);

            Test1();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            GameState.Update(gameTime);
            InputHandler.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            SpriteBatch.Begin(transformMatrix: Camera.GetTransform());
            SimulationRenderer.Draw(GameState);
            SpriteBatch.End();

            base.Draw(gameTime);
        }

        private void SetWindowToNearlyFullscreen()
        {
            var scale = 0.9;
            var screenWidth = (int) (GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width * scale);
            var screenHeight = (int) (GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height * scale);

            Graphics.PreferredBackBufferWidth = screenWidth;
            Graphics.PreferredBackBufferHeight = screenHeight;
            Graphics.IsFullScreen = false;
            Graphics.ApplyChanges();
        }


        private void Test1()
        {
            GameState.OrbitingObjects.Add(new Ship(new Orbit(2000000, 100000, 0d.ToRadians(), 180d.ToRadians())));
        }
    }
}
