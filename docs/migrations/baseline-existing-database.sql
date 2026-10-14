-- ONE-TIME baseline for a database that already has this schema (the Supabase staging database).
--
-- That database was built by hand: its __EFMigrationsHistory holds two rows from migrations that were
-- never committed (20260929080838_InitialCreate, 20260929105946_AddAuth) and 001-004 were applied as
-- SQL. Data/Migrations/20261014210441_InitialCreate.cs describes the same schema
-- (186 columns and 37 indexes compared and identical on 2026-10-14), so this script just tells EF
-- that migration is already applied. It changes no tables or data.
--
-- Run it once, by hand, against staging only. NEVER against a fresh database (production builds its
-- schema by running the migration; running this there would skip it). Fresh databases need nothing.
--
-- The guard aborts unless the database really looks like the old hand-built one.
BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929080838_InitialCreate') THEN
    RAISE EXCEPTION 'Not the expected old database: baseline not applied.';
  END IF;
  IF (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public') <> 19 THEN
    RAISE EXCEPTION 'Expected 19 tables in public (18 + history): baseline not applied.';
  END IF;
END $$;

DELETE FROM "__EFMigrationsHistory"
 WHERE "MigrationId" IN ('20260929080838_InitialCreate', '20260929105946_AddAuth');

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014210441_InitialCreate', '10.0.0');

COMMIT;
