namespace Simple_QR_Code_Maker.Core.Tests.Infrastructure;

internal sealed class TemporaryDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"simple-qr-tests-{Guid.NewGuid():N}");

    internal TemporaryDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
