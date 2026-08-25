using Axolotl2D.Assets;
using Axolotl2D.GameObjects;
using Axolotl2D.Input;
using Axolotl2D.Particles;
using Axolotl2D.Rendering;
using Axolotl2D.Scenes;
using Axolotl2D.Shaders;
using Axolotl2D.Timing;
using Silk.NET.Input;
using System.Numerics;

namespace Axolotl2D.Example.Bletris.Scenes;

public sealed class BletrisScene : BaseScene
{
    public override void Load()
    {
        Game.ClearColor = Color.FromHTML("#111827");
        Instantiate("Bletris board").AddComponent<BletrisController>();
    }

    internal void RequestPause() => SceneGameHost.PushScene<PauseMenuScene>();
}

public sealed class BletrisController(
    GameObject gameObject,
    Game game,
    AssetManager assets,
    SpriteBatch spriteBatch,
    TextRenderer textRenderer,
    InputActionMap input,
    ShaderLibrary shaders,
    Camera2D camera,
    CoroutineService coroutines,
    BletrisGame bletris) : Component(gameObject)
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
    private InputAction pause = null!;
    private InputAction carry = null!;
    private int kind;
    private int rotation;
    private int pieceX;
    private int pieceY;
    private int score;
    private int lines;
    private int carriedKind = -1;
    private int horizontalDirection;
    private double horizontalRepeat;
    private double dropTimer;
    private bool gameOver;
    private bool ready;
    private bool canCarry;
    private string? countdownText;
    private CoroutineHandle? countdown;

    public override void Start()
    {
        block = new Sprite(assets.Get<Texture2D>("block"));
        font = assets.Get<FontAsset>("ui-font");
        colorShader = shaders.Create(VertexShader, ColorFragmentShader);
        shaders.CreatePostProcess(camera, VignetteFragmentShader);
        camera.Position = Vector2.Zero;
        camera.Zoom = 1f;
        left = input.Bind("Move left", InputBinding.Button(
            InputControl.From(Key.Left), InputControl.From(Key.A), InputControl.From(ButtonName.DPadLeft)));
        right = input.Bind("Move right", InputBinding.Button(
            InputControl.From(Key.Right), InputControl.From(Key.D), InputControl.From(ButtonName.DPadRight)));
        down = input.Bind("Soft drop", InputBinding.Button(
            InputControl.From(Key.Down), InputControl.From(Key.S), InputControl.From(ButtonName.DPadDown)));
        rotate = input.Bind("Rotate", InputBinding.Button(
            InputControl.From(Key.Up), InputControl.From(Key.W), InputControl.From(Key.X),
            InputControl.From(ButtonName.A)));
        hardDrop = input.Bind("Hard drop", InputBinding.Button(
            InputControl.From(Key.Space), InputControl.From(ButtonName.Y)));
        restart = input.Bind("Restart", InputBinding.Button(
            InputControl.From(Key.R), InputControl.From(ButtonName.X)));
        pause = input.Bind("Pause", InputBinding.Button(
            InputControl.From(Key.P), InputControl.From(Key.Escape),
            InputControl.From(ButtonName.Start), InputControl.From(ButtonName.Back)));
        carry = input.Bind("Carry", InputBinding.Button(
            InputControl.From(Key.C), InputControl.From(ButtonName.LeftBumper)));
        Reset();
    }

    public override void Update(double deltaTime)
    {
        if (pause.WasPressedThisFrame)
        {
            ((BletrisScene)GameObject.Scene).RequestPause();
            return;
        }
        if (restart.WasPressedThisFrame)
            Reset();
        if (gameOver || !ready)
            return;

        if (carry.WasPressedThisFrame && canCarry)
        {
            CarryPiece();
            return;
        }

        if (rotate.WasPressedThisFrame)
            if (TryRotate())
                bletris.PlaySpatialSound(BletrisSound.Rotate, PieceWorldPosition());
        UpdateHorizontal(deltaTime);

        if (hardDrop.WasPressedThisFrame)
        {
            while (TryMove(0, 1)) { }
            camera.Shake(7f, 0.18f, 32f);
            bletris.PlaySpatialSound(BletrisSound.Drop, PieceWorldPosition());
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
        var scale = bletris.ScreenScale;
        camera.Zoom = scale;
        var (topLeft, cellSize) = GetBoardLayout();
        var boardSize = new Vector2(BletrisBoard.Width * cellSize, BletrisBoard.Height * cellSize);
        var sidebar = game.Viewport / 2f + new Vector2(topLeft.X + boardSize.X + 32f, topLeft.Y) * scale;
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
            {
                Span<(int X, int Y)> piece = stackalloc (int X, int Y)[4];
                board.PieceCells(kind, rotation, pieceX, pieceY, piece);
                foreach (var (column, row) in piece)
                    if (row >= 0)
                        DrawCell(column, row, PieceColors[kind]);
            }

            DrawPreview(bag.Next, 310f);
            if (carriedKind >= 0)
                DrawPreview(carriedKind, 410f);
        }

        textRenderer.Draw(spriteBatch, font, "BLETRIS", 30f * scale, sidebar, Color.White);
        textRenderer.Draw(spriteBatch, font,
            $"SCORE\n{score:000000}\n\nBEST\n{bletris.HighScore:000000}\n\nLINES\n{lines}\n\nLEVEL\n{lines / 10 + 1}",
            18f * scale, sidebar + new Vector2(0, 48f) * scale, Color.White);
        textRenderer.Draw(spriteBatch, font, "NEXT", 18f * scale,
            sidebar + new Vector2(0, 270f) * scale, MutedColor);
        textRenderer.Draw(spriteBatch, font, "CARRY", 18f * scale,
            sidebar + new Vector2(0, 370f) * scale, MutedColor);
        if (gameOver)
            textRenderer.Draw(spriteBatch, font, "GAME OVER\nR TO RESTART", 18f * scale,
                sidebar + new Vector2(0, 475f) * scale, GameOverColor);
        if (countdownText is not null)
            textRenderer.Draw(spriteBatch, font, countdownText, 54f * scale,
                game.Viewport / 2f - new Vector2(24f, 36f) * scale, Color.White,
                CoordinateSpace.Screen, depth: 200f);
        textRenderer.Draw(spriteBatch, font,
            "MOVE      ARROWS  A/D\nROTATE    UP  W/X  PAD A\nSOFT DROP DOWN  S\nHARD DROP SPACE  PAD Y\nCARRY     C  PAD LB\nPAUSE     P/ESC  START\nRESTART   R  PAD X",
            12f * scale, sidebar + new Vector2(0, 535f) * scale, MutedColor);

        void DrawCell(int column, int row, Color color) =>
            spriteBatch.Draw(block,
                topLeft + new Vector2((column + 0.5f) * cellSize, (row + 0.5f) * cellSize),
                drawSize, tint: color, space: CoordinateSpace.World);

        void DrawPreview(int previewKind, float y)
        {
            Span<(int X, int Y)> cells = stackalloc (int X, int Y)[4];
            board.PieceCells(previewKind, 0, 0, 0, cells);
            var minX = cells[0].X;
            var maxX = cells[0].X;
            var minY = cells[0].Y;
            foreach (var cell in cells[1..])
            {
                minX = Math.Min(minX, cell.X);
                maxX = Math.Max(maxX, cell.X);
                minY = Math.Min(minY, cell.Y);
            }
            var previewCellSize = 26f * scale;
            var previewLeft = sidebar.X + (180f * scale - (maxX - minX + 1) * previewCellSize) / 2f;
            foreach (var (column, row) in cells)
                spriteBatch.Draw(block,
                    new Vector2(
                        previewLeft + (column - minX + 0.5f) * previewCellSize,
                        sidebar.Y + y * scale + (row - minY + 0.5f) * previewCellSize),
                    new Vector2(24f * scale), tint: PieceColors[previewKind],
                    space: CoordinateSpace.Screen);
        }
    }

    private void UpdateHorizontal(double deltaTime)
    {
        var direction = (right.IsPressed ? 1 : 0) - (left.IsPressed ? 1 : 0);
        if (direction != horizontalDirection)
        {
            horizontalDirection = direction;
            horizontalRepeat = 0.16;
            if (direction != 0)
                TryMove(direction, 0, audible: true);
        }
        else if (direction != 0 && (horizontalRepeat -= deltaTime) <= 0)
        {
            TryMove(direction, 0, audible: true);
            horizontalRepeat += 0.06;
        }
    }

    private bool TryMove(int x, int y, bool audible = false)
    {
        if (!board.Fits(kind, rotation, pieceX + x, pieceY + y))
            return false;
        pieceX += x;
        pieceY += y;
        if (audible)
            bletris.PlaySpatialSound(BletrisSound.Move, PieceWorldPosition());
        return true;
    }

    private bool TryRotate()
    {
        var nextRotation = (rotation + 1) % 4;
        foreach (var kick in RotationKicks)
            if (board.Fits(kind, nextRotation, pieceX + kick, pieceY))
            {
                rotation = nextRotation;
                pieceX += kick;
                return true;
            }
        return false;
    }

    private void LockPiece()
    {
        if (!board.Place(kind, rotation, pieceX, pieceY))
        {
            gameOver = true;
            camera.Shake(15f, 0.45f, 22f);
            bletris.PlaySpatialSound(BletrisSound.GameOver, Vector2.Zero);
            UpdateTitle();
            return;
        }

        var cleared = board.ClearFullRows(clearedRows);
        foreach (var row in clearedRows)
            SpawnRowClearParticles(row);
        if (cleared > 0)
        {
            camera.Shake(10f + cleared * 2f, 0.28f, 30f);
            bletris.PlaySpatialSound(BletrisSound.Clear, Vector2.Zero, 0.9f + cleared * 0.08f);
        }
        score += LineScores[cleared] * (lines / 10 + 1);
        bletris.RecordScore(score);
        lines += cleared;
        canCarry = true;
        SpawnPiece();
        UpdateTitle();
    }

    private void SpawnPiece()
    {
        BeginPiece(bag.Take());
    }

    private void BeginPiece(int nextKind)
    {
        kind = nextKind;
        rotation = 0;
        pieceX = BletrisBoard.Width / 2;
        pieceY = 0;
        dropTimer = 0;
        gameOver = !board.Fits(kind, rotation, pieceX, pieceY);
    }

    private void CarryPiece()
    {
        var previous = carriedKind;
        carriedKind = kind;
        canCarry = false;
        if (previous < 0)
            SpawnPiece();
        else
            BeginPiece(previous);
        bletris.PlaySpatialSound(BletrisSound.Rotate, PieceWorldPosition(), 0.8f);
        UpdateTitle();
    }

    private void Reset()
    {
        board.Reset();
        score = 0;
        lines = 0;
        horizontalDirection = 0;
        horizontalRepeat = 0;
        gameOver = false;
        ready = false;
        carriedKind = -1;
        canCarry = true;
        bag.Reset();
        SpawnPiece();
        countdown?.Cancel();
        countdown = coroutines.Start(Countdown());
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
        emitter.Space = CoordinateSpace.World;
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
        const float cellSize = 32f;
        var boardSize = new Vector2(BletrisBoard.Width * cellSize, BletrisBoard.Height * cellSize);
        var topLeft = new Vector2(-boardSize.X / 2f - SidebarWidth / 2f, -boardSize.Y / 2f);
        return (topLeft, cellSize);
    }

    private Vector2 PieceWorldPosition()
    {
        var (topLeft, cellSize) = GetBoardLayout();
        return topLeft + new Vector2((pieceX + 0.5f) * cellSize, (pieceY + 0.5f) * cellSize);
    }

    private IEnumerable<CoroutineYield?> Countdown()
    {
        foreach (var value in new[] { "3", "2", "1", "GO" })
        {
            countdownText = value;
            yield return new WaitForSeconds(0.42);
        }
        countdownText = null;
        ready = true;
    }

    public override void OnDestroy()
    {
        countdown?.Cancel();
        bletris.ResumeAudio();
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

    private const string VignetteFragmentShader = """
        #version 330 core
        uniform sampler2D uTexture;
        in vec2 frag_texCoords;
        out vec4 out_color;
        void main() {
            vec4 color = texture(uTexture, frag_texCoords);
            float edge = smoothstep(0.28, 0.72, length(frag_texCoords - 0.5));
            float scan = 0.975 + 0.025 * sin(frag_texCoords.y * 900.0);
            out_color = vec4(color.rgb * (1.0 - edge * 0.42) * scan, color.a);
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
