using Axolotl2D.Assets;
using Axolotl2D.Input;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Shaders;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

[DefaultScene]
public sealed class BletrisScene(
    AssetManager assets,
    SpriteBatch spriteBatch,
    InputActionMap input,
    ShaderLibrary shaders) : BaseScene
{
    private static readonly Color[] PieceColors =
    [
        Color.FromHTML("#45D9E8"),
        Color.FromHTML("#F5D547"),
        Color.FromHTML("#B16CE2"),
        Color.FromHTML("#59C96C"),
        Color.FromHTML("#ED5D68"),
        Color.FromHTML("#5486E8"),
        Color.FromHTML("#F3984B")
    ];
    private static readonly int[] RotationKicks = [0, -1, 1, -2, 2];
    private static readonly int[] LineScores = [0, 100, 300, 500, 800];
    private static readonly Color EmptyColor = new(0.22f, 0.29f, 0.42f, 0.18f);

    private readonly BletrisBoard board = new();
    private Sprite block = null!;
    private ShaderProgram colorShader = null!;
    private InputAction left = null!;
    private InputAction right = null!;
    private InputAction down = null!;
    private InputAction rotate = null!;
    private InputAction hardDrop = null!;
    private InputAction restart = null!;
    private int kind;
    private int rotation;
    private int pieceX;
    private int pieceY;
    private int score;
    private int lines;
    private int horizontalDirection;
    private double horizontalRepeat;
    private double dropTimer;
    private bool gameOver;

    public override void Load()
    {
        Game.ClearColor = Color.FromHTML("#111827");
        block = new Sprite(assets.Get<Texture2D>("block"));
        colorShader = shaders.Create(VertexShader, ColorFragmentShader);
        left = input.BindButton("Move left", Key.Left, Key.A);
        right = input.BindButton("Move right", Key.Right, Key.D);
        down = input.BindButton("Soft drop", Key.Down, Key.S);
        rotate = input.BindButton("Rotate", Key.Up, Key.W, Key.X);
        hardDrop = input.BindButton("Hard drop", Key.Space);
        restart = input.BindButton("Restart", Key.R);
        Reset();
    }

    public override void Update(double deltaTime)
    {
        if (restart.WasPressedThisFrame)
            Reset();
        if (gameOver)
            return;

        if (rotate.WasPressedThisFrame)
            TryRotate();
        UpdateHorizontal(deltaTime);

        if (hardDrop.WasPressedThisFrame)
        {
            while (TryMove(0, 1)) { }
            LockPiece();
            return;
        }

        var interval = Math.Max(0.08, 0.65 - lines / 10 * 0.05);
        if (down.IsPressed)
            interval /= 12;
        dropTimer += deltaTime;
        while (dropTimer >= interval && !gameOver)
        {
            dropTimer -= interval;
            if (!TryMove(0, 1))
            {
                LockPiece();
                break;
            }
        }
    }

    public override void Draw(double frameDelta, double frameRate)
    {
        var cellSize = Math.Max(8f, Math.Min(32f,
            Math.Min((Game.Viewport.X - 40f) / BletrisBoard.Width,
                (Game.Viewport.Y - 40f) / BletrisBoard.Height)));
        var topLeft = (Game.Viewport - new Vector2(
            BletrisBoard.Width * cellSize, BletrisBoard.Height * cellSize)) / 2f;
        var drawSize = new Vector2(cellSize - 2f);

        using (spriteBatch.UseShader(colorShader))
        {
            for (var row = 0; row < BletrisBoard.Height; row++)
                for (var column = 0; column < BletrisBoard.Width; column++)
                {
                    var value = board[column, row];
                    DrawCell(column, row, value == 0 ? EmptyColor : PieceColors[value - 1]);
                }

            if (!gameOver)
                foreach (var (column, row) in board.PieceCells(kind, rotation, pieceX, pieceY))
                    if (row >= 0)
                        DrawCell(column, row, PieceColors[kind]);
        }

        void DrawCell(int column, int row, Color color) =>
            spriteBatch.Draw(block,
                topLeft + new Vector2((column + 0.5f) * cellSize, (row + 0.5f) * cellSize),
                drawSize, tint: color, space: CoordinateSpace.Screen);
    }

    private void UpdateHorizontal(double deltaTime)
    {
        var direction = (right.IsPressed ? 1 : 0) - (left.IsPressed ? 1 : 0);
        if (direction != horizontalDirection)
        {
            horizontalDirection = direction;
            horizontalRepeat = 0.16;
            if (direction != 0)
                TryMove(direction, 0);
        }
        else if (direction != 0 && (horizontalRepeat -= deltaTime) <= 0)
        {
            TryMove(direction, 0);
            horizontalRepeat += 0.06;
        }
    }

    private bool TryMove(int x, int y)
    {
        if (!board.Fits(kind, rotation, pieceX + x, pieceY + y))
            return false;
        pieceX += x;
        pieceY += y;
        return true;
    }

    private void TryRotate()
    {
        var nextRotation = (rotation + 1) % 4;
        foreach (var kick in RotationKicks)
            if (board.Fits(kind, nextRotation, pieceX + kick, pieceY))
            {
                rotation = nextRotation;
                pieceX += kick;
                return;
            }
    }

    private void LockPiece()
    {
        if (!board.Place(kind, rotation, pieceX, pieceY))
        {
            gameOver = true;
            UpdateTitle();
            return;
        }

        var cleared = board.ClearFullRows();
        score += LineScores[cleared] * (lines / 10 + 1);
        lines += cleared;
        SpawnPiece();
        UpdateTitle();
    }

    private void SpawnPiece()
    {
        kind = Random.Shared.Next(PieceColors.Length);
        rotation = 0;
        pieceX = BletrisBoard.Width / 2;
        pieceY = 0;
        dropTimer = 0;
        gameOver = !board.Fits(kind, rotation, pieceX, pieceY);
    }

    private void Reset()
    {
        board.Reset();
        score = 0;
        lines = 0;
        horizontalDirection = 0;
        horizontalRepeat = 0;
        gameOver = false;
        SpawnPiece();
        UpdateTitle();
    }

    private void UpdateTitle() => Game.Title = gameOver
        ? $"Bletris | Game over | Score {score} | R to restart"
        : $"Bletris | Score {score} | Lines {lines} | Arrows/WASD, Space to drop";

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec2 aTextureCoord;
        layout (location = 2) in vec4 aColor;
        out vec2 frag_texCoords;
        out vec4 frag_color;
        void main() {
            gl_Position = vec4(aPosition, 1.0);
            frag_texCoords = aTextureCoord;
            frag_color = aColor;
        }
        """;

    private const string ColorFragmentShader = """
        #version 330 core
        uniform sampler2D uTexture;
        in vec2 frag_texCoords;
        in vec4 frag_color;
        out vec4 out_color;
        void main() {
            vec4 texel = texture(uTexture, frag_texCoords);
            float detail = mix(0.55, 1.0, dot(texel.rgb, vec3(0.2126, 0.7152, 0.0722)));
            out_color = vec4(frag_color.rgb * detail, texel.a * frag_color.a);
        }
        """;
}
