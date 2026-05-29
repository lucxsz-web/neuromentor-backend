using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace NeuroMentor.Api.Services;

public class TextExtractionService
{
    public string Extract(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var raw = ext switch
        {
            ".pdf" => ExtractPdf(stream),
            _ => new StreamReader(stream).ReadToEnd()
        };
        return Clean(raw);
    }

    private static string ExtractPdf(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms.ToArray());
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    /// <summary>
    /// Cleans raw extracted text: removes noise, page numbers, repeated headers/footers,
    /// normalizes whitespace. Reduces token usage by ~30% on average.
    /// </summary>
    public static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var lines = raw.Split('\n');

        // Count line frequency to detect repeated headers/footers
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var key = line.Trim();
            if (key.Length > 3 && key.Length < 100)
                freq[key] = freq.GetValueOrDefault(key) + 1;
        }

        int pageCount = Math.Max(1, lines.Length / 30); // rough estimate
        int repeatThreshold = Math.Max(3, pageCount / 3);

        var cleaned = new List<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();

            // Skip empty / whitespace-only
            if (string.IsNullOrWhiteSpace(t)) continue;

            // Skip lone page numbers: "1", "2", "Page 3", "Página 3", "Pág. 3", "- 4 -", "3 of 50"
            if (Regex.IsMatch(t, @"^[-–—\s]*([Pp]age|[Pp][áa]gina|[Pp][áa]g\.?)\s*\d+[-–—\s\w]*$")) continue;
            if (Regex.IsMatch(t, @"^\d+\s*(of\s*\d+)?$")) continue;
            if (Regex.IsMatch(t, @"^[-–—]\s*\d+\s*[-–—]$")) continue;

            // Skip lines repeated too often (headers/footers)
            if (freq.GetValueOrDefault(t) >= repeatThreshold) continue;

            // Skip lines that are just special chars / dots / dashes
            if (Regex.IsMatch(t, @"^[.\-_=*#|/\\]{4,}$")) continue;

            // Skip URLs
            if (Regex.IsMatch(t, @"^https?://\S+$")) continue;

            cleaned.Add(t);
        }

        // Join, then collapse 3+ blank lines to 2
        var joined = string.Join("\n", cleaned);
        joined = Regex.Replace(joined, @"\n{3,}", "\n\n");

        // Collapse excessive spaces within lines
        joined = Regex.Replace(joined, @"[ \t]{3,}", "  ");

        return joined.Trim();
    }

    /// <summary>
    /// Extracts the most relevant chunk of text for a given module based on keyword matching.
    /// Returns at most maxChars characters.
    /// </summary>
    public static string ExtractChunk(string fullText, string moduleTitle, IEnumerable<string> concepts, int maxChars = 3000)
    {
        if (string.IsNullOrWhiteSpace(fullText)) return "";

        var keywords = concepts
            .Concat([moduleTitle])
            .SelectMany(k => k.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(k => k.Length > 3)
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToHashSet();

        // Split into paragraphs (~200 char blocks)
        var paragraphs = fullText
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 20)
            .ToList();

        // Score each paragraph by keyword hits
        var scored = paragraphs
            .Select(p =>
            {
                var lower = p.ToLowerInvariant();
                var score = keywords.Count(k => lower.Contains(k));
                return (paragraph: p, score);
            })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ToList();

        // Build chunk up to maxChars, keeping highest-scoring paragraphs
        var sb = new StringBuilder();
        foreach (var (paragraph, _) in scored)
        {
            if (sb.Length + paragraph.Length > maxChars) break;
            sb.AppendLine(paragraph);
            sb.AppendLine();
        }

        // If nothing matched, fall back to beginning of text
        if (sb.Length == 0)
            return fullText[..Math.Min(maxChars, fullText.Length)];

        return sb.ToString().Trim();
    }
}
