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

        public MouseKeyState LeftButton { get; private set; }
        public MouseKeyState RightButton { get; private set; }
        public MouseKeyState MiddleButton { get; private set; }

        private MouseKeyState _previousLeftButton = MouseKeyState.Unheld;
        private MouseKeyState _previousRightButton = MouseKeyState.Unheld;
        private MouseKeyState _previousMiddleButton = MouseKeyState.Unheld;

        private Game _game;
        private IMouse? _mouse;

        public Mouse(Game game)
        {
            _game = game;
            _mouse = game._input?.Mice[0]!;
            _mouse.MouseUp += mouseUp;
            _mouse.MouseDown += mouseDown;
            _mouse.MouseMove += mouseMove;
            _game.OnUpdate += gameUpdate;
        }

        private void gameUpdate(double frameDelta)
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

        private void mouseMove(IMouse mouse, Vector2 position)
        {
            X = (int)position.X;
            Y = (int)position.Y;
        }

        private void mouseDown(IMouse mouse, MouseButton button)
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

        private void mouseUp(IMouse mouse, MouseButton button)
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
                _mouse.MouseUp -= mouseUp;
                _mouse.MouseDown -= mouseDown;
                _mouse.MouseMove -= mouseMove;
            }

            _game.OnUpdate -= gameUpdate;
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
