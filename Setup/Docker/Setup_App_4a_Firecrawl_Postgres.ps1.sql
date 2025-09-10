-- firecrawl-postgres.sql
-- Atomic + idempotent NuQ schema for Firecrawl (safe to re-run)

BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL idle_in_transaction_session_timeout = '60s';

-- 1) Schema
CREATE SCHEMA IF NOT EXISTS nuq;

-- 2) Enum type: ensure it exists and has required values
DO $do$
BEGIN
  IF NOT EXISTS (
    SELECT 1
    FROM pg_type t
    JOIN pg_namespace n ON n.oid = t.typnamespace
    WHERE n.nspname = 'nuq' AND t.typname = 'job_status'
  ) THEN
    EXECUTE 'CREATE TYPE nuq.job_status AS ENUM (''queued'',''active'',''completed'',''failed'')';
  ELSE
    EXECUTE 'ALTER TYPE nuq.job_status ADD VALUE IF NOT EXISTS ''queued''';
    EXECUTE 'ALTER TYPE nuq.job_status ADD VALUE IF NOT EXISTS ''active''';
    EXECUTE 'ALTER TYPE nuq.job_status ADD VALUE IF NOT EXISTS ''completed''';
    EXECUTE 'ALTER TYPE nuq.job_status ADD VALUE IF NOT EXISTS ''failed''';
  END IF;
END
$do$;

-- 3) Base table (create minimal shape, then extend via ALTERs)
CREATE TABLE IF NOT EXISTS nuq.queue_scrape (
  id   uuid  PRIMARY KEY,
  data jsonb NOT NULL
);

-- 4) Columns the current image uses (add-only, safe to re-run)
ALTER TABLE nuq.queue_scrape
  ADD COLUMN IF NOT EXISTS status       nuq.job_status NOT NULL DEFAULT 'queued',
  ADD COLUMN IF NOT EXISTS "lock"       text,
  ADD COLUMN IF NOT EXISTS created_at   timestamptz    NOT NULL DEFAULT now(),
  ADD COLUMN IF NOT EXISTS locked_at    timestamptz,
  ADD COLUMN IF NOT EXISTS priority     integer        NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS finished_at  timestamptz,
  ADD COLUMN IF NOT EXISTS returnvalue  jsonb,
  ADD COLUMN IF NOT EXISTS failedreason text;

-- 5) Helpful indexes (idempotent)
CREATE INDEX IF NOT EXISTS queue_scrape_status_created_idx
  ON nuq.queue_scrape (status, created_at);

CREATE INDEX IF NOT EXISTS queue_scrape_status_prio_created_idx
  ON nuq.queue_scrape (status, priority, created_at);

-- 6) Optional grants (only if the 'firecrawl' role exists)
DO $do$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'firecrawl') THEN
    EXECUTE 'GRANT USAGE ON SCHEMA nuq TO firecrawl';
    EXECUTE 'GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA nuq TO firecrawl';
    EXECUTE 'ALTER DEFAULT PRIVILEGES IN SCHEMA nuq GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO firecrawl';
  END IF;
END
$do$;

COMMIT;
