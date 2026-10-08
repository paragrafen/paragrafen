# Paragrafen

Ask a question about Norwegian law, get a short answer with links to the sections it is based on.

```text
db/migrations/   SQL schema
ingest/          Python: Lovdata -> database
eval/            Python: measures the API
src/             C# API (Core, Data, Api)
tests/           C# tests
web/             Next.js frontend
```

## Run locally

```bash
docker compose up -d
dotnet run --project src/Paragrafen.Api
cd web && npm run dev
```
