using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <summary>
    /// PAVO'ya ozel tablo ve kolonlari saglayici bagimsiz POS adlarina tasir.
    /// Rename/alter kullanilmasinin nedeni mevcut terminal ve odeme kayitlarini korumaktir.
    /// </summary>
    public partial class GeneralizePosIntegration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropPavoConstraintsAndIndexes(migrationBuilder);

            migrationBuilder.RenameTable(
                name: "PavoTerminaller",
                schema: "entegrasyon",
                newName: "PosTerminaller",
                newSchema: "entegrasyon");
            migrationBuilder.RenameTable(
                name: "PavoOdemeIslemleri",
                schema: "entegrasyon",
                newName: "PosOdemeIslemleri",
                newSchema: "entegrasyon");

            migrationBuilder.RenameColumn(
                name: "PavoOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                newName: "PosOdemeIslemiId");
            migrationBuilder.RenameColumn(
                name: "PavoTerminalId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "PosTerminalId");
            migrationBuilder.RenameColumn(
                name: "PaymentLinkReference",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "IslemReferansi");
            migrationBuilder.RenameColumn(
                name: "PaymentLinkId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "SaglayiciIslemId");
            migrationBuilder.RenameColumn(
                name: "SonPavoYaniti",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "SonSaglayiciYaniti");

            migrationBuilder.AddColumn<string>(
                name: "SaglayiciKodu",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PAVO");
            migrationBuilder.AlterColumn<string>(
                name: "SourceFingerprint",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
            migrationBuilder.AlterColumn<string>(
                name: "SaglayiciIslemId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            AddPosConstraintsAndIndexes(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropPosConstraintsAndIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<long>(
                name: "SaglayiciIslemId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);
            migrationBuilder.AlterColumn<string>(
                name: "SourceFingerprint",
                schema: "entegrasyon",
                table: "PosTerminaller",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);
            migrationBuilder.DropColumn(
                name: "SaglayiciKodu",
                schema: "entegrasyon",
                table: "PosTerminaller");

            migrationBuilder.RenameColumn(
                name: "PosOdemeIslemiId",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                newName: "PavoOdemeIslemiId");
            migrationBuilder.RenameColumn(
                name: "PosTerminalId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "PavoTerminalId");
            migrationBuilder.RenameColumn(
                name: "IslemReferansi",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "PaymentLinkReference");
            migrationBuilder.RenameColumn(
                name: "SaglayiciIslemId",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "PaymentLinkId");
            migrationBuilder.RenameColumn(
                name: "SonSaglayiciYaniti",
                schema: "entegrasyon",
                table: "PosOdemeIslemleri",
                newName: "SonPavoYaniti");

            migrationBuilder.RenameTable(
                name: "PosOdemeIslemleri",
                schema: "entegrasyon",
                newName: "PavoOdemeIslemleri",
                newSchema: "entegrasyon");
            migrationBuilder.RenameTable(
                name: "PosTerminaller",
                schema: "entegrasyon",
                newName: "PavoTerminaller",
                newSchema: "entegrasyon");

            AddPavoConstraintsAndIndexes(migrationBuilder);
        }

        private static void DropPavoConstraintsAndIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_RezervasyonOdemeler_PavoOdemeIslemleri_PavoOdemeIslemiId", "RezervasyonOdemeler", schema: "dbo");
            migrationBuilder.DropForeignKey("FK_PavoOdemeIslemleri_KasaBankaHesaplari_KasaBankaHesapId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PavoOdemeIslemleri_PavoTerminaller_PavoTerminalId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PavoOdemeIslemleri_RezervasyonOdemeler_RezervasyonOdemeId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PavoOdemeIslemleri_Rezervasyonlar_RezervasyonId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PavoTerminaller_KasaBankaHesaplari_KasaBankaHesapId", "PavoTerminaller", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PavoTerminaller_Tesisler_TesisId", "PavoTerminaller", schema: "entegrasyon");

            migrationBuilder.DropPrimaryKey("PK_PavoOdemeIslemleri", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropPrimaryKey("PK_PavoTerminaller", "PavoTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_RezervasyonOdemeler_PavoOdemeIslemiId", "RezervasyonOdemeler", schema: "dbo");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_KasaBankaHesapId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_KurumId_PaymentLinkReference", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_PavoTerminalId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_PaymentLinkId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_RezervasyonId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_RezervasyonOdemeId", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoOdemeIslemleri_TesisId_Durum", "PavoOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoTerminaller_KasaBankaHesapId", "PavoTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoTerminaller_KurumId_SerialNumber", "PavoTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PavoTerminaller_TesisId_KasaBankaHesapId_AktifMi", "PavoTerminaller", schema: "entegrasyon");
        }

        private static void AddPosConstraintsAndIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey("PK_PosTerminaller", "PosTerminaller", schema: "entegrasyon", column: "Id");
            migrationBuilder.AddPrimaryKey("PK_PosOdemeIslemleri", "PosOdemeIslemleri", schema: "entegrasyon", column: "Id");
            migrationBuilder.CreateIndex("IX_RezervasyonOdemeler_PosOdemeIslemiId", "RezervasyonOdemeler", "PosOdemeIslemiId", schema: "dbo", unique: true, filter: "[PosOdemeIslemiId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_KasaBankaHesapId", "PosOdemeIslemleri", "KasaBankaHesapId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_KurumId_IslemReferansi", "PosOdemeIslemleri", new[] { "KurumId", "IslemReferansi" }, schema: "entegrasyon", unique: true);
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_PosTerminalId", "PosOdemeIslemleri", "PosTerminalId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_RezervasyonId", "PosOdemeIslemleri", "RezervasyonId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_RezervasyonOdemeId", "PosOdemeIslemleri", "RezervasyonOdemeId", schema: "entegrasyon", unique: true, filter: "[RezervasyonOdemeId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_SaglayiciIslemId", "PosOdemeIslemleri", "SaglayiciIslemId", schema: "entegrasyon", filter: "[SaglayiciIslemId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PosOdemeIslemleri_TesisId_Durum", "PosOdemeIslemleri", new[] { "TesisId", "Durum" }, schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PosTerminaller_KasaBankaHesapId", "PosTerminaller", "KasaBankaHesapId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PosTerminaller_KurumId_SaglayiciKodu_SerialNumber", "PosTerminaller", new[] { "KurumId", "SaglayiciKodu", "SerialNumber" }, schema: "entegrasyon", unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex("IX_PosTerminaller_TesisId_KasaBankaHesapId_AktifMi", "PosTerminaller", new[] { "TesisId", "KasaBankaHesapId", "AktifMi" }, schema: "entegrasyon", filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey("FK_PosTerminaller_KasaBankaHesaplari_KasaBankaHesapId", "PosTerminaller", "KasaBankaHesapId", "KasaBankaHesaplari", principalSchema: "muhasebe", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PosTerminaller_Tesisler_TesisId", "PosTerminaller", "TesisId", "Tesisler", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PosOdemeIslemleri_KasaBankaHesaplari_KasaBankaHesapId", "PosOdemeIslemleri", "KasaBankaHesapId", "KasaBankaHesaplari", principalSchema: "muhasebe", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PosOdemeIslemleri_PosTerminaller_PosTerminalId", "PosOdemeIslemleri", "PosTerminalId", "PosTerminaller", principalSchema: "entegrasyon", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PosOdemeIslemleri_RezervasyonOdemeler_RezervasyonOdemeId", "PosOdemeIslemleri", "RezervasyonOdemeId", "RezervasyonOdemeler", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId", "PosOdemeIslemleri", "RezervasyonId", "Rezervasyonlar", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_RezervasyonOdemeler_PosOdemeIslemleri_PosOdemeIslemiId", "RezervasyonOdemeler", "PosOdemeIslemiId", "PosOdemeIslemleri", principalSchema: "entegrasyon", schema: "dbo", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        private static void DropPosConstraintsAndIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_RezervasyonOdemeler_PosOdemeIslemleri_PosOdemeIslemiId", "RezervasyonOdemeler", schema: "dbo");
            migrationBuilder.DropForeignKey("FK_PosOdemeIslemleri_KasaBankaHesaplari_KasaBankaHesapId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PosOdemeIslemleri_PosTerminaller_PosTerminalId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PosOdemeIslemleri_RezervasyonOdemeler_RezervasyonOdemeId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PosOdemeIslemleri_Rezervasyonlar_RezervasyonId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PosTerminaller_KasaBankaHesaplari_KasaBankaHesapId", "PosTerminaller", schema: "entegrasyon");
            migrationBuilder.DropForeignKey("FK_PosTerminaller_Tesisler_TesisId", "PosTerminaller", schema: "entegrasyon");

            migrationBuilder.DropPrimaryKey("PK_PosOdemeIslemleri", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropPrimaryKey("PK_PosTerminaller", "PosTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_RezervasyonOdemeler_PosOdemeIslemiId", "RezervasyonOdemeler", schema: "dbo");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_KasaBankaHesapId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_KurumId_IslemReferansi", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_PosTerminalId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_RezervasyonId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_RezervasyonOdemeId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_SaglayiciIslemId", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosOdemeIslemleri_TesisId_Durum", "PosOdemeIslemleri", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosTerminaller_KasaBankaHesapId", "PosTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosTerminaller_KurumId_SaglayiciKodu_SerialNumber", "PosTerminaller", schema: "entegrasyon");
            migrationBuilder.DropIndex("IX_PosTerminaller_TesisId_KasaBankaHesapId_AktifMi", "PosTerminaller", schema: "entegrasyon");
        }

        private static void AddPavoConstraintsAndIndexes(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddPrimaryKey("PK_PavoTerminaller", "PavoTerminaller", schema: "entegrasyon", column: "Id");
            migrationBuilder.AddPrimaryKey("PK_PavoOdemeIslemleri", "PavoOdemeIslemleri", schema: "entegrasyon", column: "Id");
            migrationBuilder.CreateIndex("IX_RezervasyonOdemeler_PavoOdemeIslemiId", "RezervasyonOdemeler", "PavoOdemeIslemiId", schema: "dbo", unique: true, filter: "[PavoOdemeIslemiId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_KasaBankaHesapId", "PavoOdemeIslemleri", "KasaBankaHesapId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_KurumId_PaymentLinkReference", "PavoOdemeIslemleri", new[] { "KurumId", "PaymentLinkReference" }, schema: "entegrasyon", unique: true);
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_PavoTerminalId", "PavoOdemeIslemleri", "PavoTerminalId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_PaymentLinkId", "PavoOdemeIslemleri", "PaymentLinkId", schema: "entegrasyon", filter: "[PaymentLinkId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_RezervasyonId", "PavoOdemeIslemleri", "RezervasyonId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_RezervasyonOdemeId", "PavoOdemeIslemleri", "RezervasyonOdemeId", schema: "entegrasyon", unique: true, filter: "[RezervasyonOdemeId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_PavoOdemeIslemleri_TesisId_Durum", "PavoOdemeIslemleri", new[] { "TesisId", "Durum" }, schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PavoTerminaller_KasaBankaHesapId", "PavoTerminaller", "KasaBankaHesapId", schema: "entegrasyon");
            migrationBuilder.CreateIndex("IX_PavoTerminaller_KurumId_SerialNumber", "PavoTerminaller", new[] { "KurumId", "SerialNumber" }, schema: "entegrasyon", unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex("IX_PavoTerminaller_TesisId_KasaBankaHesapId_AktifMi", "PavoTerminaller", new[] { "TesisId", "KasaBankaHesapId", "AktifMi" }, schema: "entegrasyon", filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey("FK_PavoTerminaller_KasaBankaHesaplari_KasaBankaHesapId", "PavoTerminaller", "KasaBankaHesapId", "KasaBankaHesaplari", principalSchema: "muhasebe", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PavoTerminaller_Tesisler_TesisId", "PavoTerminaller", "TesisId", "Tesisler", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PavoOdemeIslemleri_KasaBankaHesaplari_KasaBankaHesapId", "PavoOdemeIslemleri", "KasaBankaHesapId", "KasaBankaHesaplari", principalSchema: "muhasebe", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PavoOdemeIslemleri_PavoTerminaller_PavoTerminalId", "PavoOdemeIslemleri", "PavoTerminalId", "PavoTerminaller", principalSchema: "entegrasyon", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PavoOdemeIslemleri_RezervasyonOdemeler_RezervasyonOdemeId", "PavoOdemeIslemleri", "RezervasyonOdemeId", "RezervasyonOdemeler", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_PavoOdemeIslemleri_Rezervasyonlar_RezervasyonId", "PavoOdemeIslemleri", "RezervasyonId", "Rezervasyonlar", principalSchema: "dbo", schema: "entegrasyon", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("FK_RezervasyonOdemeler_PavoOdemeIslemleri_PavoOdemeIslemiId", "RezervasyonOdemeler", "PavoOdemeIslemiId", "PavoOdemeIslemleri", principalSchema: "entegrasyon", schema: "dbo", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
