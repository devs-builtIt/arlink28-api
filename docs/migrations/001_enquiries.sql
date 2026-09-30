-- Enquiries: guests' enquiries from the public contact page (see arlink28-nextjs/docs/enquiries-plan.md).
-- Generated from the EF model (dotnet ef dbcontext script) and made idempotent. Not applied to any database yet.
-- Assumes the existing tables are "Packages" and "Staff", as the EF model names them.

CREATE TABLE IF NOT EXISTS "Enquiries" (
    "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
    "Reference" character varying(20) NOT NULL,
    "Type" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "PackageId" uuid,
    "PackageTitle" character varying(200),
    "CheckIn" date,
    "Nights" integer,
    "QuotedTotalMinor" bigint,
    "Currency" character(3),
    "Name" character varying(120) NOT NULL,
    "Email" character varying(200) NOT NULL,
    "Phone" character varying(40),
    "Subject" character varying(200),
    "Message" character varying(4000),
    "ConsentAt" timestamp with time zone NOT NULL,
    "SourceUrl" character varying(500),
    "HandledById" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Enquiries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Enquiries_Packages_PackageId" FOREIGN KEY ("PackageId") REFERENCES "Packages" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Enquiries_Staff_HandledById" FOREIGN KEY ("HandledById") REFERENCES "Staff" ("Id") ON DELETE SET NULL
);

-- Supabase serves every table in "public" over its REST API. This one holds guests' names, emails and
-- phone numbers, so lock it: with no policy, only the owning role (the one this API connects as) can read it.
ALTER TABLE "Enquiries" ENABLE ROW LEVEL SECURITY;

CREATE INDEX IF NOT EXISTS "IX_Enquiries_HandledById" ON "Enquiries" ("HandledById");
CREATE INDEX IF NOT EXISTS "IX_Enquiries_PackageId" ON "Enquiries" ("PackageId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Enquiries_Reference" ON "Enquiries" ("Reference");
CREATE INDEX IF NOT EXISTS "IX_Enquiries_Status_CreatedAt" ON "Enquiries" ("Status", "CreatedAt");
