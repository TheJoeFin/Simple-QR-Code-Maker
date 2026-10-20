namespace Simple_QR_Code_Maker.Core.Models;

public sealed class QrMatrix
{
    private readonly bool[,] modules;

    public QrMatrix(bool[,] modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        if (modules.GetLength(0) != modules.GetLength(1))
            throw new ArgumentException("QR matrices must be square.", nameof(modules));

        this.modules = modules;
    }

    public int Size => modules.GetLength(0);

    public bool this[int row, int column] => modules[row, column];
}
