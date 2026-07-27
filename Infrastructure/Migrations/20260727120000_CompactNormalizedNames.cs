namespace Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260727120000_CompactNormalizedNames")]
public sealed class CompactNormalizedNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        DropAffectedUniqueIndexes(migrationBuilder);

        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE _genre_merge ON COMMIT DROP AS
            SELECT "Id" AS "OldId", "KeepId"
            FROM (
                SELECT
                    "Id",
                    FIRST_VALUE("Id") OVER (
                        PARTITION BY regexp_replace("NormalizedName", '[[:space:]]+', '', 'g')
                        ORDER BY "Created", "Id") AS "KeepId"
                FROM "Genres"
            ) ranked
            WHERE "Id" <> "KeepId";

            INSERT INTO "BookGenre" ("BookId", "GenreId")
            SELECT relation."BookId", mapping."KeepId"
            FROM "BookGenre" relation
            JOIN _genre_merge mapping ON mapping."OldId" = relation."GenreId"
            ON CONFLICT ("BookId", "GenreId") DO NOTHING;

            DELETE FROM "BookGenre"
            WHERE "GenreId" IN (SELECT "OldId" FROM _genre_merge);

            UPDATE "Genres" keeper
            SET "Description" = COALESCE(
                NULLIF(keeper."Description", ''),
                (
                    SELECT duplicate."Description"
                    FROM _genre_merge mapping
                    JOIN "Genres" duplicate ON duplicate."Id" = mapping."OldId"
                    WHERE mapping."KeepId" = keeper."Id"
                      AND NULLIF(duplicate."Description", '') IS NOT NULL
                    ORDER BY length(duplicate."Description") DESC, duplicate."Created", duplicate."Id"
                    LIMIT 1
                ))
            WHERE EXISTS (
                SELECT 1 FROM _genre_merge mapping WHERE mapping."KeepId" = keeper."Id");

            DELETE FROM "Genres"
            WHERE "Id" IN (SELECT "OldId" FROM _genre_merge);

            CREATE TEMP TABLE _tag_merge ON COMMIT DROP AS
            SELECT "Id" AS "OldId", "KeepId"
            FROM (
                SELECT
                    "Id",
                    FIRST_VALUE("Id") OVER (
                        PARTITION BY
                            "IsGlobal",
                            CASE WHEN "IsGlobal" THEN NULL::uuid ELSE "OwnerId" END,
                            regexp_replace("NormalizedName", '[[:space:]]+', '', 'g')
                        ORDER BY "Created", "Id") AS "KeepId"
                FROM "Tags"
            ) ranked
            WHERE "Id" <> "KeepId";

            INSERT INTO "BookTag" ("BookId", "TagId")
            SELECT relation."BookId", mapping."KeepId"
            FROM "BookTag" relation
            JOIN _tag_merge mapping ON mapping."OldId" = relation."TagId"
            ON CONFLICT ("BookId", "TagId") DO NOTHING;

            DELETE FROM "BookTag"
            WHERE "TagId" IN (SELECT "OldId" FROM _tag_merge);

            INSERT INTO "BookShareTagPromotions" ("TagId")
            SELECT DISTINCT mapping."KeepId"
            FROM "BookShareTagPromotions" promotion
            JOIN _tag_merge mapping ON mapping."OldId" = promotion."TagId"
            ON CONFLICT ("TagId") DO NOTHING;

            DELETE FROM "BookShareTagPromotions"
            WHERE "TagId" IN (SELECT "OldId" FROM _tag_merge);

            UPDATE "PublicBookSnapshots" snapshot
            SET "PublicTagIdsJson" = (
                SELECT COALESCE(jsonb_agg(DISTINCT COALESCE(mapping."KeepId"::text, tag_id)), '[]'::jsonb)::text
                FROM jsonb_array_elements_text(snapshot."PublicTagIdsJson"::jsonb) tag_id
                LEFT JOIN _tag_merge mapping ON mapping."OldId"::text = tag_id)
            WHERE EXISTS (
                SELECT 1
                FROM jsonb_array_elements_text(snapshot."PublicTagIdsJson"::jsonb) tag_id
                JOIN _tag_merge mapping ON mapping."OldId"::text = tag_id);

            UPDATE "Tags" keeper
            SET
                "Description" = COALESCE(
                    NULLIF(keeper."Description", ''),
                    (
                        SELECT duplicate."Description"
                        FROM _tag_merge mapping
                        JOIN "Tags" duplicate ON duplicate."Id" = mapping."OldId"
                        WHERE mapping."KeepId" = keeper."Id"
                          AND NULLIF(duplicate."Description", '') IS NOT NULL
                        ORDER BY length(duplicate."Description") DESC, duplicate."Created", duplicate."Id"
                        LIMIT 1
                    )),
                "Color" = COALESCE(
                    NULLIF(keeper."Color", ''),
                    (
                        SELECT duplicate."Color"
                        FROM _tag_merge mapping
                        JOIN "Tags" duplicate ON duplicate."Id" = mapping."OldId"
                        WHERE mapping."KeepId" = keeper."Id"
                          AND NULLIF(duplicate."Color", '') IS NOT NULL
                        ORDER BY duplicate."Created", duplicate."Id"
                        LIMIT 1
                    ))
            WHERE EXISTS (
                SELECT 1 FROM _tag_merge mapping WHERE mapping."KeepId" = keeper."Id");

            DELETE FROM "Tags"
            WHERE "Id" IN (SELECT "OldId" FROM _tag_merge);

            CREATE TEMP TABLE _author_merge ON COMMIT DROP AS
            SELECT "Id" AS "OldId", "KeepId"
            FROM (
                SELECT
                    "Id",
                    FIRST_VALUE("Id") OVER (
                        PARTITION BY
                            "IsPublic",
                            CASE WHEN "IsPublic" THEN NULL::uuid ELSE "OwnerId" END,
                            regexp_replace("NormalizedPrimaryName", '[[:space:]]+', '', 'g')
                        ORDER BY "Created", "Id") AS "KeepId"
                FROM "Authors"
            ) ranked
            WHERE "Id" <> "KeepId";

            UPDATE "Books" book
            SET "AuthorId" = mapping."KeepId"
            FROM _author_merge mapping
            WHERE book."AuthorId" = mapping."OldId";

            UPDATE "PublicBookSnapshots" snapshot
            SET "PublicAuthorId" = mapping."KeepId"
            FROM _author_merge mapping
            WHERE snapshot."PublicAuthorId" = mapping."OldId";

            INSERT INTO "BookShareAuthorPromotions" ("AuthorId")
            SELECT DISTINCT mapping."KeepId"
            FROM "BookShareAuthorPromotions" promotion
            JOIN _author_merge mapping ON mapping."OldId" = promotion."AuthorId"
            ON CONFLICT ("AuthorId") DO NOTHING;

            DELETE FROM "BookShareAuthorPromotions"
            WHERE "AuthorId" IN (SELECT "OldId" FROM _author_merge);

            UPDATE "AuthorNames" name
            SET "AuthorId" = mapping."KeepId"
            FROM _author_merge mapping
            WHERE name."AuthorId" = mapping."OldId";

            DELETE FROM "Authors"
            WHERE "Id" IN (SELECT "OldId" FROM _author_merge);

            DELETE FROM "AuthorNames"
            WHERE "Id" IN (
                SELECT "Id"
                FROM (
                    SELECT
                        "Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                "AuthorId",
                                regexp_replace("NormalizedName", '[[:space:]]+', '', 'g')
                            ORDER BY "IsPrimary" DESC, "Created", "Id") AS duplicate_number
                    FROM "AuthorNames"
                ) ranked
                WHERE duplicate_number > 1);

            CREATE TEMP TABLE _book_merge ON COMMIT DROP AS
            SELECT "Id" AS "OldId", "KeepId"
            FROM (
                SELECT
                    "Id",
                    FIRST_VALUE("Id") OVER (
                        PARTITION BY
                            "OwnerId",
                            "ContentTypeId",
                            regexp_replace("NormalizedPrimaryTitle", '[[:space:]]+', '', 'g')
                        ORDER BY "Created", "Id") AS "KeepId"
                FROM "Books"
            ) ranked
            WHERE "Id" <> "KeepId";

            CREATE TEMP TABLE _book_members ON COMMIT DROP AS
            SELECT "KeepId", "KeepId" AS "BookId" FROM _book_merge
            UNION
            SELECT "KeepId", "OldId" AS "BookId" FROM _book_merge;

            WITH merged_values AS (
                SELECT
                    members."KeepId",
                    MAX(NULLIF(book."Description", '')) AS "Description",
                    MAX(book."TotalChapters") AS "TotalChapters",
                    MAX(book."CurrentChapterNumber") AS "CurrentChapterNumber",
                    MIN(book."Priority") AS "Priority",
                    MAX(book."Rating") AS "Rating",
                    MAX(NULLIF(book."Notes", '')) AS "Notes",
                    MAX(NULLIF(book."RawImportedLine", '')) AS "RawImportedLine",
                    MAX(book."LastModified") AS "LastModified"
                FROM _book_members members
                JOIN "Books" book ON book."Id" = members."BookId"
                GROUP BY members."KeepId"
            ),
            progress_values AS (
                SELECT DISTINCT ON (members."KeepId")
                    members."KeepId",
                    book."StatusId",
                    book."CurrentChapterLabel"
                FROM _book_members members
                JOIN "Books" book ON book."Id" = members."BookId"
                ORDER BY
                    members."KeepId",
                    book."CurrentChapterNumber" DESC NULLS LAST,
                    book."LastModified" DESC,
                    book."Id"
            ),
            author_values AS (
                SELECT DISTINCT ON (members."KeepId")
                    members."KeepId",
                    book."AuthorId"
                FROM _book_members members
                JOIN "Books" book ON book."Id" = members."BookId"
                WHERE book."AuthorId" IS NOT NULL
                ORDER BY members."KeepId", (book."Id" = members."KeepId") DESC, book."Created", book."Id"
            )
            UPDATE "Books" keeper
            SET
                "Description" = COALESCE(NULLIF(keeper."Description", ''), merged."Description"),
                "AuthorId" = COALESCE(keeper."AuthorId", author_value."AuthorId"),
                "StatusId" = progress."StatusId",
                "TotalChapters" = merged."TotalChapters",
                "CurrentChapterNumber" = merged."CurrentChapterNumber",
                "CurrentChapterLabel" = COALESCE(progress."CurrentChapterLabel", keeper."CurrentChapterLabel"),
                "Priority" = merged."Priority",
                "Rating" = merged."Rating",
                "Notes" = COALESCE(NULLIF(keeper."Notes", ''), merged."Notes"),
                "RawImportedLine" = COALESCE(NULLIF(keeper."RawImportedLine", ''), merged."RawImportedLine"),
                "LastModified" = merged."LastModified"
            FROM merged_values merged
            JOIN progress_values progress ON progress."KeepId" = merged."KeepId"
            LEFT JOIN author_values author_value ON author_value."KeepId" = merged."KeepId"
            WHERE keeper."Id" = merged."KeepId";

            INSERT INTO "BookGenre" ("BookId", "GenreId")
            SELECT mapping."KeepId", relation."GenreId"
            FROM "BookGenre" relation
            JOIN _book_merge mapping ON mapping."OldId" = relation."BookId"
            ON CONFLICT ("BookId", "GenreId") DO NOTHING;

            DELETE FROM "BookGenre"
            WHERE "BookId" IN (SELECT "OldId" FROM _book_merge);

            INSERT INTO "BookTag" ("BookId", "TagId")
            SELECT mapping."KeepId", relation."TagId"
            FROM "BookTag" relation
            JOIN _book_merge mapping ON mapping."OldId" = relation."BookId"
            ON CONFLICT ("BookId", "TagId") DO NOTHING;

            DELETE FROM "BookTag"
            WHERE "BookId" IN (SELECT "OldId" FROM _book_merge);

            UPDATE "BookLinks" relation
            SET "BookId" = mapping."KeepId"
            FROM _book_merge mapping
            WHERE relation."BookId" = mapping."OldId";

            UPDATE "BookProgressHistory" relation
            SET "BookId" = mapping."KeepId"
            FROM _book_merge mapping
            WHERE relation."BookId" = mapping."OldId";

            UPDATE "BookTitles" relation
            SET "BookId" = mapping."KeepId"
            FROM _book_merge mapping
            WHERE relation."BookId" = mapping."OldId";

            CREATE TEMP TABLE _discarded_storage (
                "StoragePath" text PRIMARY KEY
            ) ON COMMIT DROP;

            CREATE TEMP TABLE _ranked_covers ON COMMIT DROP AS
            SELECT
                cover."Id",
                members."KeepId",
                ROW_NUMBER() OVER (
                    PARTITION BY members."KeepId"
                    ORDER BY
                        (cover."StoragePath" IS NOT NULL OR cover."ThumbnailStoragePath" IS NOT NULL) DESC,
                        (cover."BookId" = members."KeepId") DESC,
                        cover."LastModified" DESC,
                        cover."Id") AS cover_number
            FROM _book_members members
            JOIN "BookCovers" cover ON cover."BookId" = members."BookId";

            INSERT INTO _discarded_storage ("StoragePath")
            SELECT cover."StoragePath"
            FROM "BookCovers" cover
            JOIN _ranked_covers ranked ON ranked."Id" = cover."Id"
            WHERE ranked.cover_number > 1 AND cover."StoragePath" IS NOT NULL
            ON CONFLICT DO NOTHING;

            INSERT INTO _discarded_storage ("StoragePath")
            SELECT cover."ThumbnailStoragePath"
            FROM "BookCovers" cover
            JOIN _ranked_covers ranked ON ranked."Id" = cover."Id"
            WHERE ranked.cover_number > 1 AND cover."ThumbnailStoragePath" IS NOT NULL
            ON CONFLICT DO NOTHING;

            DELETE FROM "BookCovers"
            WHERE "Id" IN (
                SELECT "Id" FROM _ranked_covers WHERE cover_number > 1);

            UPDATE "BookCovers" cover
            SET "BookId" = ranked."KeepId"
            FROM _ranked_covers ranked
            WHERE cover."Id" = ranked."Id"
              AND ranked.cover_number = 1
              AND cover."BookId" <> ranked."KeepId";

            CREATE TEMP TABLE _ranked_snapshots ON COMMIT DROP AS
            SELECT
                snapshot."Id",
                members."KeepId",
                ROW_NUMBER() OVER (
                    PARTITION BY members."KeepId"
                    ORDER BY
                        snapshot."SnapshotAt" DESC,
                        (snapshot."SourceBookId" = members."KeepId") DESC,
                        snapshot."Id") AS snapshot_number
            FROM _book_members members
            JOIN "PublicBookSnapshots" snapshot ON snapshot."SourceBookId" = members."BookId";

            INSERT INTO _discarded_storage ("StoragePath")
            SELECT snapshot."CoverStoragePath"
            FROM "PublicBookSnapshots" snapshot
            JOIN _ranked_snapshots ranked ON ranked."Id" = snapshot."Id"
            WHERE ranked.snapshot_number > 1 AND snapshot."CoverStoragePath" IS NOT NULL
            ON CONFLICT DO NOTHING;

            INSERT INTO _discarded_storage ("StoragePath")
            SELECT snapshot."CoverThumbnailStoragePath"
            FROM "PublicBookSnapshots" snapshot
            JOIN _ranked_snapshots ranked ON ranked."Id" = snapshot."Id"
            WHERE ranked.snapshot_number > 1 AND snapshot."CoverThumbnailStoragePath" IS NOT NULL
            ON CONFLICT DO NOTHING;

            DELETE FROM "PublicBookSnapshots"
            WHERE "Id" IN (
                SELECT "Id" FROM _ranked_snapshots WHERE snapshot_number > 1);

            UPDATE "PublicBookSnapshots" snapshot
            SET "SourceBookId" = ranked."KeepId"
            FROM _ranked_snapshots ranked
            WHERE snapshot."Id" = ranked."Id"
              AND ranked.snapshot_number = 1
              AND snapshot."SourceBookId" <> ranked."KeepId";

            DELETE FROM "Books"
            WHERE "Id" IN (SELECT "OldId" FROM _book_merge);

            DELETE FROM "BookTitles"
            WHERE "Id" IN (
                SELECT "Id"
                FROM (
                    SELECT
                        title."Id",
                        ROW_NUMBER() OVER (
                            PARTITION BY
                                title."BookId",
                                regexp_replace(title."NormalizedTitle", '[[:space:]]+', '', 'g')
                            ORDER BY
                                (
                                    regexp_replace(title."NormalizedTitle", '[[:space:]]+', '', 'g') =
                                    regexp_replace(book."NormalizedPrimaryTitle", '[[:space:]]+', '', 'g')
                                ) DESC,
                                title."IsPrimary" DESC,
                                title."Created",
                                title."Id") AS duplicate_number
                    FROM "BookTitles" title
                    JOIN "Books" book ON book."Id" = title."BookId"
                ) ranked
                WHERE duplicate_number > 1);

            WITH primary_titles AS (
                SELECT DISTINCT ON (title."BookId")
                    title."BookId",
                    title."Id"
                FROM "BookTitles" title
                JOIN "Books" book ON book."Id" = title."BookId"
                ORDER BY
                    title."BookId",
                    (
                        regexp_replace(title."NormalizedTitle", '[[:space:]]+', '', 'g') =
                        regexp_replace(book."NormalizedPrimaryTitle", '[[:space:]]+', '', 'g')
                    ) DESC,
                    title."IsPrimary" DESC,
                    title."Created",
                    title."Id"
            )
            UPDATE "BookTitles" title
            SET "IsPrimary" = title."Id" = primary_title."Id"
            FROM primary_titles primary_title
            WHERE title."BookId" = primary_title."BookId";

            INSERT INTO "StorageCleanupQueueItems" (
                "Id", "StoragePath", "AttemptCount", "NextAttemptAt", "LastError")
            SELECT gen_random_uuid(), discarded."StoragePath", 0, CURRENT_TIMESTAMP, NULL
            FROM _discarded_storage discarded
            WHERE NOT EXISTS (
                    SELECT 1
                    FROM "BookCovers" cover
                    WHERE cover."StoragePath" = discarded."StoragePath"
                       OR cover."ThumbnailStoragePath" = discarded."StoragePath")
              AND NOT EXISTS (
                    SELECT 1
                    FROM "PublicBookSnapshots" snapshot
                    WHERE snapshot."CoverStoragePath" = discarded."StoragePath"
                       OR snapshot."CoverThumbnailStoragePath" = discarded."StoragePath")
            ON CONFLICT ("StoragePath") DO NOTHING;

            UPDATE "Genres"
            SET "NormalizedName" = regexp_replace("NormalizedName", '[[:space:]]+', '', 'g');

            UPDATE "Tags"
            SET "NormalizedName" = regexp_replace("NormalizedName", '[[:space:]]+', '', 'g');

            UPDATE "Authors"
            SET "NormalizedPrimaryName" =
                regexp_replace("NormalizedPrimaryName", '[[:space:]]+', '', 'g');

            UPDATE "AuthorNames"
            SET "NormalizedName" = regexp_replace("NormalizedName", '[[:space:]]+', '', 'g');

            UPDATE "Books"
            SET "NormalizedPrimaryTitle" =
                regexp_replace("NormalizedPrimaryTitle", '[[:space:]]+', '', 'g');

            UPDATE "BookTitles"
            SET "NormalizedTitle" = regexp_replace("NormalizedTitle", '[[:space:]]+', '', 'g');

            UPDATE "PublicBookSnapshots"
            SET "NormalizedPrimaryTitle" =
                regexp_replace("NormalizedPrimaryTitle", '[[:space:]]+', '', 'g');
            """);

        RecreateAffectedUniqueIndexes(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Whitespace and records removed by a merge cannot be reconstructed reliably.
    }

    private static void DropAffectedUniqueIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Genres_NormalizedName", table: "Genres");
        migrationBuilder.DropIndex(name: "IX_Tags_OwnerId_NormalizedName", table: "Tags");
        migrationBuilder.DropIndex(name: "IX_Tags_NormalizedName", table: "Tags");
        migrationBuilder.DropIndex(name: "IX_Authors_NormalizedPrimaryName", table: "Authors");
        migrationBuilder.DropIndex(name: "IX_Authors_OwnerId_NormalizedPrimaryName", table: "Authors");
        migrationBuilder.DropIndex(name: "IX_AuthorNames_AuthorId_NormalizedName", table: "AuthorNames");
        migrationBuilder.DropIndex(
            name: "IX_Books_OwnerId_NormalizedPrimaryTitle_ContentTypeId",
            table: "Books");
        migrationBuilder.DropIndex(name: "IX_BookTitles_BookId_NormalizedTitle", table: "BookTitles");
        migrationBuilder.DropIndex(name: "IX_PublicBookSnapshots_SourceBookId", table: "PublicBookSnapshots");
    }

    private static void RecreateAffectedUniqueIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Genres_NormalizedName",
            table: "Genres",
            column: "NormalizedName",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Tags_OwnerId_NormalizedName",
            table: "Tags",
            columns: ["OwnerId", "NormalizedName"],
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Tags_NormalizedName",
            table: "Tags",
            column: "NormalizedName",
            unique: true,
            filter: "\"IsGlobal\" = TRUE");
        migrationBuilder.CreateIndex(
            name: "IX_Authors_NormalizedPrimaryName",
            table: "Authors",
            column: "NormalizedPrimaryName",
            unique: true,
            filter: "\"IsPublic\" = TRUE");
        migrationBuilder.CreateIndex(
            name: "IX_Authors_OwnerId_NormalizedPrimaryName",
            table: "Authors",
            columns: ["OwnerId", "NormalizedPrimaryName"],
            unique: true,
            filter: "\"IsPublic\" = FALSE");
        migrationBuilder.CreateIndex(
            name: "IX_AuthorNames_AuthorId_NormalizedName",
            table: "AuthorNames",
            columns: ["AuthorId", "NormalizedName"],
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_Books_OwnerId_NormalizedPrimaryTitle_ContentTypeId",
            table: "Books",
            columns: ["OwnerId", "NormalizedPrimaryTitle", "ContentTypeId"],
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_BookTitles_BookId_NormalizedTitle",
            table: "BookTitles",
            columns: ["BookId", "NormalizedTitle"],
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PublicBookSnapshots_SourceBookId",
            table: "PublicBookSnapshots",
            column: "SourceBookId",
            unique: true);
    }
}
