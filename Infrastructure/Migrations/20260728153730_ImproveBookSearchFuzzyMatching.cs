using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImproveBookSearchFuzzyMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS fuzzystrmatch;

                CREATE OR REPLACE FUNCTION book_search_has_close_lexeme(
                    p_search_vector tsvector,
                    p_term text
                )
                RETURNS boolean
                LANGUAGE sql
                IMMUTABLE
                STRICT
                PARALLEL SAFE
                COST 100
                AS $$
                    WITH input AS (
                        SELECT
                            lower(btrim(p_term)) AS term,
                            CASE
                                WHEN char_length(lower(btrim(p_term))) BETWEEN 3 AND 8 THEN 1
                                WHEN char_length(lower(btrim(p_term))) BETWEEN 9 AND 64 THEN 2
                                ELSE NULL
                            END AS max_distance
                    )
                    SELECT input.term <> ''
                       AND EXISTS (
                            SELECT 1
                            FROM unnest(tsvector_to_array(p_search_vector)) AS candidate(lexeme)
                            WHERE strpos(candidate.lexeme, input.term) > 0
                               OR (
                                    input.max_distance IS NOT NULL
                                    AND char_length(candidate.lexeme) <= 64
                                    AND abs(char_length(candidate.lexeme) - char_length(input.term))
                                        <= input.max_distance
                                    AND levenshtein_less_equal(
                                            input.term,
                                            candidate.lexeme,
                                            input.max_distance
                                        ) <= input.max_distance
                               )
                        )
                    FROM input;
                $$;

                CREATE OR REPLACE FUNCTION refresh_book_search_index(p_book_id uuid)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    document text;
                BEGIN
                    SELECT string_agg(value, ' ' ORDER BY value)
                    INTO document
                    FROM (
                        SELECT DISTINCT lower(btrim(source.value)) AS value
                        FROM "Books" AS book
                        LEFT JOIN "Authors" AS author ON author."Id" = book."AuthorId"
                        INNER JOIN "ContentTypes" AS content_type
                            ON content_type."Id" = book."ContentTypeId"
                        INNER JOIN "Statuses" AS status ON status."Id" = book."StatusId"
                        CROSS JOIN LATERAL (
                            SELECT book."PrimaryTitle" AS value
                            UNION ALL
                            SELECT title."Title"
                            FROM "BookTitles" AS title
                            WHERE title."BookId" = book."Id"
                            UNION ALL
                            SELECT author."PrimaryName"
                            WHERE author."Id" IS NOT NULL
                            UNION ALL
                            SELECT author_name."Name"
                            FROM "AuthorNames" AS author_name
                            WHERE author_name."AuthorId" = book."AuthorId"
                            UNION ALL
                            SELECT content_type."Name"
                            UNION ALL
                            SELECT content_type."Slug"
                            WHERE lower(regexp_replace(btrim(content_type."Name"), '\s+', '-', 'g'))
                                <> lower(btrim(content_type."Slug"))
                            UNION ALL
                            SELECT status."Name"
                            UNION ALL
                            SELECT status."Slug"
                            WHERE lower(regexp_replace(btrim(status."Name"), '\s+', '-', 'g'))
                                <> lower(btrim(status."Slug"))
                            UNION ALL
                            SELECT genre."Name"
                            FROM "BookGenre" AS book_genre
                            INNER JOIN "Genres" AS genre ON genre."Id" = book_genre."GenreId"
                            WHERE book_genre."BookId" = book."Id"
                            UNION ALL
                            SELECT tag."Name"
                            FROM "BookTag" AS book_tag
                            INNER JOIN "Tags" AS tag ON tag."Id" = book_tag."TagId"
                            WHERE book_tag."BookId" = book."Id"
                        ) AS source
                        WHERE book."Id" = p_book_id
                          AND nullif(btrim(source.value), '') IS NOT NULL
                    ) AS unique_values;

                    IF FOUND THEN
                        UPDATE "Books"
                        SET "SearchDocument" = coalesce(document, ''),
                            "SearchVector" = to_tsvector('simple', coalesce(document, ''))
                        WHERE "Id" = p_book_id;
                    END IF;
                END;
                $$;

                SELECT refresh_book_search_index(book."Id")
                FROM "Books" AS book;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                    INNER JOIN "ContentTypes" AS content_type
                        ON content_type."Id" = book."ContentTypeId"
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

                SELECT refresh_book_search_index(book."Id")
                FROM "Books" AS book;

                DROP FUNCTION IF EXISTS book_search_has_close_lexeme(tsvector, text);
                """);
        }
    }
}
