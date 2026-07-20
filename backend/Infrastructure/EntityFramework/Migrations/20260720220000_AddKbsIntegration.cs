using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations;

[DbContext(typeof(StysAppDbContext))]
[Migration("20260720220000_AddKbsIntegration")]
public partial class AddKbsIntegration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "Ad", "Soyad", "DogumYeri" }) migrationBuilder.AddColumn<string>(name: column, table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(100)", maxLength: 100, nullable: true);
        foreach (var column in new[] { "KimlikTuru" }) migrationBuilder.AddColumn<string>(name: column, table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(24)", maxLength: 24, nullable: true);
        foreach (var column in new[] { "KimlikNo", "BelgeNo", "BelgeTuru", "Telefon" }) migrationBuilder.AddColumn<string>(name: column, table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(32)", maxLength: 32, nullable: true);
        migrationBuilder.AddColumn<string>(name: "UyrukKodu", table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(8)", maxLength: 8, nullable: true);
        migrationBuilder.AddColumn<string>(name: "AracPlakasi", table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(16)", maxLength: 16, nullable: true);
        migrationBuilder.AddColumn<string>(name: "KonaklamaKullanimSekli", table: "RezervasyonKonaklayanlar", schema: "dbo", type: "nvarchar(16)", maxLength: 16, nullable: true);
        foreach (var column in new[] { "DogumTarihi", "FiiliGirisTarihi", "FiiliCikisTarihi" }) migrationBuilder.AddColumn<DateTime>(name: column, table: "RezervasyonKonaklayanlar", schema: "dbo", type: "datetime2", nullable: true);

        migrationBuilder.CreateTable(name: "KbsTesisAyarlari", schema: "dbo", columns: table => new
        {
            Id = table.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), KurumId = table.Column<int>("int", nullable: false), TesisId = table.Column<int>("int", nullable: false),
            KollukSistemi = table.Column<string>("nvarchar(16)", maxLength: 16, nullable: false), EntegrasyonTipi = table.Column<string>("nvarchar(16)", maxLength: 16, nullable: false), TesisKodu = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: true), SecretReference = table.Column<string>("nvarchar(256)", maxLength: 256, nullable: true),
            AktifMi = table.Column<bool>("bit", nullable: false), CanliGonderimAktifMi = table.Column<bool>("bit", nullable: false), SonBaglantiKontrolTarihi = table.Column<DateTime>("datetime2", nullable: true), SonBaglantiKontrolSonucu = table.Column<string>("nvarchar(512)", maxLength: 512, nullable: true),
            IsDeleted = table.Column<bool>("bit", nullable: false), CreatedAt = table.Column<DateTime>("datetime2", nullable: true), UpdatedAt = table.Column<DateTime>("datetime2", nullable: true), DeletedAt = table.Column<DateTime>("datetime2", nullable: true), CreatedBy = table.Column<string>("nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true), DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_KbsTesisAyarlari", x => x.Id); table.ForeignKey("FK_KbsTesisAyarlari_Tesisler_TesisId", x => x.TesisId, "dbo", "Tesisler", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateTable(name: "KbsBildirimler", schema: "dbo", columns: table => new
        {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"), KurumId = table.Column<int>("int", nullable: false), TesisId = table.Column<int>("int", nullable: false), RezervasyonId = table.Column<int>("int", nullable: false), RezervasyonKonaklayanId = table.Column<int>("int", nullable: false),
            BildirimTipi = table.Column<string>("nvarchar(24)", maxLength: 24, nullable: false), Saglayici = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false), Durum = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false), IdempotencyKey = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: false), OlayAnahtari = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false), PayloadVersion = table.Column<string>("nvarchar(16)", maxLength: 16, nullable: false), PayloadHash = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false), DenemeSayisi = table.Column<int>("int", nullable: false), SonrakiDenemeTarihi = table.Column<DateTime>("datetime2", nullable: true), SonHataKodu = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: true), SonHataMesaji = table.Column<string>("nvarchar(512)", maxLength: 512, nullable: true), GonderimTarihi = table.Column<DateTime>("datetime2", nullable: true), TamamlanmaTarihi = table.Column<DateTime>("datetime2", nullable: true), ExcelManifestHash = table.Column<string>("nvarchar(128)", maxLength: 128, nullable: true), RowVersion = table.Column<byte[]>("rowversion", rowVersion: true, nullable: false),
            IsDeleted = table.Column<bool>("bit", nullable: false), CreatedAt = table.Column<DateTime>("datetime2", nullable: true), UpdatedAt = table.Column<DateTime>("datetime2", nullable: true), DeletedAt = table.Column<DateTime>("datetime2", nullable: true), CreatedBy = table.Column<string>("nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true), DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_KbsBildirimler", x => x.Id); table.ForeignKey("FK_KbsBildirimler_Rezervasyonlar_RezervasyonId", x => x.RezervasyonId, "dbo", "Rezervasyonlar", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_KbsBildirimler_RezervasyonKonaklayanlar_RezervasyonKonaklayanId", x => x.RezervasyonKonaklayanId, "dbo", "RezervasyonKonaklayanlar", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateTable(name: "KbsBildirimDenemeleri", schema: "dbo", columns: table => new
        {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"), KurumId = table.Column<int>("int", nullable: false), KbsBildirimId = table.Column<long>("bigint", nullable: false), DenemeTarihi = table.Column<DateTime>("datetime2", nullable: false), Sonuc = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: false), HataSinifi = table.Column<string>("nvarchar(32)", maxLength: 32, nullable: true), SaglayiciHataKodu = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: true), MaskelenmisAciklama = table.Column<string>("nvarchar(512)", maxLength: 512, nullable: true),
            IsDeleted = table.Column<bool>("bit", nullable: false), CreatedAt = table.Column<DateTime>("datetime2", nullable: true), UpdatedAt = table.Column<DateTime>("datetime2", nullable: true), DeletedAt = table.Column<DateTime>("datetime2", nullable: true), CreatedBy = table.Column<string>("nvarchar(max)", nullable: true), UpdatedBy = table.Column<string>("nvarchar(max)", nullable: true), DeletedBy = table.Column<string>("nvarchar(max)", nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_KbsBildirimDenemeleri", x => x.Id); table.ForeignKey("FK_KbsBildirimDenemeleri_KbsBildirimler_KbsBildirimId", x => x.KbsBildirimId, "dbo", "KbsBildirimler", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateIndex("IX_KbsTesisAyarlari_KurumId_TesisId", "KbsTesisAyarlari", new[] { "KurumId", "TesisId" }, "dbo", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_KbsTesisAyarlari_TesisId", "KbsTesisAyarlari", "TesisId", "dbo");
        migrationBuilder.CreateIndex("IX_KbsBildirimler_IdempotencyKey", "KbsBildirimler", "IdempotencyKey", "dbo", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_KbsBildirimler_KurumId_RezervasyonKonaklayanId_BildirimTipi_OlayAnahtari", "KbsBildirimler", new[] { "KurumId", "RezervasyonKonaklayanId", "BildirimTipi", "OlayAnahtari" }, "dbo", unique: true, filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_KbsBildirimler_Durum_SonrakiDenemeTarihi", "KbsBildirimler", new[] { "Durum", "SonrakiDenemeTarihi" }, "dbo", filter: "[IsDeleted] = 0");
        migrationBuilder.CreateIndex("IX_KbsBildirimler_RezervasyonId", "KbsBildirimler", "RezervasyonId", "dbo"); migrationBuilder.CreateIndex("IX_KbsBildirimler_RezervasyonKonaklayanId", "KbsBildirimler", "RezervasyonKonaklayanId", "dbo");
        migrationBuilder.CreateIndex("IX_KbsBildirimDenemeleri_KbsBildirimId_DenemeTarihi", "KbsBildirimDenemeleri", new[] { "KbsBildirimId", "DenemeTarihi" }, "dbo");

        migrationBuilder.Sql("""
            DECLARE @Now datetime2=SYSUTCDATETIME(), @Admin uniqueidentifier='22222222-2222-2222-2222-222222222201', @Menu uniqueidentifier='7b510000-0000-0000-0000-000000000001';
            DECLARE @Roles TABLE(Id uniqueidentifier, Name nvarchar(64)); INSERT @Roles VALUES
            ('7b510000-0000-0000-0000-000000000011','Menu'),('7b510000-0000-0000-0000-000000000012','View'),('7b510000-0000-0000-0000-000000000013','Manage'),('7b510000-0000-0000-0000-000000000014','Retry'),('7b510000-0000-0000-0000-000000000015','Settings'),('7b510000-0000-0000-0000-000000000016','SensitiveDataView');
            INSERT [TODBase].[Roles](Id,Name,Domain,IsDeleted,CreatedAt,UpdatedAt) SELECT Id,Name,'KbsYonetimi',0,@Now,@Now FROM @Roles r WHERE NOT EXISTS(SELECT 1 FROM [TODBase].[Roles] x WHERE x.Id=r.Id);
            IF EXISTS(SELECT 1 FROM [TODBase].[UserGroups] WHERE Id=@Admin) INSERT [TODBase].[UserGroupRoles](Id,UserGroupId,RoleId,IsDeleted,CreatedAt,UpdatedAt) SELECT NEWID(),@Admin,r.Id,0,@Now,@Now FROM @Roles r WHERE NOT EXISTS(SELECT 1 FROM [TODBase].[UserGroupRoles] x WHERE x.UserGroupId=@Admin AND x.RoleId=r.Id);
            IF NOT EXISTS(SELECT 1 FROM [TODBase].[MenuItems] WHERE Id=@Menu) INSERT [TODBase].[MenuItems](Id,Label,Icon,Route,QueryParams,ParentId,MenuOrder,IsDeleted,CreatedAt,UpdatedAt) VALUES(@Menu,N'KBS Bildirim Merkezi',N'fa-solid fa-building-shield',N'kbs-bildirim-merkezi',NULL,'66666666-6666-6666-6666-666666666601',26,0,@Now,@Now);
            IF NOT EXISTS(SELECT 1 FROM [TODBase].[MenuItemRoles] WHERE MenuItemId=@Menu AND RoleId='7b510000-0000-0000-0000-000000000011') INSERT [TODBase].[MenuItemRoles](Id,MenuItemId,RoleId,IsDeleted,CreatedAt,UpdatedAt) VALUES(NEWID(),@Menu,'7b510000-0000-0000-0000-000000000011',0,@Now,@Now);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("KbsBildirimDenemeleri", "dbo"); migrationBuilder.DropTable("KbsBildirimler", "dbo"); migrationBuilder.DropTable("KbsTesisAyarlari", "dbo");
        foreach (var column in new[] { "Ad", "Soyad", "DogumYeri", "KimlikTuru", "KimlikNo", "BelgeNo", "BelgeTuru", "Telefon", "UyrukKodu", "AracPlakasi", "KonaklamaKullanimSekli", "DogumTarihi", "FiiliGirisTarihi", "FiiliCikisTarihi" }) migrationBuilder.DropColumn(column, "RezervasyonKonaklayanlar", "dbo");
        migrationBuilder.Sql("DELETE FROM [TODBase].[MenuItemRoles] WHERE MenuItemId='7b510000-0000-0000-0000-000000000001'; DELETE FROM [TODBase].[MenuItems] WHERE Id='7b510000-0000-0000-0000-000000000001'; DELETE FROM [TODBase].[UserGroupRoles] WHERE RoleId IN (SELECT Id FROM [TODBase].[Roles] WHERE Domain='KbsYonetimi'); DELETE FROM [TODBase].[Roles] WHERE Domain='KbsYonetimi';");
    }
}
