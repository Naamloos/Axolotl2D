using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Input
{
    public class Mouse : IDisposable
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int ScrollX { get; private set; }
        public int ScrollY { get; private set; }

        public MouseKeyState LeftButton { get; private set; } = MouseKeyState.Unheld;
        public MouseKeyState RightButton { get; private set; } = MouseKeyState.Unheld;
        public MouseKeyState MiddleButton { get; private set; } = MouseKeyState.Unheld;

        private MouseKeyState _previousLeftButton = MouseKeyState.Unheld;
        private MouseKeyState _previousRightButton = MouseKeyState.Unheld;
        private MouseKeyState _previousMiddleButton = MouseKeyState.Unheld;

        private readonly Game _game;
        private readonly IMouse? _mouse;

        public Mouse(Game game)
        {
            _game = game;
            _mouse = game._input?.Mice[0]!;
            _mouse.MouseUp += MouseUp;
            _mouse.MouseDown += MouseDown;
            _mouse.MouseMove += MouseMove;
            _game.OnUpdate += GameUpdate;
        }

        private void GameUpdate(double frameDelta)
        {
            if(LeftButton == MouseKeyState.Click && _previousLeftButton != MouseKeyState.Click)
            {
                LeftButton = MouseKeyState.Held;
            }
            if(RightButton == MouseKeyState.Click && _previousRightButton != MouseKeyState.Click)
            {
                RightButton = MouseKeyState.Held;
            }
            if (MiddleButton == MouseKeyState.Click && _previousMiddleButton != MouseKeyState.Click)
            {
                MiddleButton = MouseKeyState.Held;
            }

            if (LeftButton == MouseKeyState.Release && _previousLeftButton != MouseKeyState.Release)
            {
                LeftButton = MouseKeyState.Unheld;
            }
            if (RightButton == MouseKeyState.Release && _previousRightButton != MouseKeyState.Release)
            {
                RightButton = MouseKeyState.Unheld;
            }
            if (MiddleButton == MouseKeyState.Release && _previousMiddleButton != MouseKeyState.Release)
            {
                MiddleButton = MouseKeyState.Unheld;
            }

            _previousLeftButton = LeftButton;
            _previousRightButton = RightButton;
            _previousMiddleButton = MiddleButton;
        }

        private void MouseMove(IMouse mouse, Vector2 position)
        {
            X = (int)position.X;
            Y = (int)position.Y;
        }

        private void MouseDown(IMouse mouse, MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    LeftButton = MouseKeyState.Click;
                    break;
                case MouseButton.Right:
                    RightButton = MouseKeyState.Click;
                    break;
                case MouseButton.Middle:
                    MiddleButton = MouseKeyState.Click;
                    break;
            }
        }

        private void MouseUp(IMouse mouse, MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    LeftButton = MouseKeyState.Release;
                    break;
                case MouseButton.Right:
                    RightButton = MouseKeyState.Release;
                    break;
                case MouseButton.Middle:
                    MiddleButton = MouseKeyState.Release;
                    break;
            }
        }

        public void Dispose()
        {
            if (_mouse is not null)
            {
                _mouse.MouseUp -= MouseUp;
                _mouse.MouseDown -= MouseDown;
                _mouse.MouseMove -= MouseMove;
            }

            _game.OnUpdate -= GameUpdate;
            GC.SuppressFinalize(this);
        }

        ~Mouse()
        {
            Dispose();
        }
    }

    public enum MouseKeyState
    {
        Click,
        Release,
        Held,
        Unheld
    }
}
