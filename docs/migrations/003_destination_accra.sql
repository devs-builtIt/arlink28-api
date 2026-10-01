-- Adds Accra, Ghana as a destination so flights, hotel reservations and visas to Ghana can be listed.
-- Idempotent. Also added to Data/Seed/CatalogueSeedData.cs so a reseed keeps it.

INSERT INTO "Destinations" ("Id", "Slug", "Name", "Country", "CreatedAt", "UpdatedAt")
VALUES (gen_random_uuid(), 'accra', 'Accra', 'GH', now(), now())
ON CONFLICT ("Slug") DO NOTHING;
