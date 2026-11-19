namespace LocalRag.Api.Services;

public sealed class TextChunker
{
    private const int ChunkSize = 1000;
    private const int Overlap = 150;

    public IReadOnlyList<string> Split(string text)
    {
        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkSize, text.Length);
            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end - 1, end - start);
                if (boundary > start + ChunkSize / 2)
                {
                    end = boundary;
                }
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end == text.Length)
            {
                break;
            }

            start = Math.Max(start + 1, end - Overlap);
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }

        return chunks;
    }
}
