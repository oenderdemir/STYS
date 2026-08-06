using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.9.1 - `scripts/test-ebelge.ps1`/`.sh`'ın ağır profiller (integration/nightly/release)
/// için ana test koşumundan ÖNCE çalıştırdığı, dependency erişilebilirliğini kanıtlayan
/// PREFLIGHT testleri. Bilinçli olarak `Domain=EBelge` trait sözleşmesine TABİ DEĞİLDİR (bkz.
/// `EBelgeTestMetadataContractTests.DomainSozlesmesiIstisnalari`) - bunlar e-Belge DOMAIN
/// davranışını değil, TEST ALTYAPISININ KENDİSİNİN çalışabilir olduğunu doğrular; bu yüzden
/// `Domain=EBelge` filtresiyle çalışan 4 profilin hiçbirinde SAYILMAZLAR (mükerrer sayım/gereksiz
/// yeniden değerlendirme YOK) - yalnız kendi özel `Purpose` trait'leriyle, script'ler tarafından
/// AYRI bir ön-adımda hedeflenirler.
///
/// SQL preflight'ı, e-Belge SQL entegrasyon testlerinin ZATEN kullandığı AYNI bağlantı yolunu
/// (`SatisBelgesiMuhasebeTestSupport.CreateDbContext` - gerçek `StysAppDbContext` + EF Core
/// SqlServer provider) kullanır - script tarafında ayrı bir ham SqlClient/TCP kontrolü İCAT
/// EDİLMEZ. Java sidecar preflight'ı da AYNI şekilde, gerçek entegrasyon testlerinin kullandığı
/// `SchematronSidecarProcessFixture`'ı KENDİ, KISA ÖMÜRLÜ örneğiyle başlatıp hemen kapatır - ana
/// test koşumu henüz BAŞLAMADIĞI için bu, "ikinci bir kalıcı sidecar süreci" YARATMAZ (yalnız
/// sıralı bir ön-kontrol; ana koşumun kendi fixture'larıyla ASLA eş zamanlı ÇALIŞMAZ).
/// </summary>
public class EBelgeSqlSidecarPreflightTests
{
    [Trait("Purpose", "SqlPreflight")]
    [IntegrationFact]
    public async Task SqlServerTestVeriTabaniErisilebilirdir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var erisilebilirMi = await dbContext.Database.CanConnectAsync();

        // Baglanti dizesinin/host-port/kimlik bilgisinin KENDISI HICBIR ZAMAN loglanmaz - yalniz
        // bu GUVENLI, tip-safe boolean sonuc raporlanir.
        Assert.True(erisilebilirMi, "SQL Server test veritabanina baglanti kurulamadi (erisilebilirlik kontrolu basarisiz).");
    }

    [Trait("Purpose", "JavaSidecarPreflight")]
    [Fact]
    public async Task JavaSchematronSidecarBaslatilabilirVeHazirOlur()
    {
        var fixture = new SchematronSidecarProcessFixture();
        await fixture.InitializeAsync();
        try
        {
            Assert.True(fixture.BaseUrl is not null,
                $"Java Schematron sidecar baslatilamadi/hazir olmadi: {fixture.AtlamaNedeni ?? "(neden bildirilmedi)"}");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
