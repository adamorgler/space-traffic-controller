using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpDX.MediaFoundation;
using SpaceTrafficController.Core;
using SpaceTrafficController.GameObjects;
using SpaceTrafficController.Input;
using SpaceTrafficController.Simulation;
using SpaceTrafficController.UI;
using SpaceTrafficController.Utilities;
using System;

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
        private UIRenderer UIRenderer;


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
            SimulationRenderer = new SimulationRenderer(GraphicsDevice ,SpriteBatch, Camera);
            UIRenderer = new UIRenderer(GraphicsDevice, SpriteBatch);
            InputHandler = new InputHandler(Camera, GameState, UIRenderer);

            Fonts.DebugFont = Content.Load<SpriteFont>("DebugFont");
            Fonts.ManueverNode = Content.Load<SpriteFont>("ManueverNode");

            Test2();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            GameState.Update(gameTime);
            InputHandler.Update(gameTime);
            Camera.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            SpriteBatch.Begin(transformMatrix: Camera.GetTransform());
            SimulationRenderer.DrawWorld(GameState);
            SpriteBatch.End();

            SpriteBatch.Begin();
            UIRenderer.Draw(GameState);
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
            var stationAlt = 750e3;
            var stationAOP = 180d;

            var station = new Station(new Orbit(stationAlt, stationAlt, stationAOP.ToRadians(), 0d.ToRadians()))
            {
                Name = "Test Station"
            };

            var arrivialShip1 = new Ship(new Orbit(stationAlt - 40e3, stationAlt - 40e3, (stationAOP - 10).ToRadians(), 0d.ToRadians()))
            {
                Name = "Arrivial Ship1",
                Destination = new StationDestination(station)
            };

            var arrivialShip2 = new Ship(new Orbit(stationAlt+ 40e3, stationAlt + 40e3, (stationAOP + 10).ToRadians(), 0d.ToRadians()))
            {
                Name = "Arrivial Ship2",
                Destination = new StationDestination(station)
            };

            GameState.OrbitingObjects.Add(arrivialShip1);
            GameState.OrbitingObjects.Add(arrivialShip2);
            GameState.OrbitingObjects.Add(station);
        }

        private void Test2()
        {
            int minOrbit = 500000;
            int maxOrbit = 2000000;
            Random r = new Random();
            for (int i = 0; i < 100; i++)
            {
                GameState.OrbitingObjects.Add(
                    new Ship(
                        new Orbit(
                            r.Next(minOrbit, maxOrbit),
                            r.Next(minOrbit, maxOrbit),
                            (r.NextDouble() * 360d).ToRadians(),
                            (r.NextDouble() * 360d).ToRadians())
                        )
                    );
            }
        }

        private void Test3()
        {
            GameState.OrbitingObjects.Clear();

            const double controlAltitude = 600000d;

            GameState.OrbitingObjects.Add(new Ship(new Orbit(controlAltitude, controlAltitude, 180d.ToRadians(), 0d.ToRadians()))
            {
                Name = "Control Ship"
            });

            var escapeOrbit = new Orbit(
                apoapsis: double.PositiveInfinity,
                periapsis: controlAltitude,
                argumentOfPeriapsis: 0d.ToRadians(),
                trueAnomaly: 0d.ToRadians(),
                eccentricity: 1.1d);

            GameState.OrbitingObjects.Add(new Ship(escapeOrbit)
            {
                Name = "Escape Test Ship"
            });
        }
    }
}
