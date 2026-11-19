# Local RAG API with .NET and Ollama

A minimal ASP.NET Core API that ingests text, embeds overlapping chunks with Ollama, retrieves relevant chunks using cosine similarity, and answers questions with Gemma 3. Documents and vectors are kept in memory, so they are cleared when the API restarts.

## Requirements

- .NET 10 SDK
- [Ollama](https://ollama.com/) running locally

Pull the chat and embedding models:

```powershell
ollama pull gemma3:4b
ollama pull nomic-embed-text
```

If the chat API reports that a model is missing, pull the configured model or change `Ollama:ChatModel` in `appsettings.json` to an installed Ollama model. The default chat model is `gemma3:4b`.

Run the API from this directory:

```powershell
dotnet run
```

Open the base URL printed in the console to use the web app. The UI and API are served from the same origin. Ollama defaults to `http://localhost:11434`. Change `Ollama:BaseUrl`, `Ollama:ChatModel`, or `Ollama:EmbeddingModel` in `appsettings.json` to use a different local configuration.

The web app is served from `wwwroot`. It supports adding pasted text or local `.txt`, `.md`, `.csv`, and `.log` files, then asking questions and viewing source citations. The API stores documents and embeddings in memory, so add documents again after restarting the API.

In Development, the OpenAPI document is available at `http://localhost:5249/openapi/v1.json`. Use the requests in `LocalRag.Api.http` from VS Code's REST Client or the built-in HTTP client to inspect the OpenAPI document and test the endpoints.

## API

Add a text document:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5249/api/documents `
  -ContentType 'application/json' `
  -Body (@{
    source = 'getting-started.txt'
    content = 'Your document text goes here.'
  } | ConvertTo-Json)
```

Ask a question:

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://localhost:5249/api/chat `
  -ContentType 'application/json' `
  -Body (@{
    question = 'What does the document say?'
    topK = 3
  } | ConvertTo-Json)
```

`POST /api/documents` accepts `{ "source": "...", "content": "..." }`. `POST /api/chat` accepts `{ "question": "...", "topK": 3 }`, where `topK` is optional and must be between 1 and 10. Responses include the answer and the retrieved source names with similarity scores.

`GET /api/health` checks that the API process is running. It does not check Ollama connectivity; Ollama errors are returned when an ingestion or chat request needs the model.
