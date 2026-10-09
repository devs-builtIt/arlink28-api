-- Destination profiles: country/place hierarchy, publish status, descriptive fields, hero photo,
-- and a child table of attractions. See arlink28-nextjs/docs/destinations-plan.md.
-- Idempotent: safe to run more than once. Existing destinations stay Published.

ALTER TABLE "Destinations"
    ADD COLUMN IF NOT EXISTS "Kind"            varchar(20)  NOT NULL DEFAULT 'Place',
    ADD COLUMN IF NOT EXISTS "Status"          varchar(20)  NOT NULL DEFAULT 'Published',
    ADD COLUMN IF NOT EXISTS "ParentId"        uuid         NULL,
    ADD COLUMN IF NOT EXISTS "Tagline"         varchar(160) NULL,
    ADD COLUMN IF NOT EXISTS "Summary"         varchar(400) NULL,
    ADD COLUMN IF NOT EXISTS "Description"     text         NULL,
    ADD COLUMN IF NOT EXISTS "BestTimeToVisit" varchar(200) NULL,
    ADD COLUMN IF NOT EXISTS "HeroPath"        varchar(255) NULL,
    ADD COLUMN IF NOT EXISTS "HeroAlt"         varchar(250) NULL,
    ADD COLUMN IF NOT EXISTS "HeroCredit"      varchar(250) NULL,
    ADD COLUMN IF NOT EXISTS "Latitude"        double precision NULL,
    ADD COLUMN IF NOT EXISTS "Longitude"       double precision NULL,
    ADD COLUMN IF NOT EXISTS "SortOrder"       integer      NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Destinations_Destinations_ParentId') THEN
        ALTER TABLE "Destinations"
            ADD CONSTRAINT "FK_Destinations_Destinations_ParentId"
            FOREIGN KEY ("ParentId") REFERENCES "Destinations" ("Id") ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Destinations_ParentId" ON "Destinations" ("ParentId");
CREATE INDEX IF NOT EXISTS "IX_Destinations_Status_Country_SortOrder" ON "Destinations" ("Status", "Country", "SortOrder");

CREATE TABLE IF NOT EXISTS "DestinationAttractions" (
    "Id"            uuid         NOT NULL DEFAULT gen_random_uuid(),
    "DestinationId" uuid         NOT NULL,
    "Name"          varchar(120) NOT NULL,
    "Summary"       varchar(300) NULL,
    "PhotoPath"     varchar(255) NULL,
    "PhotoAlt"      varchar(250) NULL,
    "PhotoCredit"   varchar(250) NULL,
    "SortOrder"     integer      NOT NULL DEFAULT 0,
    "CreatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_DestinationAttractions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DestinationAttractions_Destinations_DestinationId"
        FOREIGN KEY ("DestinationId") REFERENCES "Destinations" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_DestinationAttractions_DestinationId_SortOrder"
    ON "DestinationAttractions" ("DestinationId", "SortOrder");

-- Country rows and the first African places. New rows start as Draft: they need copy and a hero
-- photo before they can be published. Mirrored in Data/Seed/CatalogueSeedData.cs.
INSERT INTO "Destinations" ("Id", "Slug", "Name", "Country", "Kind", "Status", "CreatedAt", "UpdatedAt") VALUES
    (gen_random_uuid(), 'kenya',    'Kenya',    'KE', 'Country', 'Draft', now(), now()),
    (gen_random_uuid(), 'ghana',    'Ghana',    'GH', 'Country', 'Draft', now(), now()),
    (gen_random_uuid(), 'zimbabwe', 'Zimbabwe', 'ZW', 'Country', 'Draft', now(), now()),
    (gen_random_uuid(), 'botswana', 'Botswana', 'BW', 'Country', 'Draft', now(), now()),
    (gen_random_uuid(), 'egypt',    'Egypt',    'EG', 'Country', 'Draft', now(), now())
ON CONFLICT ("Slug") DO NOTHING;

INSERT INTO "Destinations" ("Id", "Slug", "Name", "Country", "Kind", "Status", "CreatedAt", "UpdatedAt") VALUES
    (gen_random_uuid(), 'victoria-falls',      'Victoria Falls',       'ZW', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'chobe-national-park', 'Chobe National Park',  'BW', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'okavango-delta',      'Okavango Delta',       'BW', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'moremi-game-reserve', 'Moremi Game Reserve',  'BW', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'makgadikgadi-pans',   'Makgadikgadi Pans',    'BW', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'giza',                'Giza',                 'EG', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'luxor',               'Luxor',                'EG', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'aswan',               'Aswan and Abu Simbel', 'EG', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'red-sea',             'Red Sea',              'EG', 'Place', 'Draft', now(), now()),
    (gen_random_uuid(), 'cairo',               'Cairo',                'EG', 'Place', 'Draft', now(), now())
ON CONFLICT ("Slug") DO NOTHING;

-- Attach every place to its country. Existing rows (Nairobi, Masai Mara, Samburu, Accra) get parents too.
UPDATE "Destinations" d
SET "ParentId" = p."Id"
FROM "Destinations" p
WHERE d."Kind" = 'Place' AND d."ParentId" IS NULL
  AND p."Kind" = 'Country' AND p."Country" = d."Country";
