-- Database access for the apps' managed identities in Azure. Runs on every deploy, after migrations.
-- When a migration adds a table, add its grants here in the same pull request.
-- Only grant what each app needs: ingest must not read conversations or logs, which may hold personal data.

\set ON_ERROR_STOP 1
\set app_db :DBNAME

-- Entra roles are server-wide, but Azure only installs pgaadauth in the postgres database.
\connect postgres

SELECT pgaadauth_create_principal('id-paragrafen-api', false, false)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'id-paragrafen-api');

SELECT pgaadauth_create_principal('id-paragrafen-ingest', false, false)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'id-paragrafen-ingest');

\connect :app_db

GRANT USAGE ON SCHEMA public TO "id-paragrafen-api", "id-paragrafen-ingest";

GRANT SELECT, INSERT, UPDATE, DELETE
    ON public.documents, public.sections, public.chunks
    TO "id-paragrafen-ingest";
GRANT SELECT ON public.documents, public.sections, public.chunks
    TO "id-paragrafen-api";


-- Identity (004_identity.sql). Only the api: these tables hold users' emails and password hashes.
GRANT SELECT, INSERT, UPDATE, DELETE ON
  "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "AspNetUserClaims",
  "AspNetUserLogins", "AspNetUserTokens", "AspNetRoleClaims"
  TO "id-paragrafen-api";

-- The claims tables have generated ids; inserting a row needs the sequence behind them.
GRANT USAGE ON SEQUENCE "AspNetUserClaims_Id_seq", "AspNetRoleClaims_Id_seq" TO "id-paragrafen-api";
