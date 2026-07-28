using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueuedBookSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchDocument",
                table: "Books",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Books",
                type: "tsvector",
                nullable: false,
                defaultValueSql: "''::tsvector");

            migrationBuilder.CreateTable(
                name: "BookSearchIndexQueueItems",
                columns: table => new
                {
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookSearchIndexQueueItems", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_BookSearchIndexQueueItems_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookSearchIndexQueueItems_NextAttemptAt_LeaseUntil",
                table: "BookSearchIndexQueueItems",
                columns: new[] { "NextAttemptAt", "LeaseUntil" });

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Books_FullTextSearch";
                CREATE EXTENSION IF NOT EXISTS pg_trgm;

                CREATE OR REPLACE FUNCTION refresh_book_search_index(p_book_id uuid)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    document text;
                BEGIN
                    SELECT lower(concat_ws(
                        ' ',
                        book."PrimaryTitle",
                        (
                            SELECT string_agg(DISTINCT title."Title", ' ' ORDER BY title."Title")
                            FROM "BookTitles" AS title
                            WHERE title."BookId" = book."Id"
                        ),
                        author."PrimaryName",
                        (
                            SELECT string_agg(DISTINCT author_name."Name", ' ' ORDER BY author_name."Name")
                            FROM "AuthorNames" AS author_name
                            WHERE author_name."AuthorId" = book."AuthorId"
                        ),
                        content_type."Name",
                        content_type."Slug",
                        status."Name",
                        status."Slug",
                        (
                            SELECT string_agg(DISTINCT genre."Name", ' ' ORDER BY genre."Name")
                            FROM "BookGenre" AS book_genre
                            INNER JOIN "Genres" AS genre ON genre."Id" = book_genre."GenreId"
                            WHERE book_genre."BookId" = book."Id"
                        ),
                        (
                            SELECT string_agg(DISTINCT tag."Name", ' ' ORDER BY tag."Name")
                            FROM "BookTag" AS book_tag
                            INNER JOIN "Tags" AS tag ON tag."Id" = book_tag."TagId"
                            WHERE book_tag."BookId" = book."Id"
                        )
                    ))
                    INTO document
                    FROM "Books" AS book
                    LEFT JOIN "Authors" AS author ON author."Id" = book."AuthorId"
                    INNER JOIN "ContentTypes" AS content_type ON content_type."Id" = book."ContentTypeId"
                    INNER JOIN "Statuses" AS status ON status."Id" = book."StatusId"
                    WHERE book."Id" = p_book_id;

                    IF FOUND THEN
                        UPDATE "Books"
                        SET "SearchDocument" = coalesce(document, ''),
                            "SearchVector" = to_tsvector('simple', coalesce(document, ''))
                        WHERE "Id" = p_book_id;
                    END IF;
                END;
                $$;

                CREATE OR REPLACE FUNCTION enqueue_book_search_indexes(p_book_ids uuid[])
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF coalesce(array_length(p_book_ids, 1), 0) = 0 THEN
                        RETURN;
                    END IF;

                    INSERT INTO "BookSearchIndexQueueItems" (
                        "BookId",
                        "EnqueuedAt",
                        "AttemptCount",
                        "NextAttemptAt",
                        "LastError",
                        "LeaseId",
                        "LeaseUntil"
                    )
                    SELECT DISTINCT
                        requested."BookId",
                        clock_timestamp(),
                        0,
                        clock_timestamp(),
                        NULL::text,
                        NULL::uuid,
                        NULL::timestamp with time zone
                    FROM unnest(p_book_ids) AS requested("BookId")
                    INNER JOIN "Books" AS book ON book."Id" = requested."BookId"
                    ON CONFLICT ("BookId") DO UPDATE
                    SET "EnqueuedAt" = EXCLUDED."EnqueuedAt",
                        "AttemptCount" = 0,
                        "NextAttemptAt" = EXCLUDED."NextAttemptAt",
                        "LastError" = NULL;

                    IF FOUND THEN
                        PERFORM pg_notify('book_search_index_changed', '');
                    END IF;
                END;
                $$;

                CREATE OR REPLACE FUNCTION queue_book_search_from_book()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    PERFORM enqueue_book_search_indexes(ARRAY[NEW."Id"]);
                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION queue_book_search_from_book_relation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        PERFORM enqueue_book_search_indexes(ARRAY[NEW."BookId"]);
                        RETURN NEW;
                    ELSIF TG_OP = 'DELETE' THEN
                        PERFORM enqueue_book_search_indexes(ARRAY[OLD."BookId"]);
                        RETURN OLD;
                    END IF;

                    PERFORM enqueue_book_search_indexes(ARRAY[OLD."BookId", NEW."BookId"]);
                    RETURN NEW;
                END;
                $$;

                CREATE OR REPLACE FUNCTION queue_book_search_from_author_name()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    author_ids uuid[];
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        author_ids := ARRAY[NEW."AuthorId"];
                    ELSIF TG_OP = 'DELETE' THEN
                        author_ids := ARRAY[OLD."AuthorId"];
                    ELSE
                        author_ids := ARRAY[OLD."AuthorId", NEW."AuthorId"];
                    END IF;

                    PERFORM enqueue_book_search_indexes(ARRAY(
                        SELECT book."Id"
                        FROM "Books" AS book
                        WHERE book."AuthorId" = ANY(author_ids)
                    ));
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION queue_book_search_from_lookup()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    book_ids uuid[];
                BEGIN
                    CASE TG_TABLE_NAME
                        WHEN 'Authors' THEN
                            book_ids := ARRAY(
                                SELECT book."Id" FROM "Books" AS book WHERE book."AuthorId" = NEW."Id"
                            );
                        WHEN 'Genres' THEN
                            book_ids := ARRAY(
                                SELECT book_genre."BookId"
                                FROM "BookGenre" AS book_genre
                                WHERE book_genre."GenreId" = NEW."Id"
                            );
                        WHEN 'Tags' THEN
                            book_ids := ARRAY(
                                SELECT book_tag."BookId"
                                FROM "BookTag" AS book_tag
                                WHERE book_tag."TagId" = NEW."Id"
                            );
                        WHEN 'Statuses' THEN
                            book_ids := ARRAY(
                                SELECT book."Id" FROM "Books" AS book WHERE book."StatusId" = NEW."Id"
                            );
                        WHEN 'ContentTypes' THEN
                            book_ids := ARRAY(
                                SELECT book."Id" FROM "Books" AS book WHERE book."ContentTypeId" = NEW."Id"
                            );
                        ELSE
                            book_ids := ARRAY[]::uuid[];
                    END CASE;

                    PERFORM enqueue_book_search_indexes(book_ids);
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER "TR_Books_QueueSearchIndex"
                AFTER INSERT OR UPDATE OF "PrimaryTitle", "AuthorId", "ContentTypeId", "StatusId"
                ON "Books"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_book();

                CREATE TRIGGER "TR_BookTitles_QueueSearchIndex"
                AFTER INSERT OR UPDATE OR DELETE
                ON "BookTitles"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_book_relation();

                CREATE TRIGGER "TR_BookGenre_QueueSearchIndex"
                AFTER INSERT OR UPDATE OR DELETE
                ON "BookGenre"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_book_relation();

                CREATE TRIGGER "TR_BookTag_QueueSearchIndex"
                AFTER INSERT OR UPDATE OR DELETE
                ON "BookTag"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_book_relation();

                CREATE TRIGGER "TR_Authors_QueueSearchIndex"
                AFTER UPDATE OF "PrimaryName"
                ON "Authors"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_lookup();

                CREATE TRIGGER "TR_AuthorNames_QueueSearchIndex"
                AFTER INSERT OR UPDATE OR DELETE
                ON "AuthorNames"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_author_name();

                CREATE TRIGGER "TR_Genres_QueueSearchIndex"
                AFTER UPDATE OF "Name"
                ON "Genres"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_lookup();

                CREATE TRIGGER "TR_Tags_QueueSearchIndex"
                AFTER UPDATE OF "Name"
                ON "Tags"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_lookup();

                CREATE TRIGGER "TR_Statuses_QueueSearchIndex"
                AFTER UPDATE OF "Name", "Slug"
                ON "Statuses"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_lookup();

                CREATE TRIGGER "TR_ContentTypes_QueueSearchIndex"
                AFTER UPDATE OF "Name", "Slug"
                ON "ContentTypes"
                FOR EACH ROW
                EXECUTE FUNCTION queue_book_search_from_lookup();

                SELECT refresh_book_search_index(book."Id")
                FROM "Books" AS book;

                DELETE FROM "BookSearchIndexQueueItems";

                CREATE INDEX "IX_Books_SearchVector"
                ON "Books"
                USING GIN ("SearchVector");

                CREATE INDEX "IX_Books_SearchDocument_Trigram"
                ON "Books"
                USING GIN ("SearchDocument" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Books_SearchDocument_Trigram";
                DROP INDEX IF EXISTS "IX_Books_SearchVector";
                DROP TRIGGER IF EXISTS "TR_Books_QueueSearchIndex" ON "Books";
                DROP TRIGGER IF EXISTS "TR_BookTitles_QueueSearchIndex" ON "BookTitles";
                DROP TRIGGER IF EXISTS "TR_BookGenre_QueueSearchIndex" ON "BookGenre";
                DROP TRIGGER IF EXISTS "TR_BookTag_QueueSearchIndex" ON "BookTag";
                DROP TRIGGER IF EXISTS "TR_Authors_QueueSearchIndex" ON "Authors";
                DROP TRIGGER IF EXISTS "TR_AuthorNames_QueueSearchIndex" ON "AuthorNames";
                DROP TRIGGER IF EXISTS "TR_Genres_QueueSearchIndex" ON "Genres";
                DROP TRIGGER IF EXISTS "TR_Tags_QueueSearchIndex" ON "Tags";
                DROP TRIGGER IF EXISTS "TR_Statuses_QueueSearchIndex" ON "Statuses";
                DROP TRIGGER IF EXISTS "TR_ContentTypes_QueueSearchIndex" ON "ContentTypes";
                DROP FUNCTION IF EXISTS queue_book_search_from_book();
                DROP FUNCTION IF EXISTS queue_book_search_from_book_relation();
                DROP FUNCTION IF EXISTS queue_book_search_from_author_name();
                DROP FUNCTION IF EXISTS queue_book_search_from_lookup();
                DROP FUNCTION IF EXISTS enqueue_book_search_indexes(uuid[]);
                DROP FUNCTION IF EXISTS refresh_book_search_index(uuid);

                CREATE INDEX "IX_Books_FullTextSearch"
                ON "Books"
                USING GIN (
                    to_tsvector(
                        'simple',
                        coalesce("PrimaryTitle", '') || ' ' ||
                        coalesce("Description", '') || ' ' ||
                        coalesce("Notes", '')
                    )
                );
                """);

            migrationBuilder.DropTable(
                name: "BookSearchIndexQueueItems");

            migrationBuilder.DropColumn(
                name: "SearchDocument",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Books");
        }
    }
}
