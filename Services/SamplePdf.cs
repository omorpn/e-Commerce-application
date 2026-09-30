using System.Text;

namespace e_Commerce_application.Services
{
    // Builds a small single-page PDF so seeded sample ebooks have a real downloadable file.
    public static class SamplePdf
    {
        public static byte[] Create(string title, string author, IEnumerable<string> paragraphs)
        {
            var content = new StringBuilder();
            content.Append("BT /F1 26 Tf 72 700 Td (").Append(Escape(title)).Append(") Tj ET\n");
            content.Append("BT /F2 14 Tf 72 670 Td (by ").Append(Escape(author)).Append(") Tj ET\n");
            content.Append("BT /F2 12 Tf 16 TL 72 630 Td\n");
            foreach (var paragraph in paragraphs)
            {
                foreach (var line in Wrap(paragraph, 80))
                {
                    content.Append('(').Append(Escape(line)).Append(") Tj T*\n");
                }
                content.Append("T*\n");
            }
            content.Append("ET\n");

            var objects = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {content.Length} >>\nstream\n{content}endstream"
            };

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (var i = 0; i < objects.Length; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
            }

            var xref = pdf.Length;
            pdf.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
            pdf.Append("0000000000 65535 f \n");
            foreach (var offset in offsets)
            {
                pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
            }
            pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\n");
            pdf.Append("startxref\n").Append(xref).Append("\n%%EOF\n");

            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        private static string Escape(string text) =>
            text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        private static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new StringBuilder();
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + word.Length + 1 > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0)
                {
                    line.Append(' ');
                }
                line.Append(word);
            }
            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }
}
