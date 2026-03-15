using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
            InputHandler = new InputHandler(Camera, GameState);

            Fonts.DebugFont = Content.Load<SpriteFont>("DebugFont");
            Fonts.ManueverNode = Content.Load<SpriteFont>("ManueverNode");

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
            SimulationRenderer.DrawWorld(GameState);
            SpriteBatch.End();

            SpriteBatch.Begin();
            SimulationRenderer.DrawScreen(GameState);
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
            var station = new Station(new Orbit(600000d, 600000d, 185d.ToRadians(), 0d.ToRadians()))
            {
                Name = "Test Station"
            };

            var ship1 = new Ship(new Orbit(560000d, 560000d, 175d.ToRadians(), 0d.ToRadians()))
            {
                Name = "Test Ship1",
                Destination = new StationDestination(station)
            };

            var ship2 = new Ship(new Orbit(640000d, 640000d, 195d.ToRadians(), 0d.ToRadians()))
            {
                Name = "Test Ship2",
                Destination = new StationDestination(station)
            };

            GameState.OrbitingObjects.Add(ship1);
            GameState.OrbitingObjects.Add(ship2);
            GameState.OrbitingObjects.Add(station);
        }

        private void Test2()
        {
            int minOrbit = 500000;
            int maxOrbit = 5000000;
            Random r = new Random();
            for (int i = 0; i < 1000; i++)
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
