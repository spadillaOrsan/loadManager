using System.Globalization;
using System.Text;

namespace LoadManager.Helpers;

/// <summary>
/// Builder fluido de documentos ESC/POS para impresoras termicas.
/// El texto se normaliza a ASCII (se eliminan diacriticos) para no depender del
/// codepage configurado en cada impresora.
/// </summary>
public sealed class EscPosDocumentBuilder(int columns = 32)
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    private readonly List<byte> buffer = [];

    public int Columns { get; } = Math.Max(16, columns);

    /// <summary>ESC @ — reinicia la impresora a su estado inicial.</summary>
    public EscPosDocumentBuilder Initialize()
    {
        buffer.AddRange([Esc, (byte)'@']);
        return this;
    }

    /// <summary>ESC a n — 0 izquierda, 1 centro, 2 derecha.</summary>
    public EscPosDocumentBuilder AlignLeft() => Align(0);

    public EscPosDocumentBuilder AlignCenter() => Align(1);

    public EscPosDocumentBuilder AlignRight() => Align(2);

    private EscPosDocumentBuilder Align(byte mode)
    {
        buffer.AddRange([Esc, (byte)'a', mode]);
        return this;
    }

    /// <summary>ESC E n — negrita.</summary>
    public EscPosDocumentBuilder Bold(bool enabled)
    {
        buffer.AddRange([Esc, (byte)'E', enabled ? (byte)1 : (byte)0]);
        return this;
    }

    /// <summary>GS ! n — doble ancho/alto (para folios o encabezados).</summary>
    public EscPosDocumentBuilder DoubleSize(bool enabled)
    {
        buffer.AddRange([Gs, (byte)'!', enabled ? (byte)0x11 : (byte)0x00]);
        return this;
    }

    public EscPosDocumentBuilder TextLine(string text)
    {
        buffer.AddRange(EncodeAscii(text));
        buffer.Add((byte)'\n');
        return this;
    }

    /// <summary>Linea con texto a la izquierda y a la derecha, rellenada a Columns.</summary>
    public EscPosDocumentBuilder TwoColumns(string left, string right)
    {
        left = NormalizeToAscii(left);
        right = NormalizeToAscii(right);

        var padding = Columns - left.Length - right.Length;
        if (padding < 1)
        {
            // Si no caben, se recorta la izquierda dejando al menos un espacio.
            var maxLeft = Math.Max(0, Columns - right.Length - 1);
            left = left.Length > maxLeft ? left[..maxLeft] : left;
            padding = Math.Max(1, Columns - left.Length - right.Length);
        }

        return TextLine(left + new string(' ', padding) + right);
    }

    public EscPosDocumentBuilder Separator(char character = '-') =>
        TextLine(new string(character, Columns));

    /// <summary>ESC d n — avanza n lineas de papel.</summary>
    public EscPosDocumentBuilder Feed(int lines = 1)
    {
        buffer.AddRange([Esc, (byte)'d', (byte)Math.Clamp(lines, 0, 255)]);
        return this;
    }

    /// <summary>
    /// GS ( k — imprime un codigo QR (modelo 2). moduleSize 1-16 puntos.
    /// </summary>
    public EscPosDocumentBuilder QrCode(string data, int moduleSize = 6)
    {
        var payload = EncodeAscii(data);

        // Modelo 2.
        buffer.AddRange([Gs, (byte)'(', (byte)'k', 4, 0, 49, 65, 50, 0]);
        // Tamano de modulo.
        buffer.AddRange([Gs, (byte)'(', (byte)'k', 3, 0, 49, 67, (byte)Math.Clamp(moduleSize, 1, 16)]);
        // Correccion de errores nivel M.
        buffer.AddRange([Gs, (byte)'(', (byte)'k', 3, 0, 49, 69, 49]);
        // Almacena los datos.
        var length = payload.Length + 3;
        buffer.AddRange([Gs, (byte)'(', (byte)'k', (byte)(length & 0xFF), (byte)(length >> 8), 49, 80, 48]);
        buffer.AddRange(payload);
        // Imprime.
        buffer.AddRange([Gs, (byte)'(', (byte)'k', 3, 0, 49, 81, 48]);
        return this;
    }

    /// <summary>GS k 73 — imprime un codigo de barras CODE128 (set B) con texto abajo.</summary>
    public EscPosDocumentBuilder Barcode(string data, int height = 80)
    {
        var payload = EncodeAscii(data);

        // HRI abajo, altura y ancho de modulo.
        buffer.AddRange([Gs, (byte)'H', 2]);
        buffer.AddRange([Gs, (byte)'h', (byte)Math.Clamp(height, 1, 255)]);
        buffer.AddRange([Gs, (byte)'w', 2]);

        buffer.AddRange([Gs, (byte)'k', 73, (byte)(payload.Length + 2), (byte)'{', (byte)'B']);
        buffer.AddRange(payload);
        return this;
    }

    /// <summary>GS V 66 — avanza y hace corte (parcial) de papel.</summary>
    public EscPosDocumentBuilder Cut(int feedBefore = 3)
    {
        Feed(feedBefore);
        buffer.AddRange([Gs, (byte)'V', 66, 0]);
        return this;
    }

    public byte[] Build() => [.. buffer];

    private static byte[] EncodeAscii(string text) =>
        Encoding.ASCII.GetBytes(NormalizeToAscii(text));

    /// <summary>Quita diacriticos (á→a, ñ→n) y reemplaza lo no representable por '?'.</summary>
    internal static string NormalizeToAscii(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            result.Append(character <= 0x7F ? character : '?');
        }

        return result.ToString();
    }
}
