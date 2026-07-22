namespace Infrastructure.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260722220000_ReconcileStoredCoverStatus")]
public sealed class ReconcileStoredCoverStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "BookCovers"
            SET "Status" = CASE
                    WHEN "Source" IN ('ManualUpload', 'ManualUrl') THEN 'Uploaded'
                    ELSE 'Found'
                END,
                "FailureReason" = NULL,
                "LastModified" = CURRENT_TIMESTAMP
            WHERE ("StoragePath" IS NOT NULL OR "ThumbnailStoragePath" IS NOT NULL)
              AND "Status" NOT IN ('Found', 'Uploaded');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous status cannot be reconstructed safely once a stored cover has been confirmed.
    }
}
