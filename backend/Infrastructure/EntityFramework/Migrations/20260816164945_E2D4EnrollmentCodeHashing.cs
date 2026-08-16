using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class E2D4EnrollmentCodeHashing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Code",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                newName: "CodeHash");

            migrationBuilder.RenameIndex(
                name: "IX_AgentEnrollments_Code",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                newName: "IX_AgentEnrollments_CodeHash");

            migrationBuilder.AddColumn<string>(
                name: "CodePrefix",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // The rename above leaves pre-existing rows holding a PLAINTEXT code in a column now
            // named CodeHash. Replace each one with its SHA-256 hash so no usable enrollment secret
            // survives in the database, keeping only a short non-secret prefix for identification.
            //
            // Must match AgentEnrollmentCodeHasher exactly: trim, uppercase, UTF-8 bytes, lowercase
            // hex. Enrollment codes use an ASCII-only alphabet, so CONVERT to varchar yields the
            // same bytes as UTF-8. The WHERE clause makes this idempotent by skipping values that
            // are already a 64-character lowercase hex digest.
            migrationBuilder.Sql("""
                UPDATE entegrasyon.AgentEnrollments
                SET CodePrefix = LEFT(UPPER(LTRIM(RTRIM(CodeHash))), 6),
                    CodeHash = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256',
                                   CONVERT(varchar(128), UPPER(LTRIM(RTRIM(CodeHash))))), 2))
                WHERE CodeHash IS NOT NULL
                  AND (LEN(CodeHash) <> 64 OR CodeHash LIKE '%[^0-9a-f]%');
                """);
        }

        /// <inheritdoc />
        /// <remarks>Hashing is one-way, so rolling back restores the column name but NOT the
        /// original plaintext codes. Any enrollment code issued before the rollback stays unusable
        /// and a new one must be generated.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodePrefix",
                schema: "entegrasyon",
                table: "AgentEnrollments");

            migrationBuilder.RenameColumn(
                name: "CodeHash",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                newName: "Code");

            migrationBuilder.RenameIndex(
                name: "IX_AgentEnrollments_CodeHash",
                schema: "entegrasyon",
                table: "AgentEnrollments",
                newName: "IX_AgentEnrollments_Code");
        }
    }
}
