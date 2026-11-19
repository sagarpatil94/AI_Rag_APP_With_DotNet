using LocalRag.Api.Middleware;
using LocalRag.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddHttpClient<OllamaClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/health", (CancellationToken cancellationToken) =>
    TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithSummary("Check API health")
    .WithDescription("Returns the health status of the API process.")
    .Produces<HealthResponse>(StatusCodes.Status200OK);

app.MapPost("/api/documents", async Task<Results<Ok<AddDocumentResponse>, BadRequest<ApiErrorResponse>>> (
    AddDocumentRequest request,
    TextChunker chunker,
    OllamaClient ollama,
    DocumentStore store,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Source))
    {
        return TypedResults.BadRequest(new ApiErrorResponse("Source is required."));
    }

    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return TypedResults.BadRequest(new ApiErrorResponse("Content is required."));
    }

    var chunks = chunker.Split(request.Content);
    var embeddings = await ollama.EmbedAsync(chunks, cancellationToken);
    store.Add(request.Source, chunks, embeddings);

    return TypedResults.Ok(new AddDocumentResponse(request.Source, chunks.Count));
})
    .WithName("AddDocument")
    .WithSummary("Add a document to the RAG index")
    .WithDescription("Splits text into chunks, creates local Ollama embeddings, and stores them in memory.")
    .Produces<AddDocumentResponse>(StatusCodes.Status200OK)
    .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapPost("/api/chat", async Task<Results<Ok<ChatResponse>, BadRequest<ApiErrorResponse>>> (
    ChatRequest request,
    OllamaClient ollama,
    DocumentStore store,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return TypedResults.BadRequest(new ApiErrorResponse("Question is required."));
    }

    if (request.TopK is < 1 or > 10)
    {
        return TypedResults.BadRequest(new ApiErrorResponse("TopK must be between 1 and 10."));
    }

    var queryEmbeddings = await ollama.EmbedAsync([request.Question], cancellationToken);
    var matches = store.Search(queryEmbeddings[0], request.TopK);

    if (matches.Count == 0)
    {
        return TypedResults.Ok(new ChatResponse(
            "No documents have been added yet. Add a document before asking a question.",
            []));
    }

    var context = string.Join(
        "\n\n",
        matches.Select((match, index) =>
            $"[{index + 1}] Source: {match.Source}\n{match.Text}"));

    var answer = await ollama.ChatAsync(request.Question, context, cancellationToken);
    var citations = matches
        .Select(match => new SourceCitation(match.Source, match.Score))
        .DistinctBy(citation => citation.Source)
        .ToArray();

    return TypedResults.Ok(new ChatResponse(answer, citations));
})
    .WithName("Chat")
    .WithSummary("Ask a question about indexed documents")
    .WithDescription("Retrieves the most relevant chunks and asks the configured Ollama chat model to answer with citations.")
    .Produces<ChatResponse>(StatusCodes.Status200OK)
    .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

/// <summary>Reports whether the API process is running.</summary>
internal sealed record HealthResponse(string Status);

/// <summary>Provides the source name and text to add to the RAG index.</summary>
internal sealed record AddDocumentRequest(string Source, string Content);

/// <summary>Reports the number of chunks added for a document.</summary>
internal sealed record AddDocumentResponse(string Source, int Chunks);

/// <summary>Provides a question and the maximum number of matching chunks to retrieve.</summary>
internal sealed record ChatRequest(string Question, int TopK = 3);

/// <summary>Contains an answer and its supporting document sources.</summary>
internal sealed record ChatResponse(string Answer, IReadOnlyList<SourceCitation> Sources);

/// <summary>Identifies a source document and its retrieval similarity score.</summary>
internal sealed record SourceCitation(string Source, float Score);

/// <summary>Describes a request validation error.</summary>
internal sealed record ApiErrorResponse(string Error);
