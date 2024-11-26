﻿using Axolotl2D.Attributes;
using Axolotl2D.Drawable;
using Axolotl2D.Entities;
using Axolotl2D.Input;
using Axolotl2D.Services;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Example.Scenes
{
    [DefaultScene]
    public class ExampleScene : BaseScene
    {
        private const int QUAD_COUNT = 9;
        private const int MOVE_SPEED = 5;

        private float _currentXPos = 0;
        private bool _goesRight = true;

        private BaseDrawable? _object1;
        private BaseDrawable? _object2;
        private BaseDrawable? _object3;

        private Mouse? _mouse;
        private IKeyboard? _keyboard;

        private ILogger<ExampleScene> _logger;

        private IKeyboard kb;

        private ExampleGame _game;

        public ExampleScene(ExampleGame game, IServiceProvider services, ILogger<ExampleScene> logger) 
        {
            _game = game;
            kb = game.GetKeyboard()!;
            _game.Title = "Scene 1";

            this._logger = logger;
        }

        public override void Draw(double frameDelta, double frameRate)
        {
            for (int i = 0; i < QUAD_COUNT; i++)
            {
                BaseDrawable? thisSprite;
                if (i % 3 == 0)
                {
                    thisSprite = _object1;
                }
                else if (i % 3 == 1)
                {
                    thisSprite = _object2;
                }
                else
                {
                    thisSprite = _object3;
                }
                thisSprite!.Draw(new Vector2(_currentXPos, i * 74), new Vector2(50, 50));
            }
        }

        public override void Load()
        {
            // get streams for resources
            using var mochiCat = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.mochicat.png")!;
            using var rei = GetType().Assembly.GetManifestResourceStream("Axolotl2D.Example.Resources.Sprites.rei.png")!;

            _object1 = new Sprite(_game, mochiCat, new Vector2(0, 0), new Vector2(50, 50));
            _object2 = new Sprite(_game, rei, new Vector2(0, 0), new Vector2(50, 50));
            _object3 = new SimpleQuad(_game, new Vector2(0, 0), new Vector2(50, 50));

            _mouse = _game.GetMouse();
            _keyboard = _game.GetKeyboard();

            _logger.LogInformation("Loaded Example Scene");
        }

        public override void Resize(Vector2 size)
        {
            
        }

        public override void Unload()
        {
            Console.WriteLine("Unloaded Example Scene");
        }

        public override void Update(double frameDelta)
        {
            float maxX = _game.Viewport.X - 50;
            float deltaPosition = MOVE_SPEED * ((float)frameDelta * 60);
            _currentXPos += _goesRight ? deltaPosition : -deltaPosition;

            if (_currentXPos > maxX)
            {
                _goesRight = false;
            }
            else if (_currentXPos < 0)
            {
                _goesRight = true;
            }

            if (_mouse!.LeftButton == MouseKeyState.Click)
                _logger.LogInformation("Mouse Click");
            if (_mouse!.LeftButton == MouseKeyState.Release)
                _logger.LogInformation("Mouse Released");

            if (_mouse.LeftButton == MouseKeyState.Held || _keyboard!.IsKeyPressed(Key.Space))
                _game.ClearColor = Color.Red;
            else
                _game.ClearColor = Color.FromHTML("#0088FF");

            if (kb.IsKeyPressed(Key.Escape))
            {
                SceneGameHost.ChangeScene<ExampleScene2>();
            }
        }
    }
}