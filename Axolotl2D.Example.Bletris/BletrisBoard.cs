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

    public IEnumerable<(int X, int Y)> PieceCells(int kind, int rotation, int x, int y)
    {
        var turns = kind == 1 ? 0 : (rotation % 4 + 4) % 4;
        foreach (var (shapeX, shapeY) in Shapes[kind])
        {
            var cellX = shapeX;
            var cellY = shapeY;
            for (var turn = 0; turn < turns; turn++)
                (cellX, cellY) = (-cellY, cellX);
            yield return (x + cellX, y + cellY);
        }
    }

    public bool Fits(int kind, int rotation, int x, int y)
    {
        foreach (var (cellX, cellY) in PieceCells(kind, rotation, x, y))
        {
            if (cellX < 0 || cellX >= Width || cellY >= Height || cellY >= 0 && cells[cellX, cellY] != 0)
                return false;
        }
        return true;
    }

    public bool Place(int kind, int rotation, int x, int y)
    {
        var entirelyVisible = true;
        foreach (var (cellX, cellY) in PieceCells(kind, rotation, x, y))
        {
            if (cellY < 0)
                entirelyVisible = false;
            else
                cells[cellX, cellY] = kind + 1;
        }
        return entirelyVisible;
    }

    public int ClearFullRows()
    {
        var cleared = 0;
        for (var row = Height - 1; row >= 0; row--)
        {
            var full = true;
            for (var column = 0; column < Width; column++)
                full &= cells[column, row] != 0;
            if (!full)
                continue;

            cleared++;
            for (var destination = row; destination > 0; destination--)
                for (var column = 0; column < Width; column++)
                    cells[column, destination] = cells[column, destination - 1];
            for (var column = 0; column < Width; column++)
                cells[column, 0] = 0;
            row++;
        }
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
    }
}
