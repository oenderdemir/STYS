using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STYS.Infrastructure.EntityFramework.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// SatisIadeFaturasi/AlisIadeFaturasi (BelgeTipi 6/7) satırlarında, KaynakSatirId'nin veri
    /// tabanında KANONİK biçimde (kaynakSatirId.ToString(InvariantCulture) - baştaki sıfırlar
    /// olmadan) saklanmasını sağlar. SatisBelgesiService.ValidateIadeSatirlariAsync artık bu
    /// biçimi HER yazım yolunda (Create, satırlı Update, onay akışları) uygular, ancak bu
    /// düzeltmeden ÖNCE oluşturulmuş satırlar ("00123" gibi baştaki sıfırlı biçimler) veri
    /// tabanında kanonik olmayan halleriyle kalmış olabilir. Kümülatif iade miktarı sorgusu
    /// (x.KaynakSatirId == kaynakSatirId.ToString(...)) SAF METİN eşitliği kullandığından, "123"
    /// ve "00123" AYNI kaynak satırı gösterse dahi birbirini GÖRMEZ ve kümülatif sınır delinebilir
    /// - bu migration, geriye dönük olarak yalnızca İADE TİPİ belgelere ait, GEÇERLİ (yalnızca
    /// ASCII rakamlardan oluşan, pozitif, int aralığına sığan) ama kanonik olmayan değerleri
    /// düzeltir.
    ///
    /// Kapsam dışı bırakılan (BİLİNÇLİ OLARAK dokunulmayan) durumlar:
    /// - Normal SatisFaturasi/AlisFaturasi (ve diğer BelgeTipi) satırları - KaynakSatirId bu
    ///   belgelerde hâlâ diğer modüllerin (Kamp, Restoran, Rezervasyon) kullandığı serbest biçimli
    ///   harici kaynak kimliğidir, ASLA dokunulmaz.
    /// - İade tipi belgelerde NULL/boş, rakam olmayan karakter içeren, veya int aralığını aşan
    ///   KaynakSatirId değerleri - bunlar zaten uygulama seviyesinde geçersiz sayılır (satır bir
    ///   sonraki Create/Update/onay adımında ValidateIadeSatirlariAsync tarafından reddedilir);
    ///   hangi satıra ait olduğu güvenle belirlenemeyen bu değerler TAHMİN EDİLEREK düzeltilmez.
    /// </summary>
    public partial class HardenIadeSatirKaynagiCanonicalFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT: WHERE koşulları arasında CAST/TRY_CAST kullanılırken SQL Server'ın koşul
            // DEĞERLENDİRME SIRASINI yazıldığı sırayla garanti ETMEDİĞİ bilinir (sorgu planlayıcısı
            // yeniden sıralayabilir) - bu yüzden "önce LIKE ile filtrele, sonra CAST et" varsayımına
            // GÜVENİLMEZ; CAST rakam olmayan bir değere planlayıcı tarafından erken uygulanırsa
            // dönüşüm hatasıyla TÜM UPDATE başarısız olur. Bunun yerine HER YERDE TRY_CAST kullanılır
            // (geçersiz girdide hata fırlatmak yerine NULL döner) ve NULL sonuçlar ayrıca elenir - bu,
            // değerlendirme sırasından BAĞIMSIZ olarak güvenlidir.
            migrationBuilder.Sql("""
                SET NOCOUNT ON;

                UPDATE ssb
                SET ssb.[KaynakSatirId] = CAST(TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) AS NVARCHAR(100))
                FROM [muhasebe].[SatisBelgesiSatirlari] ssb
                INNER JOIN [muhasebe].[SatisBelgeleri] sb ON sb.[Id] = ssb.[SatisBelgesiId]
                WHERE sb.[BelgeTipi] IN (6, 7)
                  AND ssb.[KaynakSatirId] IS NOT NULL
                  AND ssb.[KaynakSatirId] <> N''
                  -- Yalnızca ASCII '0'-'9' karakterlerinden oluşan değerler (işaret/boşluk/binlik
                  -- ayıracı YOK) - uygulama seviyesindeki int.TryParse(..., NumberStyles.None, ...)
                  -- ile AYNI, kesin kabul kriteri.
                  AND ssb.[KaynakSatirId] NOT LIKE N'%[^0-9]%'
                  -- BIGINT taşmasına karşı güvenli üst sınır (gerçek satır Id'leri int aralığında).
                  AND LEN(ssb.[KaynakSatirId]) <= 18
                  AND TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) IS NOT NULL
                  AND TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) > 0
                  AND TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) <= 2147483647
                  -- Yalnızca GERÇEKTEN kanonik olmayan (ör. baştaki sıfırlı) değerleri güncelle -
                  -- zaten kanonik olan satırlarda no-op bir UPDATE üretilmez.
                  AND ssb.[KaynakSatirId] <> CAST(TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) AS NVARCHAR(100));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Kasıtlı olarak no-op: bu migration yalnızca MEVCUT KaynakSatirId değerlerini kanonik
            // biçime dönüştürür (veri onarımı), yeni bir şema/yapı oluşturmaz - geri alınacak bir
            // "yapı" yoktur. Orijinal (kanonik olmayan) metin temsilini "geri almak" da anlamlı
            // değildir - kanonik değer, aynı kaynak satırı GEÇERLİ ve DOĞRU şekilde temsil etmeye
            // devam eder.
        }
    }
}
