namespace Axolotl2D.Example.Bletris;

internal sealed class BletrisBoard
{
    public const int Width = 10;
    public const int Height = 20;

    private static readonly (int X, int Y)[][] Shapes =
    [
        [(-1, 0), (0, 0), (1, 0), (2, 0)],
        [(0, 0), (1, 0), (0, 1), (1, 1)],
        [(-1, 0), (0, 0), (1, 0), (0, 1)],
        [(0, 0), (1, 0), (-1, 1), (0, 1)],
        [(-1, 0), (0, 0), (0, 1), (1, 1)],
        [(-1, 0), (-1, 1), (0, 1), (1, 1)],
        [(1, 0), (-1, 1), (0, 1), (1, 1)]
    ];

    private readonly int[,] cells = new int[Width, Height];

    public int this[int x, int y] => cells[x, y];

    public void Reset() => Array.Clear(cells);

    public void PieceCells(int kind, int rotation, int x, int y, Span<(int X, int Y)> destination)
    {
        if (destination.Length < 4)
            throw new ArgumentException("A tetromino needs four destination cells.", nameof(destination));
        var turns = kind == 1 ? 0 : (rotation % 4 + 4) % 4;
        var shape = Shapes[kind];
        for (var index = 0; index < shape.Length; index++)
        {
            var (shapeX, shapeY) = shape[index];
            var cellX = shapeX;
            var cellY = shapeY;
            for (var turn = 0; turn < turns; turn++)
                (cellX, cellY) = (-cellY, cellX);
            destination[index] = (x + cellX, y + cellY);
        }
    }

    public bool Fits(int kind, int rotation, int x, int y)
    {
        Span<(int X, int Y)> piece = stackalloc (int X, int Y)[4];
        PieceCells(kind, rotation, x, y, piece);
        foreach (var (cellX, cellY) in piece)
        {
            if (cellX < 0 || cellX >= Width || cellY >= Height || cellY >= 0 && cells[cellX, cellY] != 0)
                return false;
        }
        return true;
    }

    public bool Place(int kind, int rotation, int x, int y)
    {
        var entirelyVisible = true;
        Span<(int X, int Y)> piece = stackalloc (int X, int Y)[4];
        PieceCells(kind, rotation, x, y, piece);
        foreach (var (cellX, cellY) in piece)
        {
            if (cellY < 0)
                entirelyVisible = false;
            else
                cells[cellX, cellY] = kind + 1;
        }
        return entirelyVisible;
    }

    public int ClearFullRows(ICollection<int>? clearedRows = null)
    {
        clearedRows?.Clear();
        var destination = Height - 1;
        for (var source = Height - 1; source >= 0; source--)
        {
            var full = true;
            for (var column = 0; column < Width; column++)
                full &= cells[column, source] != 0;
            if (full)
            {
                clearedRows?.Add(source);
                continue;
            }

            for (var column = 0; column < Width; column++)
                cells[column, destination] = cells[column, source];
            destination--;
        }

        var cleared = destination + 1;
        for (; destination >= 0; destination--)
            for (var column = 0; column < Width; column++)
                cells[column, destination] = 0;
        return cleared;
    }

    public static void RunSelfCheck()
    {
        var board = new BletrisBoard();
        if (board.Fits(0, 0, 0, 0) || !board.Fits(0, 0, 1, 0))
            throw new InvalidOperationException("Piece bounds check failed.");

        for (var column = 0; column < Width; column++)
            board.cells[column, Height - 1] = 1;
        board.cells[0, Height - 2] = 2;
        if (board.ClearFullRows() != 1 || board.cells[0, Height - 1] != 2)
            throw new InvalidOperationException("Line clearing check failed.");

        var bag = new BletrisBag(new Random(7));
        bag.Reset();
        var previous = -1;
        for (var group = 0; group < 4; group++)
        {
            var pieces = new HashSet<int>();
            for (var index = 0; index < 7; index++)
            {
                var next = bag.Next;
                var piece = bag.Take();
                if (next != piece || piece == previous)
                    throw new InvalidOperationException("Piece queue repeated unexpectedly.");
                pieces.Add(piece);
                previous = piece;
            }
            if (pieces.Count != 7)
                throw new InvalidOperationException("Piece queue is not a seven-piece bag.");
        }
    }
}

internal sealed class BletrisBag(Random? random = null)
{
    private readonly Queue<int> pieces = new();
    private readonly Random random = random ?? Random.Shared;

    public int Next
    {
        get
        {
            EnsurePieces();
            return pieces.Peek();
        }
    }

    public int Take()
    {
        EnsurePieces();
        var piece = pieces.Dequeue();
        if (pieces.Count < 2)
            AddBag();
        return piece;
    }

    public void Reset()
    {
        pieces.Clear();
        AddBag();
    }

    private void EnsurePieces()
    {
        if (pieces.Count == 0)
            AddBag();
    }

    private void AddBag()
    {
        int[] bag = [0, 1, 2, 3, 4, 5, 6];
        random.Shuffle(bag);
        if (pieces.Count > 0 && bag[0] == pieces.Last())
            (bag[0], bag[1]) = (bag[1], bag[0]);
        foreach (var piece in bag)
            pieces.Enqueue(piece);
    }
}
