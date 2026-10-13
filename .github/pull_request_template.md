## What and why

## How it was tested

## Checklist

- [ ] CI is green
- [ ] If any changes regard `db/migrations/`, `chunk_id` or `contracts/openapi.json`: both sides updated, ensure backend and frontend changes align
- [ ] New table in `db/migrations/`: grants added to `infra/db-access.sql`
- [ ] Changes to search, prompt or model: eval numbers before and after are included above
- [ ] No secrets, and no personal or customer data
