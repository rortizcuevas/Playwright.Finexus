using System.Text;

namespace Playwright.Finexus.Helpers;

public static class PdfGenerator
{
    /// <summary>Genera un PDF estructuralmente valido de aproximadamente targetBytes bytes.</summary>
    public static byte[] CreatePdf(long targetBytes)
    {
        var header = "%PDF-1.4\n"
            + "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"
            + "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n"
            + "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>\nendobj\n";
        const string streamEnd = "\nendstream\nendobj\n";
        const string trailer = "trailer\n<< /Root 1 0 R /Size 5 >>\n%%EOF\n";

        var overhead = header.Length + "4 0 obj\n<< /Length 00000000 >>\nstream\n".Length + streamEnd.Length + trailer.Length;
        var padLength = (int)Math.Max(0, targetBytes - overhead);

        using var ms = new MemoryStream((int)targetBytes + 256);
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write(header);
        Write($"4 0 obj\n<< /Length {padLength:D8} >>\nstream\n");
        var padding = new byte[padLength];
        Array.Fill(padding, (byte)' ');
        ms.Write(padding);
        Write(streamEnd);
        Write(trailer);
        return ms.ToArray();
    }
}
