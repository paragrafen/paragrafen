# Infrastructure

Everything runs in Azure, in the resource group `rg-paragrafen-dev` (subscription "LZ Development Paragrafen").

- `main.bicep`: the base. Database, model access (Foundry), container registry, logging and permissions.
- `apps.bicep`: the three apps: `web` (public), `api` (internal only) and `ingest` (runs every night).

Only JWT signing key is used. The apps and GitHub log in with their own Azure identities, and so do you.

## Getting access

1. Log in: `az login --tenant 774897da-0c2c-4c71-9897-873c4d659aee`
2. Add your name and object id to `main.bicepparam`, and ask someone with Owner access to deploy it.
3. Open the database firewall for your own IP. This is not kept in the repo:

```bash
az postgres flexible-server firewall-rule create -g rg-paragrafen-dev -s psql-paragrafen-noe -n <your first name> --start-ip-address $(curl -s https://api.ipify.org)
```

After that you can use the models from your own machine and connect to the database:

```bash
PGPASSWORD=$(az account get-access-token --resource-type oss-rdbms --query accessToken -o tsv) psql "host=psql-paragrafen-noe.postgres.database.azure.com dbname=paragrafen user=<your first name> sslmode=require"
```

## Deploying

```bash
az deployment group create -g rg-paragrafen-dev -p infra/main.bicepparam
```

For the apps, pass the image tags you want to run:

```bash
az deployment group create -g rg-paragrafen-dev -f infra/apps.bicep -p webImage=<image> apiImage=<image> ingestImage=<image> jwtSigningKey=<key>
```

## Good to know

- The database is in Norway East because Sweden Central was out of capacity.
- Claude models can't be used in this subscription, so we have gpt-6-luna and Mistral Small.
- `az containerapp` commands don't work yet because Azure still says the provider is registering. Use the portal for now.
