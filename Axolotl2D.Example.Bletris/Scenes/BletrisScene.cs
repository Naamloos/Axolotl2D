using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Particles;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Shaders;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

public sealed class BletrisScene : BaseScene
{
    private bool returnToMenu;

    public override void Load()
    {
        Game.ClearColor = Color.FromHTML("#111827");
        Instantiate("Bletris board").AddComponent<BletrisController>();
    }

    public override void Update(double deltaTime)
    {
        if (returnToMenu)
            SceneGameHost.ChangeScene<MainMenuScene>();
    }

    internal void RequestReturnToMenu() => returnToMenu = true;
}

public sealed class BletrisController(
    GameObject gameObject,
    Game game,
    AssetManager assets,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input,
    ShaderLibrary shaders) : Component(gameObject)
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
    private static readonly Color MutedColor = Color.FromHTML("#94A3B8");
    private static readonly Color GameOverColor = Color.FromHTML("#ED5D68");
    private const double SoftDropInterval = 0.125;
    private const float SidebarWidth = 240f;

    private readonly BletrisBoard board = new();
    private readonly BletrisBag bag = new();
    private readonly List<int> clearedRows = new(4);
    private Sprite block = null!;
    private FontAsset font = null!;
    private ShaderProgram colorShader = null!;
    private InputAction left = null!;
    private InputAction right = null!;
    private InputAction down = null!;
    private InputAction rotate = null!;
    private InputAction hardDrop = null!;
    private InputAction restart = null!;
    private InputAction menu = null!;
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

    public override void Start()
    {
        block = new Sprite(assets.Get<Texture2D>("block"));
        font = assets.Get<FontAsset>("ui-font");
        colorShader = shaders.Create(VertexShader, ColorFragmentShader);
        left = input.BindButton("Move left", Key.Left, Key.A);
        right = input.BindButton("Move right", Key.Right, Key.D);
        down = input.BindButton("Soft drop", Key.Down, Key.S);
        rotate = input.BindButton("Rotate", Key.Up, Key.W, Key.X);
        hardDrop = input.BindButton("Hard drop", Key.Space);
        restart = input.BindButton("Restart", Key.R);
        menu = input.BindButton("Main menu", Key.Escape);
        Reset();
    }

    public override void Update(double deltaTime)
    {
        if (menu.WasPressedThisFrame)
        {
            ((BletrisScene)GameObject.Scene).RequestReturnToMenu();
            return;
        }
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

        if (down.WasPressedThisFrame)
            dropTimer = 0;
        var interval = down.IsPressed
            ? SoftDropInterval
            : Math.Max(0.08, 0.65 - lines / 10 * 0.05);
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

    public override void Render()
    {
        var (topLeft, cellSize) = GetBoardLayout();
        var boardSize = new Vector2(BletrisBoard.Width * cellSize, BletrisBoard.Height * cellSize);
        var sidebar = new Vector2(topLeft.X + boardSize.X + 32f, topLeft.Y);
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

            var nextKind = bag.Next;
            var previewCells = board.PieceCells(nextKind, 0, 0, 0).ToArray();
            var minX = previewCells.Min(cell => cell.X);
            var maxX = previewCells.Max(cell => cell.X);
            var minY = previewCells.Min(cell => cell.Y);
            var previewCellSize = Math.Min(26f, cellSize);
            var previewLeft = sidebar.X + (180f - (maxX - minX + 1) * previewCellSize) / 2f;
            foreach (var (column, row) in previewCells)
                spriteBatch.Draw(block,
                    new Vector2(
                        previewLeft + (column - minX + 0.5f) * previewCellSize,
                        sidebar.Y + 310f + (row - minY + 0.5f) * previewCellSize),
                    new Vector2(previewCellSize - 2f), tint: PieceColors[nextKind],
                    space: CoordinateSpace.Screen);
        }

        textRenderer.Draw(spriteBatch, font, "BLETRIS", 30f, sidebar, Color.White);
        textRenderer.Draw(spriteBatch, font,
            $"SCORE\n{score:000000}\n\nLINES\n{lines}\n\nLEVEL\n{lines / 10 + 1}",
            18f, sidebar + new Vector2(0, 48f), Color.White);
        textRenderer.Draw(spriteBatch, font, "NEXT", 18f,
            sidebar + new Vector2(0, 270f), MutedColor);
        if (gameOver)
            textRenderer.Draw(spriteBatch, font, "GAME OVER\nR TO RESTART", 18f,
                sidebar + new Vector2(0, 375f), GameOverColor);
        textRenderer.Draw(spriteBatch, font,
            "MOVE      LEFT/RIGHT  A/D\nROTATE    UP  W/X\nSOFT DROP DOWN  S\nHARD DROP SPACE\nRESTART   R\nMENU      ESC",
            12f, sidebar + new Vector2(0, 455f), MutedColor);

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

        var cleared = board.ClearFullRows(clearedRows);
        foreach (var row in clearedRows)
            SpawnRowClearParticles(row);
        score += LineScores[cleared] * (lines / 10 + 1);
        lines += cleared;
        SpawnPiece();
        UpdateTitle();
    }

    private void SpawnPiece()
    {
        kind = bag.Take();
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
        bag.Reset();
        SpawnPiece();
        UpdateTitle();
    }

    private void SpawnRowClearParticles(int row)
    {
        var (topLeft, cellSize) = GetBoardLayout();
        var effect = GameObject.Scene.Instantiate("Row clear particles");
        effect.Transform.LocalPosition = topLeft + new Vector2(
            BletrisBoard.Width * cellSize / 2f,
            (row + 0.5f) * cellSize);

        var emitter = effect.AddComponent<ParticleEmitter>();
        emitter.Sprite = block;
        emitter.Space = CoordinateSpace.Screen;
        emitter.PlayOnStart = false;
        emitter.MaxParticles = BletrisBoard.Width * 5;
        emitter.Lifetime = 0.65f;
        emitter.LifetimeVariation = 0.15f;
        emitter.Speed = cellSize * 5f;
        emitter.SpeedVariation = cellSize * 2f;
        emitter.Direction = -MathF.PI / 2f;
        emitter.Spread = MathF.PI * 0.9f;
        emitter.Acceleration = new Vector2(0f, cellSize * 7f);
        emitter.StartSize = cellSize * 0.55f;
        emitter.EndSize = 1f;
        emitter.StartColor = Color.Cyan;
        emitter.EndColor = Color.Transparent;
        emitter.SetRandomSeed(score + lines * 31 + row);
        emitter.Emit(BletrisBoard.Width * 5);
        effect.AddComponent<DestroyWhenParticlesFinish>().Emitter = emitter;
    }

    private (Vector2 TopLeft, float CellSize) GetBoardLayout()
    {
        var availableBoardWidth = Math.Max(80f, game.Viewport.X - SidebarWidth - 40f);
        var cellSize = Math.Max(8f, Math.Min(32f,
            Math.Min(availableBoardWidth / BletrisBoard.Width,
                (game.Viewport.Y - 40f) / BletrisBoard.Height)));
        var boardSize = new Vector2(BletrisBoard.Width * cellSize, BletrisBoard.Height * cellSize);
        var topLeft = new Vector2(
            Math.Max(10f, (game.Viewport.X - boardSize.X - SidebarWidth) / 2f),
            (game.Viewport.Y - boardSize.Y) / 2f);
        return (topLeft, cellSize);
    }

    private void UpdateTitle() => game.Title = gameOver ? "Bletris | Game over" : "Bletris";

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

public sealed class DestroyWhenParticlesFinish(GameObject gameObject) : Component(gameObject)
{
    public ParticleEmitter Emitter { get; set; } = null!;

    public override void Update(double deltaTime)
    {
        if (Emitter.HasStarted && Emitter.AliveCount == 0)
            GameObject.Destroy();
    }
}
