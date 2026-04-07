using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddKampIadePolitikaParametreleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @Now datetime2 = SYSUTCDATETIME();
                INSERT INTO [dbo].[KampParametreleri] ([Kod], [Deger], [Aciklama], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
                SELECT v.[Kod], v.[Deger], v.[Aciklama], 0, @Now, @Now, N'system', N'system'
                FROM (VALUES
                  (N'VazgecmeIadeGunSayisi',            N'7',    N'Tam iade icin kamp baslangicindan kac gun once vazgecilmesi gerekir'),
                  (N'GecBildirimGunlukKesintiyUzdesi',  N'0.05', N'Gec bildirimde gunluk kesinti yuzdesi (ornegin 0.05 = yuzde 5)'),
                  (N'NoShowSuresiGun',                  N'2',    N'Kamp basladiktan kac gun sonra no-show iptali yapilabilir'),
                  (N'YilAraligiBas',                    N'2000', N'Kamp yili icin gecerli aralik baslangici'),
                  (N'YilAraligiBit',                    N'2100', N'Kamp yili icin gecerli aralik bitisi')
                ) AS v([Kod], [Deger], [Aciklama])
                WHERE NOT EXISTS (
                  SELECT 1 FROM [dbo].[KampParametreleri] p
                  WHERE p.[Kod] = v.[Kod] AND p.[IsDeleted] = 0
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [dbo].[KampParametreleri]
                WHERE [Kod] IN (
                  N'VazgecmeIadeGunSayisi',
                  N'GecBildirimGunlukKesintiyUzdesi',
                  N'NoShowSuresiGun',
                  N'YilAraligiBas',
                  N'YilAraligiBit'
                ) AND [CreatedBy] = N'system';
                """);
        }
    }
}
