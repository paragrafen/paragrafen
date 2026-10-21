# Paragrafen.Api

The HTTP layer: endpoints, error handling and app configuration. Requests from the web come in here and are passed on to the services in Core.

## Before you run it

- .NET 10 SDK
- Azure CLI, logged in: `az login --tenant 774897da-0c2c-4c71-9897-873c4d659aee`
- Your name in `infra/main.bicepparam`, deployed by an Owner. See [infra/README.md](../../infra/README.md).
- Postgres running, with migrations applied: `docker compose up -d`
- A JWT signing key, once per machine (any 32+ characters; it only signs tokens locally):
  ```bash
  dotnet user-secrets set "Jwt:IssuerSigningKey" "<any 32+ characters>" --project src/Paragrafen.Api
  ```

Only the JWT Sing in key is needed, this should be retrieved from GitHub. The API uses your Azure login to call the model.


## Run

```bash
dotnet run --project src/Paragrafen.Api
```

Open <http://localhost:5073/swagger> and try `POST /ask` with `{ "question": "Hvor gammel må man være for å stemme?" }`.

To use it from the web app instead of the mock, add `API_URL=http://localhost:5073` to `web/.env.local`.

## Configuration

| Setting                        | Where                                                      | Notes                                                                                                                                                                                                                                                  |
| ------------------------------ | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Foundry:Endpoint`             | `appsettings.json`                                         | Not a secret. In Azure it is set as `Foundry__Endpoint` by `infra/apps.bicep`                                                                                                                                                                          |
| `ConnectionStrings:Paragrafen` | `launchSettings.json` locally, `infra/apps.bicep` in Azure | Locally with the Docker password. In Azure without a password: the API connects with its managed identity                                                                                                                                              |
| `Foundry:Prices:<deployment>`  | `appsettings.json`                                         | NOK per million tokens for the deployment in `Foundry:ChatClient`. List prices from the [Azure Retail Prices API](https://prices.azure.com/api/retail/prices) (`currencyCode=NOK`, `swedencentral`, `Std Gl` short-context meters), checked 19/10/2026 |
| `Jwt:IssuerSigningKey` | user-secrets locally, `JWT_SIGNING_KEY` GitHub secret in Azure | Signs login tokens, at least 32 characters. A new key logs everyone out |

# Call loghttps://prices.azure.com/api/retail/prices

Every `POST /ask` that reaches `AskService` writes one row to `calls`: status, failure reason, model, tokens, cost in NOK and latency. Writing the row never fails the request; if the insert fails it is logged as an error. Malformed JSON bodies are rejected before `AskService` and only appear in the application log.

## Errors

Errors are returned as Problem Details with a `code` the frontend can use:

| Status | `code`                                                   | When                                             |
| ------ | -------------------------------------------------------- | ------------------------------------------------ |
| 400    | `empty_question`, `question_too_long`, `invalid_request` | Bad input                                        |
| 502    | `model_unavailable`, `model_no_answer`                   | The model could not be reached or gave no answer |
| 503    | `model_busy`                                             | The model's rate limit was hit                   |

## Troubleshooting

- **502 with an authentication error in the log:** your `az login` has expired. Log in again and restart the API.
- **502 with 401 or 403 in the log:** you don't have access to Foundry yet. See [infra/README.md](../../infra/README.md).
- **`Jwt:IssuerSigningKey is missing` on startup:** run the `dotnet user-secrets set` command above.
