#!/usr/bin/env bash
# Applies db/migrations/*.sql in order, each once. Connection comes from the standard PG* environment variables.
set -euo pipefail

psql -v ON_ERROR_STOP=1 -qc "SET client_min_messages = warning; CREATE TABLE IF NOT EXISTS schema_migrations (filename text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())"

for f in "$(dirname "$0")"/migrations/*.sql; do
  name=$(basename "$f")
  if [ -n "$(psql -tAc "SELECT 1 FROM schema_migrations WHERE filename = '$name'")" ]; then
    continue
  fi
  echo "Applying $name"
  psql -v ON_ERROR_STOP=1 --single-transaction -f "$f" -c "INSERT INTO schema_migrations (filename) VALUES ('$name')"
done
