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
        public int X
        {
            get
            {
                tryInitialize();
                return _x;
            }
            private set => _x = value;
        }
        public int Y
        {
            get
            {
                tryInitialize();
                return _y;
            }
            private set => _y = value;
        }
        public int ScrollX
        {
            get
            {
                tryInitialize();
                return _scrollX;
            }
            private set => _scrollX = value;
        }
        public int ScrollY
        {
            get
            {
                tryInitialize();
                return _scrollY;
            }
            private set => _scrollY = value;
        }

        public MouseKeyState LeftButton
        {
            get
            {
                tryInitialize();
                return _leftButton;
            }
            private set => _leftButton = value;
        }
        public MouseKeyState RightButton
        {
            get
            {
                tryInitialize();
                return _rightButton;
            }
            private set => _rightButton = value;
        }
        public MouseKeyState MiddleButton
        {
            get
            {
                tryInitialize();
                return _middleButton;
            }
            private set => _middleButton = value;
        }

        private int _x;
        private int _y;
        private int _scrollX;
        private int _scrollY;
        private MouseKeyState _leftButton = MouseKeyState.Unheld;
        private MouseKeyState _rightButton = MouseKeyState.Unheld;
        private MouseKeyState _middleButton = MouseKeyState.Unheld;

        private MouseKeyState _previousLeftButton = MouseKeyState.Unheld;
        private MouseKeyState _previousRightButton = MouseKeyState.Unheld;
        private MouseKeyState _previousMiddleButton = MouseKeyState.Unheld;

        private ILazyDependencyLoader<Game> _game;
        private IMouse? _mouse;

        public Mouse(ILazyDependencyLoader<Game> game)
        {
            _game = game;
        }

        private void tryInitialize()
        {
            if (_mouse is null)
            {
                if (!_game.IsLoaded)
                    return;

                _mouse = _game.Value._input!.Mice[0];
                _mouse.MouseUp += mouseUp;
                _mouse.MouseDown += mouseDown;
                _mouse.MouseMove += mouseMove;
                _game.Value.OnUpdate += gameUpdate;
            }
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

            if(_game.IsLoaded)
                _game.Value.OnUpdate -= gameUpdate;
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
