using System.Collections.Concurrent;

namespace LocalRag.Api.Services;

public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<Guid, DocumentChunk> _chunks = new();

    public void Add(string source, IReadOnlyList<string> texts, IReadOnlyList<float[]> embeddings)
    {
        if (texts.Count != embeddings.Count)
        {
            throw new InvalidOperationException("The number of embeddings did not match the document chunks.");
        }

        for (var index = 0; index < texts.Count; index++)
        {
            var chunk = new DocumentChunk(source, texts[index], embeddings[index]);
            _chunks[Guid.NewGuid()] = chunk;
        }
    }

    public IReadOnlyList<SearchMatch> Search(float[] queryEmbedding, int topK)
    {
        return _chunks.Values
            .Select(chunk => new SearchMatch(
                chunk.Source,
                chunk.Text,
                CosineSimilarity(queryEmbedding, chunk.Embedding)))
            .OrderByDescending(match => match.Score)
            .Take(topK)
            .ToArray();
    }

    private static float CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count != right.Count)
        {
            throw new InvalidOperationException(
                "Embedding dimensions differ. Use the same embedding model for all indexed documents and queries.");
        }

        double dotProduct = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < left.Count; index++)
        {
            dotProduct += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return (float)(dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude)));
    }

    private sealed record DocumentChunk(string Source, string Text, float[] Embedding);
}

public sealed record SearchMatch(string Source, string Text, float Score);
