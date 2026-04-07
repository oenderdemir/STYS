using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSnapshotForTarifeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Bildirimler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KaynakUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mesaj = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Link = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    KaynakUserAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bildirimler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BildirimTercihleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BildirimlerAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    MinimumSeverity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IzinliTiplerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IzinliKaynaklarJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BildirimTercihleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalEkHizmetTanimlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BirimAdi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaketIcerikHizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalEkHizmetTanimlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Iller",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Iller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsletmeAlaniSiniflari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeAlaniSiniflari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampAkrabalikTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    YakindanDogrulanabilirMi = table.Column<bool>(type: "bit", nullable: false),
                    BasvuruSahibiAkrabaligiMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampAkrabalikTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruSahibiTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                    TabanPuan = table.Column<int>(type: "int", nullable: false),
                    HizmetYiliPuaniAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    EmekliBonusPuani = table.Column<int>(type: "int", nullable: false),
                    VarsayilanKatilimciTipiKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruSahibiTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruSahipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BasvuruSahibiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HizmetYili = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruSahipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampKatilimciTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    KamuTarifesiUygulanirMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampKatilimciTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampParametreleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Deger = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampParametreleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampProgramlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampProgramlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KampYasUcretKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UcretsizCocukMaxYas = table.Column<int>(type: "int", nullable: false),
                    YarimUcretliCocukMaxYas = table.Column<int>(type: "int", nullable: false),
                    YemekOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampYasUcretKurallari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonaklamaTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MisafirTipleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdaOzellikleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VeriTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdaOzellikleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdaSiniflari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdaSiniflari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tesisler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IlId = table.Column<int>(type: "int", nullable: false),
                    Telefon = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Adres = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Eposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GirisSaati = table.Column<TimeSpan>(type: "time(0)", nullable: false, defaultValue: new TimeSpan(0, 14, 0, 0, 0)),
                    CikisSaati = table.Column<TimeSpan>(type: "time(0)", nullable: false, defaultValue: new TimeSpan(0, 10, 0, 0, 0)),
                    EkHizmetPaketCakismaPolitikasi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "OnayIste"),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tesisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tesisler_Iller_IlId",
                        column: x => x.IlId,
                        principalSchema: "dbo",
                        principalTable: "Iller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampDonemleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    BasvuruBaslangicTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    BasvuruBitisTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    KonaklamaBaslangicTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    KonaklamaBitisTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    MinimumGece = table.Column<int>(type: "int", nullable: false),
                    MaksimumGece = table.Column<int>(type: "int", nullable: false),
                    OnayGerektirirMi = table.Column<bool>(type: "bit", nullable: false),
                    CekilisGerekliMi = table.Column<bool>(type: "bit", nullable: false),
                    AyniAileIcinTekBasvuruMu = table.Column<bool>(type: "bit", nullable: false),
                    IptalSonGun = table.Column<DateTime>(type: "date", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampDonemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampDonemleri_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampKonaklamaTarifeleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MinimumKisi = table.Column<int>(type: "int", nullable: false),
                    MaksimumKisi = table.Column<int>(type: "int", nullable: false),
                    KamuGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DigerGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BuzdolabiGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TelevizyonGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KlimaGunlukUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampKonaklamaTarifeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampKonaklamaTarifeleri_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampKuralSetleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    KampYili = table.Column<int>(type: "int", nullable: false),
                    OncekiYilSayisi = table.Column<int>(type: "int", nullable: false),
                    KatilimCezaPuani = table.Column<int>(type: "int", nullable: false),
                    KatilimciBasinaPuan = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampKuralSetleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampKuralSetleri_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampProgramiBasvuruSahibiTipKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    KampBasvuruSahibiTipiId = table.Column<int>(type: "int", nullable: false),
                    OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                    TabanPuan = table.Column<int>(type: "int", nullable: false),
                    HizmetYiliPuaniAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    EmekliBonusPuani = table.Column<int>(type: "int", nullable: false),
                    VarsayilanKatilimciTipiKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampProgramiBasvuruSahibiTipKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampProgramiBasvuruSahibiTipKurallari_KampBasvuruSahibiTipleri_KampBasvuruSahibiTipiId",
                        column: x => x.KampBasvuruSahibiTipiId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvuruSahibiTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampProgramiBasvuruSahibiTipKurallari_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampProgramiParametreAyarlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampProgramiId = table.Column<int>(type: "int", nullable: false),
                    KamuAvansKisiBasi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DigerAvansKisiBasi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    VazgecmeIadeGunSayisi = table.Column<int>(type: "int", nullable: true),
                    GecBildirimGunlukKesintiyUzdesi = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    NoShowSuresiGun = table.Column<int>(type: "int", nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampProgramiParametreAyarlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampProgramiParametreAyarlari_KampProgramlari_KampProgramiId",
                        column: x => x.KampProgramiId,
                        principalSchema: "dbo",
                        principalTable: "KampProgramlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KonaklamaTipiIcerikKalemleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    HizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimBaslangicSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    KullanimBitisSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckInGunuGecerliMi = table.Column<bool>(type: "bit", nullable: false),
                    CheckOutGunuGecerliMi = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KonaklamaTipiIcerikKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KonaklamaTipiIcerikKalemleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Binalar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KatSayisi = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Binalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Binalar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EkHizmetler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    GlobalEkHizmetTanimiId = table.Column<int>(type: "int", nullable: true),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BirimAdi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaketIcerikHizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkHizmetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkHizmetler_GlobalEkHizmetTanimlari_GlobalEkHizmetTanimiId",
                        column: x => x.GlobalEkHizmetTanimiId,
                        principalSchema: "dbo",
                        principalTable: "GlobalEkHizmetTanimlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkHizmetler_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndirimKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IndirimTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Deger = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KapsamTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Oncelik = table.Column<int>(type: "int", nullable: false),
                    BirlesebilirMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndirimKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKurallari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KullaniciTesisSahiplikleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KullaniciTesisSahiplikleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KullaniciTesisSahiplikleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rezervasyonlar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferansNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KisiSayisi = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: true),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: true),
                    GirisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CikisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TekKisilikFiyatUygulandiMi = table.Column<bool>(type: "bit", nullable: false),
                    ToplamBazUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamUcret = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UygulananIndirimlerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MisafirAdiSoyadi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MisafirTelefon = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MisafirEposta = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PasaportNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    MisafirCinsiyeti = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Notlar = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RezervasyonDurumu = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rezervasyonlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rezervasyonlar_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rezervasyonlar_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SezonKurallari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MinimumGece = table.Column<int>(type: "int", nullable: false),
                    StopSaleMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SezonKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SezonKurallari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisKonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisKonaklamaTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisMisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisMisafirTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisMisafirTipleri_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisMisafirTipleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisOdaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    OdaSinifiId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaylasimliMi = table.Column<bool>(type: "bit", nullable: false),
                    Kapasite = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisOdaTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipleri_OdaSiniflari_OdaSinifiId",
                        column: x => x.OdaSinifiId,
                        principalSchema: "dbo",
                        principalTable: "OdaSiniflari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisResepsiyonistleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisResepsiyonistleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisResepsiyonistleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisYoneticileri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisYoneticileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisYoneticileri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvurulari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaBirimiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BasvuruNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KampBasvuruSahibiId = table.Column<int>(type: "int", nullable: false),
                    BasvuruSahibiAdiSoyadiSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BasvuruSahibiTipiSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    HizmetYiliSnapshot = table.Column<int>(type: "int", nullable: false),
                    EvcilHayvanGetirecekMi = table.Column<bool>(type: "bit", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KatilimciSayisi = table.Column<int>(type: "int", nullable: false),
                    OncelikSirasi = table.Column<int>(type: "int", nullable: false),
                    Puan = table.Column<int>(type: "int", nullable: false),
                    GunlukToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DonemToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvansToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KalanOdemeTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UyariMesajlariJson = table.Column<string>(type: "nvarchar(max)", maxLength: 2048, nullable: true),
                    BuzdolabiTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
                    TelevizyonTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
                    KlimaTalepEdildiMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvurulari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvurulari_KampBasvuruSahipleri_KampBasvuruSahibiId",
                        column: x => x.KampBasvuruSahibiId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvuruSahipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvurulari_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvurulari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampDonemiTesisleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    BasvuruyaAcikMi = table.Column<bool>(type: "bit", nullable: false),
                    ToplamKontenjan = table.Column<int>(type: "int", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    KonaklamaTarifeKodlariJson = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampDonemiTesisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampDonemiTesisleri_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampDonemiTesisleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisKonaklamaTipiIcerikOverridelari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiIcerikKalemiId = table.Column<int>(type: "int", nullable: false),
                    DevreDisiMi = table.Column<bool>(type: "bit", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: true),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    KullanimBaslangicSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    KullanimBitisSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckInGunuGecerliMi = table.Column<bool>(type: "bit", nullable: true),
                    CheckOutGunuGecerliMi = table.Column<bool>(type: "bit", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisKonaklamaTipiIcerikOverridelari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipiIcerikOverridelari_KonaklamaTipiIcerikKalemleri_KonaklamaTipiIcerikKalemiId",
                        column: x => x.KonaklamaTipiIcerikKalemiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipiIcerikKalemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisKonaklamaTipiIcerikOverridelari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BinaYoneticileri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BinaId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinaYoneticileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BinaYoneticileri_Binalar_BinaId",
                        column: x => x.BinaId,
                        principalSchema: "dbo",
                        principalTable: "Binalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsletmeAlanlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BinaId = table.Column<int>(type: "int", nullable: false),
                    IsletmeAlaniSinifiId = table.Column<int>(type: "int", nullable: false),
                    OzelAd = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsletmeAlanlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsletmeAlanlari_Binalar_BinaId",
                        column: x => x.BinaId,
                        principalSchema: "dbo",
                        principalTable: "Binalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IsletmeAlanlari_IsletmeAlaniSiniflari_IsletmeAlaniSinifiId",
                        column: x => x.IsletmeAlaniSinifiId,
                        principalSchema: "dbo",
                        principalTable: "IsletmeAlaniSiniflari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EkHizmetTarifeleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    EkHizmetId = table.Column<int>(type: "int", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkHizmetTarifeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkHizmetTarifeleri_EkHizmetler_EkHizmetId",
                        column: x => x.EkHizmetId,
                        principalSchema: "dbo",
                        principalTable: "EkHizmetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkHizmetTarifeleri_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndirimKuraliKonaklamaTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndirimKuraliId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndirimKuraliKonaklamaTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliKonaklamaTipleri_IndirimKurallari_IndirimKuraliId",
                        column: x => x.IndirimKuraliId,
                        principalSchema: "dbo",
                        principalTable: "IndirimKurallari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliKonaklamaTipleri_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndirimKuraliMisafirTipleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndirimKuraliId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndirimKuraliMisafirTipleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliMisafirTipleri_IndirimKurallari_IndirimKuraliId",
                        column: x => x.IndirimKuraliId,
                        principalSchema: "dbo",
                        principalTable: "IndirimKurallari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IndirimKuraliMisafirTipleri_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonDegisiklikGecmisleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    IslemTipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OncekiDegerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YeniDegerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonDegisiklikGecmisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonDegisiklikGecmisleri_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklamaHaklari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    HizmetKodu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HizmetAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    Periyot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PeriyotAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimTipiAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimNoktasiAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KullanimBaslangicSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    KullanimBitisSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    CheckInGunuGecerliMi = table.Column<bool>(type: "bit", nullable: false),
                    CheckOutGunuGecerliMi = table.Column<bool>(type: "bit", nullable: false),
                    HakTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AciklamaSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonKonaklamaHaklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklamaHaklari_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklayanlar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PasaportNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Cinsiyet = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    KatilimDurumu = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Bekleniyor"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonKonaklayanlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanlar_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonOdemeler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OdemeTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    OdemeTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonOdemeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonOdemeler_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonSegmentleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    SegmentSirasi = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonSegmentleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentleri_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdaFiyatlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisOdaTipiId = table.Column<int>(type: "int", nullable: false),
                    KonaklamaTipiId = table.Column<int>(type: "int", nullable: false),
                    MisafirTipiId = table.Column<int>(type: "int", nullable: false),
                    KisiSayisi = table.Column<int>(type: "int", nullable: false),
                    KullanimSekli = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Fiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdaFiyatlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_KonaklamaTipleri_KonaklamaTipiId",
                        column: x => x.KonaklamaTipiId,
                        principalSchema: "dbo",
                        principalTable: "KonaklamaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_MisafirTipleri_MisafirTipiId",
                        column: x => x.MisafirTipiId,
                        principalSchema: "dbo",
                        principalTable: "MisafirTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaFiyatlari_TesisOdaTipleri_TesisOdaTipiId",
                        column: x => x.TesisOdaTipiId,
                        principalSchema: "dbo",
                        principalTable: "TesisOdaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Odalar",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OdaNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinaId = table.Column<int>(type: "int", nullable: false),
                    TesisOdaTipiId = table.Column<int>(type: "int", nullable: false),
                    KatNo = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    TemizlikDurumu = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Hazir"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Odalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Odalar_Binalar_BinaId",
                        column: x => x.BinaId,
                        principalSchema: "dbo",
                        principalTable: "Binalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Odalar_TesisOdaTipleri_TesisOdaTipiId",
                        column: x => x.TesisOdaTipiId,
                        principalSchema: "dbo",
                        principalTable: "TesisOdaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TesisOdaTipiOzellikDegerleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisOdaTipiId = table.Column<int>(type: "int", nullable: false),
                    OdaOzellikId = table.Column<int>(type: "int", nullable: false),
                    Deger = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TesisOdaTipiOzellikDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipiOzellikDegerleri_OdaOzellikleri_OdaOzellikId",
                        column: x => x.OdaOzellikId,
                        principalSchema: "dbo",
                        principalTable: "OdaOzellikleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TesisOdaTipiOzellikDegerleri_TesisOdaTipleri_TesisOdaTipiId",
                        column: x => x.TesisOdaTipiId,
                        principalSchema: "dbo",
                        principalTable: "TesisOdaTipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruGecmisKatilimlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampBasvuruSahibiId = table.Column<int>(type: "int", nullable: false),
                    KatilimYili = table.Column<int>(type: "int", nullable: false),
                    KaynakBasvuruId = table.Column<int>(type: "int", nullable: true),
                    BeyanMi = table.Column<bool>(type: "bit", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruGecmisKatilimlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvuruGecmisKatilimlari_KampBasvuruSahipleri_KampBasvuruSahibiId",
                        column: x => x.KampBasvuruSahibiId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvuruSahipleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampBasvuruGecmisKatilimlari_KampBasvurulari_KaynakBasvuruId",
                        column: x => x.KaynakBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampBasvuruKatilimcilari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KampBasvuruId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    DogumTarihi = table.Column<DateTime>(type: "date", nullable: false),
                    BasvuruSahibiMi = table.Column<bool>(type: "bit", nullable: false),
                    KatilimciTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AkrabalikTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KimlikBilgileriDogrulandiMi = table.Column<bool>(type: "bit", nullable: false),
                    YemekTalepEdiyorMu = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampBasvuruKatilimcilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampBasvuruKatilimcilari_KampBasvurulari_KampBasvuruId",
                        column: x => x.KampBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KampRezervasyonlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KampBasvuruId = table.Column<int>(type: "int", nullable: false),
                    KampDonemiId = table.Column<int>(type: "int", nullable: false),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    BasvuruSahibiAdiSoyadi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BasvuruSahibiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KonaklamaBirimiTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KatilimciSayisi = table.Column<int>(type: "int", nullable: false),
                    DonemToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvansToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IptalNedeni = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IptalTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KampRezervasyonlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KampRezervasyonlari_KampBasvurulari_KampBasvuruId",
                        column: x => x.KampBasvuruId,
                        principalSchema: "dbo",
                        principalTable: "KampBasvurulari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampRezervasyonlari_KampDonemleri_KampDonemiId",
                        column: x => x.KampDonemiId,
                        principalSchema: "dbo",
                        principalTable: "KampDonemleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KampRezervasyonlari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklamaHakkiTuketimKayitlari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonKonaklamaHakkiId = table.Column<int>(type: "int", nullable: false),
                    IsletmeAlaniId = table.Column<int>(type: "int", nullable: true),
                    TuketimTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    KullanimTipi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimNoktasi = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    KullanimNoktasiAdiSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TuketimNoktasiAdi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonKonaklamaHakkiTuketimKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_IsletmeAlanlari_IsletmeAlaniId",
                        column: x => x.IsletmeAlaniId,
                        principalSchema: "dbo",
                        principalTable: "IsletmeAlanlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonKonaklamaHaklari_RezervasyonKonaklamaHakkiId",
                        column: x => x.RezervasyonKonaklamaHakkiId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonKonaklamaHaklari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklamaHakkiTuketimKayitlari_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdaKullanimBloklari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TesisId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    BlokTipi = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdaKullanimBloklari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaKullanimBloklari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaKullanimBloklari_Tesisler_TesisId",
                        column: x => x.TesisId,
                        principalSchema: "dbo",
                        principalTable: "Tesisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdaOzellikDegerleri",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    OdaOzellikId = table.Column<int>(type: "int", nullable: false),
                    Deger = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdaOzellikDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdaOzellikDegerleri_OdaOzellikleri_OdaOzellikId",
                        column: x => x.OdaOzellikId,
                        principalSchema: "dbo",
                        principalTable: "OdaOzellikleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdaOzellikDegerleri_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonEkHizmetler",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonKonaklayanId = table.Column<int>(type: "int", nullable: false),
                    EkHizmetId = table.Column<int>(type: "int", nullable: false),
                    EkHizmetTarifeId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    YatakNoSnapshot = table.Column<int>(type: "int", nullable: true),
                    TarifeAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BirimAdiSnapshot = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OdaNoSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinaAdiSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HizmetTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonEkHizmetler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_EkHizmetTarifeleri_EkHizmetTarifeId",
                        column: x => x.EkHizmetTarifeId,
                        principalSchema: "dbo",
                        principalTable: "EkHizmetTarifeleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_EkHizmetler_EkHizmetId",
                        column: x => x.EkHizmetId,
                        principalSchema: "dbo",
                        principalTable: "EkHizmetler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_RezervasyonKonaklayanlar_RezervasyonKonaklayanId",
                        column: x => x.RezervasyonKonaklayanId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonKonaklayanlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonEkHizmetler_Rezervasyonlar_RezervasyonId",
                        column: x => x.RezervasyonId,
                        principalSchema: "dbo",
                        principalTable: "Rezervasyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonKonaklayanSegmentAtamalari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonKonaklayanId = table.Column<int>(type: "int", nullable: false),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    YatakNo = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonKonaklayanSegmentAtamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_RezervasyonKonaklayanlar_RezervasyonKonaklayanId",
                        column: x => x.RezervasyonKonaklayanId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonKonaklayanlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RezervasyonSegmentOdaAtamalari",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RezervasyonSegmentId = table.Column<int>(type: "int", nullable: false),
                    OdaId = table.Column<int>(type: "int", nullable: false),
                    AyrilanKisiSayisi = table.Column<int>(type: "int", nullable: false),
                    OdaNoSnapshot = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BinaAdiSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OdaTipiAdiSnapshot = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PaylasimliMiSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    KapasiteSnapshot = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RezervasyonSegmentOdaAtamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentOdaAtamalari_Odalar_OdaId",
                        column: x => x.OdaId,
                        principalSchema: "dbo",
                        principalTable: "Odalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RezervasyonSegmentOdaAtamalari_RezervasyonSegmentleri_RezervasyonSegmentId",
                        column: x => x.RezervasyonSegmentId,
                        principalSchema: "dbo",
                        principalTable: "RezervasyonSegmentleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bildirimler_UserId_CreatedAt",
                schema: "dbo",
                table: "Bildirimler",
                columns: new[] { "UserId", "CreatedAt" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bildirimler_UserId_IsRead_CreatedAt",
                schema: "dbo",
                table: "Bildirimler",
                columns: new[] { "UserId", "IsRead", "CreatedAt" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BildirimTercihleri_UserId",
                schema: "dbo",
                table: "BildirimTercihleri",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Binalar_TesisId_Ad",
                schema: "dbo",
                table: "Binalar",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BinaYoneticileri_BinaId_UserId",
                schema: "dbo",
                table: "BinaYoneticileri",
                columns: new[] { "BinaId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                schema: "dbo",
                table: "Countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                column: "GlobalEkHizmetTanimiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_TesisId_Ad",
                schema: "dbo",
                table: "EkHizmetler",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetler_TesisId_GlobalEkHizmetTanimiId",
                schema: "dbo",
                table: "EkHizmetler",
                columns: new[] { "TesisId", "GlobalEkHizmetTanimiId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [GlobalEkHizmetTanimiId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_EkHizmetId",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                column: "EkHizmetId");

            migrationBuilder.CreateIndex(
                name: "IX_EkHizmetTarifeleri_TesisId_EkHizmetId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "EkHizmetTarifeleri",
                columns: new[] { "TesisId", "EkHizmetId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalEkHizmetTanimlari_Ad",
                schema: "dbo",
                table: "GlobalEkHizmetTanimlari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Iller_Ad",
                schema: "dbo",
                table: "Iller",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliKonaklamaTipleri_IndirimKuraliId_KonaklamaTipiId",
                schema: "dbo",
                table: "IndirimKuraliKonaklamaTipleri",
                columns: new[] { "IndirimKuraliId", "KonaklamaTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliKonaklamaTipleri_KonaklamaTipiId",
                schema: "dbo",
                table: "IndirimKuraliKonaklamaTipleri",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliMisafirTipleri_IndirimKuraliId_MisafirTipiId",
                schema: "dbo",
                table: "IndirimKuraliMisafirTipleri",
                columns: new[] { "IndirimKuraliId", "MisafirTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKuraliMisafirTipleri_MisafirTipiId",
                schema: "dbo",
                table: "IndirimKuraliMisafirTipleri",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKurallari_Kod",
                schema: "dbo",
                table: "IndirimKurallari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndirimKurallari_TesisId",
                schema: "dbo",
                table: "IndirimKurallari",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlaniSiniflari_Ad",
                schema: "dbo",
                table: "IsletmeAlaniSiniflari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlaniSiniflari_Kod",
                schema: "dbo",
                table: "IsletmeAlaniSiniflari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlanlari_BinaId_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                columns: new[] { "BinaId", "IsletmeAlaniSinifiId" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IsletmeAlanlari_IsletmeAlaniSinifiId",
                schema: "dbo",
                table: "IsletmeAlanlari",
                column: "IsletmeAlaniSinifiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampAkrabalikTipleri_Kod",
                schema: "dbo",
                table: "KampAkrabalikTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruGecmisKatilimlari_KampBasvuruSahibiId_KatilimYili",
                schema: "dbo",
                table: "KampBasvuruGecmisKatilimlari",
                columns: new[] { "KampBasvuruSahibiId", "KatilimYili" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruGecmisKatilimlari_KaynakBasvuruId",
                schema: "dbo",
                table: "KampBasvuruGecmisKatilimlari",
                column: "KaynakBasvuruId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruKatilimcilari_KampBasvuruId_TcKimlikNo",
                schema: "dbo",
                table: "KampBasvuruKatilimcilari",
                columns: new[] { "KampBasvuruId", "TcKimlikNo" },
                filter: "[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_BasvuruNo",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "BasvuruNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampBasvuruSahibiId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "KampBasvuruSahibiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampBasvurulari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvurulari_TesisId",
                schema: "dbo",
                table: "KampBasvurulari",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahibiTipleri_Kod",
                schema: "dbo",
                table: "KampBasvuruSahibiTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahipleri_TcKimlikNo",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                column: "TcKimlikNo",
                unique: true,
                filter: "[IsDeleted] = 0 AND [TcKimlikNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampBasvuruSahipleri_UserId",
                schema: "dbo",
                table: "KampBasvuruSahipleri",
                column: "UserId",
                filter: "[IsDeleted] = 0 AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemiTesisleri_KampDonemiId_TesisId",
                schema: "dbo",
                table: "KampDonemiTesisleri",
                columns: new[] { "KampDonemiId", "TesisId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemiTesisleri_TesisId",
                schema: "dbo",
                table: "KampDonemiTesisleri",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_KampProgramiId_Yil_Ad",
                schema: "dbo",
                table: "KampDonemleri",
                columns: new[] { "KampProgramiId", "Yil", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampDonemleri_Kod",
                schema: "dbo",
                table: "KampDonemleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKatilimciTipleri_Kod",
                schema: "dbo",
                table: "KampKatilimciTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_AktifMi",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                column: "AktifMi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKonaklamaTarifeleri_KampProgramiId_Kod",
                schema: "dbo",
                table: "KampKonaklamaTarifeleri",
                columns: new[] { "KampProgramiId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampKuralSetleri_KampProgramiId_KampYili",
                schema: "dbo",
                table: "KampKuralSetleri",
                columns: new[] { "KampProgramiId", "KampYili" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampParametreleri_Kod",
                schema: "dbo",
                table: "KampParametreleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramiBasvuruSahibiTipKurallari_KampBasvuruSahibiTipiId",
                schema: "dbo",
                table: "KampProgramiBasvuruSahibiTipKurallari",
                column: "KampBasvuruSahibiTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramiBasvuruSahibiTipKurallari_KampProgramiId_KampBasvuruSahibiTipiId",
                schema: "dbo",
                table: "KampProgramiBasvuruSahibiTipKurallari",
                columns: new[] { "KampProgramiId", "KampBasvuruSahibiTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramiParametreAyarlari_KampProgramiId",
                schema: "dbo",
                table: "KampProgramiParametreAyarlari",
                column: "KampProgramiId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Ad",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampProgramlari_Kod",
                schema: "dbo",
                table: "KampProgramlari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampBasvuruId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "KampBasvuruId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_KampDonemiId_TesisId_Durum",
                schema: "dbo",
                table: "KampRezervasyonlari",
                columns: new[] { "KampDonemiId", "TesisId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_RezervasyonNo",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "RezervasyonNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KampRezervasyonlari_TesisId",
                schema: "dbo",
                table: "KampRezervasyonlari",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KampYasUcretKurallari_AktifMi",
                schema: "dbo",
                table: "KampYasUcretKurallari",
                column: "AktifMi",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipiIcerikKalemleri_KonaklamaTipiId_HizmetKodu",
                schema: "dbo",
                table: "KonaklamaTipiIcerikKalemleri",
                columns: new[] { "KonaklamaTipiId", "HizmetKodu" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipleri_Ad",
                schema: "dbo",
                table: "KonaklamaTipleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KonaklamaTipleri_Kod",
                schema: "dbo",
                table: "KonaklamaTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciTesisSahiplikleri_TesisId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                column: "TesisId");

            migrationBuilder.CreateIndex(
                name: "IX_KullaniciTesisSahiplikleri_UserId",
                schema: "dbo",
                table: "KullaniciTesisSahiplikleri",
                column: "UserId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MisafirTipleri_Ad",
                schema: "dbo",
                table: "MisafirTipleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MisafirTipleri_Kod",
                schema: "dbo",
                table: "MisafirTipleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_KonaklamaTipiId",
                schema: "dbo",
                table: "OdaFiyatlari",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_MisafirTipiId",
                schema: "dbo",
                table: "OdaFiyatlari",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaFiyatlari_TesisOdaTipiId_KonaklamaTipiId_MisafirTipiId_KullanimSekli_KisiSayisi_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaFiyatlari",
                columns: new[] { "TesisOdaTipiId", "KonaklamaTipiId", "MisafirTipiId", "KullanimSekli", "KisiSayisi", "BaslangicTarihi", "BitisTarihi" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OdaKullanimBloklari_OdaId",
                schema: "dbo",
                table: "OdaKullanimBloklari",
                column: "OdaId",
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaKullanimBloklari_TesisId_OdaId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "OdaKullanimBloklari",
                columns: new[] { "TesisId", "OdaId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Odalar_BinaId_OdaNo",
                schema: "dbo",
                table: "Odalar",
                columns: new[] { "BinaId", "OdaNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Odalar_TesisOdaTipiId",
                schema: "dbo",
                table: "Odalar",
                column: "TesisOdaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikDegerleri_OdaId_OdaOzellikId",
                schema: "dbo",
                table: "OdaOzellikDegerleri",
                columns: new[] { "OdaId", "OdaOzellikId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikDegerleri_OdaOzellikId",
                schema: "dbo",
                table: "OdaOzellikDegerleri",
                column: "OdaOzellikId");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikleri_Ad",
                schema: "dbo",
                table: "OdaOzellikleri",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaOzellikleri_Kod",
                schema: "dbo",
                table: "OdaOzellikleri",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaSiniflari_Ad",
                schema: "dbo",
                table: "OdaSiniflari",
                column: "Ad",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OdaSiniflari_Kod",
                schema: "dbo",
                table: "OdaSiniflari",
                column: "Kod",
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonDegisiklikGecmisleri_RezervasyonId_CreatedAt",
                schema: "dbo",
                table: "RezervasyonDegisiklikGecmisleri",
                columns: new[] { "RezervasyonId", "CreatedAt" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_EkHizmetId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "EkHizmetId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_EkHizmetTarifeId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "EkHizmetTarifeId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_OdaId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "OdaId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonId_HizmetTarihi",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                columns: new[] { "RezervasyonId", "HizmetTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonKonaklayanId_HizmetTarihi",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                columns: new[] { "RezervasyonKonaklayanId", "HizmetTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonEkHizmetler_RezervasyonSegmentId",
                schema: "dbo",
                table: "RezervasyonEkHizmetler",
                column: "RezervasyonSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_IsletmeAlaniId",
                schema: "dbo",
                table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
                column: "IsletmeAlaniId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonId_TuketimTarihi",
                schema: "dbo",
                table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
                columns: new[] { "RezervasyonId", "TuketimTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklamaHakkiTuketimKayitlari_RezervasyonKonaklamaHakkiId_TuketimTarihi",
                schema: "dbo",
                table: "RezervasyonKonaklamaHakkiTuketimKayitlari",
                columns: new[] { "RezervasyonKonaklamaHakkiId", "TuketimTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklamaHaklari_RezervasyonId_HizmetKodu_HakTarihi_Periyot",
                schema: "dbo",
                table: "RezervasyonKonaklamaHaklari",
                columns: new[] { "RezervasyonId", "HizmetKodu", "HakTarihi", "Periyot" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanlar_RezervasyonId_SiraNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanlar",
                columns: new[] { "RezervasyonId", "SiraNo" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_OdaId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                column: "OdaId");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonKonaklayanId_RezervasyonSegmentId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonKonaklayanId", "RezervasyonSegmentId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentId_OdaId",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonKonaklayanSegmentAtamalari_RezervasyonSegmentId_OdaId_YatakNo",
                schema: "dbo",
                table: "RezervasyonKonaklayanSegmentAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId", "YatakNo" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [YatakNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_KonaklamaTipiId",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_ReferansNo",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "ReferansNo",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_RezervasyonDurumu",
                schema: "dbo",
                table: "Rezervasyonlar",
                column: "RezervasyonDurumu",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervasyonlar_TesisId_GirisTarihi_CikisTarihi",
                schema: "dbo",
                table: "Rezervasyonlar",
                columns: new[] { "TesisId", "GirisTarihi", "CikisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonOdemeler_RezervasyonId_OdemeTarihi",
                schema: "dbo",
                table: "RezervasyonOdemeler",
                columns: new[] { "RezervasyonId", "OdemeTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentleri_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "RezervasyonSegmentleri",
                columns: new[] { "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentleri_RezervasyonId_SegmentSirasi",
                schema: "dbo",
                table: "RezervasyonSegmentleri",
                columns: new[] { "RezervasyonId", "SegmentSirasi" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentOdaAtamalari_OdaId",
                schema: "dbo",
                table: "RezervasyonSegmentOdaAtamalari",
                column: "OdaId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RezervasyonSegmentOdaAtamalari_RezervasyonSegmentId_OdaId",
                schema: "dbo",
                table: "RezervasyonSegmentOdaAtamalari",
                columns: new[] { "RezervasyonSegmentId", "OdaId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SezonKurallari_TesisId_BaslangicTarihi_BitisTarihi",
                schema: "dbo",
                table: "SezonKurallari",
                columns: new[] { "TesisId", "BaslangicTarihi", "BitisTarihi" },
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SezonKurallari_TesisId_Kod",
                schema: "dbo",
                table: "SezonKurallari",
                columns: new[] { "TesisId", "Kod" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipiIcerikOverridelari_KonaklamaTipiIcerikKalemiId",
                schema: "dbo",
                table: "TesisKonaklamaTipiIcerikOverridelari",
                column: "KonaklamaTipiIcerikKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipiIcerikOverridelari_TesisId_KonaklamaTipiIcerikKalemiId",
                schema: "dbo",
                table: "TesisKonaklamaTipiIcerikOverridelari",
                columns: new[] { "TesisId", "KonaklamaTipiIcerikKalemiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipleri_KonaklamaTipiId",
                schema: "dbo",
                table: "TesisKonaklamaTipleri",
                column: "KonaklamaTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisKonaklamaTipleri_TesisId_KonaklamaTipiId",
                schema: "dbo",
                table: "TesisKonaklamaTipleri",
                columns: new[] { "TesisId", "KonaklamaTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tesisler_IlId_Ad",
                schema: "dbo",
                table: "Tesisler",
                columns: new[] { "IlId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TesisMisafirTipleri_MisafirTipiId",
                schema: "dbo",
                table: "TesisMisafirTipleri",
                column: "MisafirTipiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisMisafirTipleri_TesisId_MisafirTipiId",
                schema: "dbo",
                table: "TesisMisafirTipleri",
                columns: new[] { "TesisId", "MisafirTipiId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipiOzellikDegerleri_OdaOzellikId",
                schema: "dbo",
                table: "TesisOdaTipiOzellikDegerleri",
                column: "OdaOzellikId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipiOzellikDegerleri_TesisOdaTipiId_OdaOzellikId",
                schema: "dbo",
                table: "TesisOdaTipiOzellikDegerleri",
                columns: new[] { "TesisOdaTipiId", "OdaOzellikId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipleri_OdaSinifiId",
                schema: "dbo",
                table: "TesisOdaTipleri",
                column: "OdaSinifiId");

            migrationBuilder.CreateIndex(
                name: "IX_TesisOdaTipleri_TesisId_Ad",
                schema: "dbo",
                table: "TesisOdaTipleri",
                columns: new[] { "TesisId", "Ad" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [AktifMi] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TesisResepsiyonistleri_TesisId_UserId",
                schema: "dbo",
                table: "TesisResepsiyonistleri",
                columns: new[] { "TesisId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TesisYoneticileri_TesisId_UserId",
                schema: "dbo",
                table: "TesisYoneticileri",
                columns: new[] { "TesisId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bildirimler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BildirimTercihleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "BinaYoneticileri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IndirimKuraliKonaklamaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IndirimKuraliMisafirTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampAkrabalikTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruGecmisKatilimlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruKatilimcilari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampDonemiTesisleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampKatilimciTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampKonaklamaTarifeleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampKuralSetleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampParametreleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampProgramiBasvuruSahibiTipKurallari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampProgramiParametreAyarlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampRezervasyonlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampYasUcretKurallari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KullaniciTesisSahiplikleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaFiyatlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaKullanimBloklari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaOzellikDegerleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonDegisiklikGecmisleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonEkHizmetler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonKonaklamaHakkiTuketimKayitlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonKonaklayanSegmentAtamalari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonOdemeler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonSegmentOdaAtamalari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SezonKurallari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisKonaklamaTipiIcerikOverridelari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisKonaklamaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisMisafirTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisOdaTipiOzellikDegerleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisResepsiyonistleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisYoneticileri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IndirimKurallari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruSahibiTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvurulari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EkHizmetTarifeleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IsletmeAlanlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonKonaklamaHaklari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonKonaklayanlar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Odalar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RezervasyonSegmentleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KonaklamaTipiIcerikKalemleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MisafirTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaOzellikleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampBasvuruSahipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampDonemleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EkHizmetler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IsletmeAlaniSiniflari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Binalar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TesisOdaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Rezervasyonlar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KampProgramlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GlobalEkHizmetTanimlari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OdaSiniflari",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "KonaklamaTipleri",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tesisler",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Iller",
                schema: "dbo");
        }
    }
}
