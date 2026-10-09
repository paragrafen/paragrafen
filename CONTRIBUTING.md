# Contributing

## Workflow

1. Branch from `main`
2. Keep it small. One thing per pull request.
3. Open a pull request and fill in the template.
4. Merge (squash) when CI is green and a teammate has approved.

Don't push to `main` directly.

## Commits and PR titles

Use [Conventional Commits](https://www.conventionalcommits.org/): `type: short description`, in English, lowercase, present tense.

```text
feat: add keyword search
fix: skip empty sections in ingest
docs: explain chunk_id format
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`, `ci`. Add `!` for breaking changes (`feat!: ...`).

Since we squash, the PR title becomes the commit message, so it needs to follow this too.

## Before you push

Run what CI runs:

```bash
dotnet format && dotnet test
cd ingest && uv run ruff check --fix && uv run ruff format && uv run pytest
cd web && npm run build
```

## Rules that matter

- Schema, `chunk_id` and `contracts/openapi.json` change only through a PR that updates both sides (Python and C#).
- Changes to search, prompts or the model include eval numbers from before and after.
- No secrets, personal data or customer data in the repo.
- Code, comments and docs are in English. UI text is in Norwegian.
