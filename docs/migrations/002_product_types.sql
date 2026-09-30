-- Product types: a package is now a holiday package, flight, hotel reservation or visa support
-- (see arlink28-nextjs docs, "package categories"). Existing rows are all holiday packages.
-- Idempotent. Not applied to any database yet. Apply BEFORE deploying an API build that knows about
-- "ProductType", or every package query will fail on the missing column.

ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "ProductType" character varying(20) NOT NULL DEFAULT 'HolidayPackage';
ALTER TABLE "Packages" ADD COLUMN IF NOT EXISTS "Details" jsonb;

CREATE INDEX IF NOT EXISTS "IX_Packages_ProductType_Status" ON "Packages" ("ProductType", "Status");
