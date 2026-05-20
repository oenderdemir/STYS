using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260419174000_BackfillTasinirKodParentIds")]
public partial class BackfillTasinirKodParentIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ;WITH Candidates AS
            (
                SELECT
                    c.Id AS ChildId,
                    p.Id AS ParentId,
                    c.Kod,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY c.Id
                        ORDER BY LEN(p.TamKod) DESC, p.Id DESC
                    ) AS RN
                FROM [muhasebe].[TasinirKodlar] c
                INNER JOIN [muhasebe].[TasinirKodlar] p
                    ON p.IsDeleted = 0
                   AND c.IsDeleted = 0
                   AND c.Id <> p.Id
                   AND c.TamKod LIKE p.TamKod + N'.%'
                WHERE c.UstKodId IS NULL
            ),
            Deduped AS
            (
                SELECT
                    ChildId,
                    ParentId,
                    ROW_NUMBER() OVER (PARTITION BY ParentId, Kod ORDER BY ChildId) AS DupRN
                FROM Candidates
                WHERE RN = 1
            ),
            -- Exclude children whose (ParentId, Kod) would conflict with
            -- a row that already has UstKodId set (from an earlier migration or seed)
            ConflictCheck AS
            (
                SELECT DISTINCT d.ChildId
                FROM Deduped d
                INNER JOIN [muhasebe].[TasinirKodlar] ex
                    ON ex.UstKodId = d.ParentId
                   AND ex.Kod = d.Kod
                   AND ex.Id != d.ChildId
                   AND ex.IsDeleted = 0
            )
            UPDATE c
            SET
                c.UstKodId = x.ParentId,
                c.UpdatedAt = SYSUTCDATETIME(),
                c.UpdatedBy = N'migration_backfill_tasinir_parent'
            FROM [muhasebe].[TasinirKodlar] c
            INNER JOIN Deduped x ON x.ChildId = c.Id AND x.DupRN = 1
            LEFT JOIN ConflictCheck cf ON cf.ChildId = c.Id
            WHERE c.IsDeleted = 0
              AND c.UstKodId IS NULL
              AND cf.ChildId IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE [muhasebe].[TasinirKodlar]
            SET
                UstKodId = NULL,
                UpdatedAt = SYSUTCDATETIME(),
                UpdatedBy = N'migration_backfill_tasinir_parent_rollback'
            WHERE UpdatedBy = N'migration_backfill_tasinir_parent';
            """);
    }
}
