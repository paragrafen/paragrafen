# Paragrafen.Api

The HTTP layer: endpoints, error handling and app configuration. Requests from the web come in here and are passed on to the services in Core.

## Before you run it

- .NET 10 SDK
- Azure CLI, logged in: `az login --tenant 774897da-0c2c-4c71-9897-873c4d659aee`
- Your name in `infra/main.bicepparam`, deployed by an Owner. See [infra/README.md](../../infra/README.md).

No keys or secrets are needed. The API uses your Azure login to call the model.

## Run

```bash
dotnet run --project src/Paragrafen.Api
```

Open http://localhost:5073/swagger and try `POST /ask` with `{ "question": "Hvor gammel må man være for å stemme?" }`.

To use it from the web app instead of the mock, add `API_URL=http://localhost:5073` to `web/.env.local`.

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `Foundry:Endpoint` | `appsettings.json` | Not a secret. In Azure it is set as `Foundry__Endpoint` by `infra/apps.bicep` |

## Errors

Errors are returned as Problem Details with a `code` the frontend can use:

| Status | `code` | When |
|---|---|---|
| 400 | `empty_question`, `question_too_long`, `invalid_request` | Bad input |
| 502 | `model_unavailable`, `model_no_answer` | The model could not be reached or gave no answer |
| 503 | `model_busy` | The model's rate limit was hit |

## Troubleshooting

- **502 with an authentication error in the log:** your `az login` has expired. Log in again and restart the API.
- **502 with 401 or 403 in the log:** you don't have access to Foundry yet. See [infra/README.md](../../infra/README.md).
