using Simple_QR_Code_Maker.Core.Models;
using ZXing;
using ZXing.QrCode.Internal;

namespace Simple_QR_Code_Maker.Core.Services;

public sealed class QrMatrixEncoder
{
    private const string Utf8CharacterSet = "UTF-8";

    public QrMatrix Encode(string content, QrErrorCorrectionLevel errorCorrectionLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        ErrorCorrectionLevel correctionLevel = errorCorrectionLevel switch
        {
            QrErrorCorrectionLevel.Low => ErrorCorrectionLevel.L,
            QrErrorCorrectionLevel.Medium => ErrorCorrectionLevel.M,
            QrErrorCorrectionLevel.Quartile => ErrorCorrectionLevel.Q,
            QrErrorCorrectionLevel.High => ErrorCorrectionLevel.H,
            _ => throw new ArgumentOutOfRangeException(nameof(errorCorrectionLevel)),
        };

        QRCode code = NeedsUtf8Encoding(content)
            ? Encoder.encode(content, correctionLevel, new Dictionary<EncodeHintType, object>
            {
                [EncodeHintType.CHARACTER_SET] = Utf8CharacterSet,
            })
            : Encoder.encode(content, correctionLevel);

        ByteMatrix byteMatrix = code.Matrix ?? throw new InvalidOperationException("The QR encoder did not create a matrix.");
        int size = byteMatrix.Width;
        bool[,] modules = new bool[size, size];

        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
                modules[row, column] = byteMatrix[column, row] == 1;
        }

        return new QrMatrix(modules);
    }

    private static bool NeedsUtf8Encoding(string content)
    {
        foreach (char character in content)
        {
            if (character > 'ÿ')
                return true;
        }

        return false;
    }
}
