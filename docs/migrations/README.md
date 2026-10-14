# Database migrations

The schema is owned by EF Core migrations in `Data/Migrations/`. They are the only way to change it.

- Change an entity or its configuration, then `dotnet ef migrations add <Name>`. Commit the three generated files.
- `dotnet Arlink28.Api.dll migrate` (or `dotnet ef database update`) applies what is pending. The API never migrates on startup; a deploy runs `migrate` first.
- CI builds an empty Postgres from the migrations and fails if the model has drifted from them.
- Set `MIGRATION_CONNECTION` to point `dotnet ef` at a specific database. Without it the tool uses your user secrets (the dev/staging Supabase database).

`001`-`004` here are the old hand-applied scripts, kept for history. Their effect is already inside `InitialCreate`. Do not run them on a database built from migrations.

`baseline-existing-database.sql` is a one-time script for the existing Supabase database; see its header.
