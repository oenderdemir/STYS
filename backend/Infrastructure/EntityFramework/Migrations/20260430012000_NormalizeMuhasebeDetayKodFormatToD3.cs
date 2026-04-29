using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STYS.Infrastructure.EntityFramework;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    [DbContext(typeof(StysAppDbContext))]
    [Migration("20260430012000_NormalizeMuhasebeDetayKodFormatToD3")]
    public partial class NormalizeMuhasebeDetayKodFormatToD3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

-- CariKartlar
UPDATE ck
SET ck.CariKodu = CONCAT(ck.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(ck.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    ck.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[CariKartlar] ck
WHERE ck.IsDeleted = 0
  AND ck.MuhasebeHesapPlaniId IS NOT NULL
  AND ck.AnaMuhasebeHesapKodu IS NOT NULL
  AND ck.MuhasebeHesapSiraNo IS NOT NULL
  AND ck.CariKodu <> CONCAT(ck.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(ck.MuhasebeHesapSiraNo as nvarchar(16)), 3));

-- KasaBankaHesaplari
UPDATE kb
SET kb.Kod = CONCAT(kb.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(kb.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    kb.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[KasaBankaHesaplari] kb
WHERE kb.IsDeleted = 0
  AND kb.MuhasebeHesapPlaniId IS NOT NULL
  AND kb.AnaMuhasebeHesapKodu IS NOT NULL
  AND kb.MuhasebeHesapSiraNo IS NOT NULL
  AND kb.Kod <> CONCAT(kb.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(kb.MuhasebeHesapSiraNo as nvarchar(16)), 3));

-- Depolar
UPDATE d
SET d.Kod = CONCAT(d.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(d.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    d.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[Depolar] d
WHERE d.IsDeleted = 0
  AND d.MuhasebeHesapPlaniId IS NOT NULL
  AND d.AnaMuhasebeHesapKodu IS NOT NULL
  AND d.MuhasebeHesapSiraNo IS NOT NULL
  AND d.Kod <> CONCAT(d.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(d.MuhasebeHesapSiraNo as nvarchar(16)), 3));

-- TasinirKartlar
UPDATE tk
SET tk.StokKodu = CONCAT(tk.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(tk.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    tk.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[TasinirKartlar] tk
WHERE tk.IsDeleted = 0
  AND tk.MuhasebeHesapPlaniId IS NOT NULL
  AND tk.AnaMuhasebeHesapKodu IS NOT NULL
  AND tk.MuhasebeHesapSiraNo IS NOT NULL
  AND tk.StokKodu <> CONCAT(tk.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(tk.MuhasebeHesapSiraNo as nvarchar(16)), 3));

-- MuhasebeHesapPlanlari (ilgili operasyonal kayitlarla bagli olanlar)
UPDATE mhp
SET mhp.Kod = CONCAT(src.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(src.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    mhp.TamKod = CONCAT(src.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(src.MuhasebeHesapSiraNo as nvarchar(16)), 3)),
    mhp.UpdatedAt = SYSUTCDATETIME()
FROM [muhasebe].[MuhasebeHesapPlanlari] mhp
INNER JOIN
(
    SELECT DISTINCT ck.MuhasebeHesapPlaniId as HesapId, ck.AnaMuhasebeHesapKodu, ck.MuhasebeHesapSiraNo
    FROM [muhasebe].[CariKartlar] ck
    WHERE ck.IsDeleted = 0
      AND ck.MuhasebeHesapPlaniId IS NOT NULL
      AND ck.AnaMuhasebeHesapKodu IS NOT NULL
      AND ck.MuhasebeHesapSiraNo IS NOT NULL

    UNION

    SELECT DISTINCT kb.MuhasebeHesapPlaniId, kb.AnaMuhasebeHesapKodu, kb.MuhasebeHesapSiraNo
    FROM [muhasebe].[KasaBankaHesaplari] kb
    WHERE kb.IsDeleted = 0
      AND kb.MuhasebeHesapPlaniId IS NOT NULL
      AND kb.AnaMuhasebeHesapKodu IS NOT NULL
      AND kb.MuhasebeHesapSiraNo IS NOT NULL

    UNION

    SELECT DISTINCT d.MuhasebeHesapPlaniId, d.AnaMuhasebeHesapKodu, d.MuhasebeHesapSiraNo
    FROM [muhasebe].[Depolar] d
    WHERE d.IsDeleted = 0
      AND d.MuhasebeHesapPlaniId IS NOT NULL
      AND d.AnaMuhasebeHesapKodu IS NOT NULL
      AND d.MuhasebeHesapSiraNo IS NOT NULL

    UNION

    SELECT DISTINCT tk.MuhasebeHesapPlaniId, tk.AnaMuhasebeHesapKodu, tk.MuhasebeHesapSiraNo
    FROM [muhasebe].[TasinirKartlar] tk
    WHERE tk.IsDeleted = 0
      AND tk.MuhasebeHesapPlaniId IS NOT NULL
      AND tk.AnaMuhasebeHesapKodu IS NOT NULL
      AND tk.MuhasebeHesapSiraNo IS NOT NULL
) src ON src.HesapId = mhp.Id
WHERE mhp.IsDeleted = 0
  AND (mhp.Kod <> CONCAT(src.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(src.MuhasebeHesapSiraNo as nvarchar(16)), 3))
    OR mhp.TamKod <> CONCAT(src.AnaMuhasebeHesapKodu, N'.', RIGHT(N'000' + CAST(src.MuhasebeHesapSiraNo as nvarchar(16)), 3)));
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // no-op
        }
    }
}
