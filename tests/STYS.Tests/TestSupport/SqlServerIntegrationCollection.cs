namespace STYS.Tests;

/// <summary>
/// Gercek SQL Server test veritabanina karsi calisan TUM entegrasyon test siniflarini TEK bir xUnit
/// collection'inda toplar.
///
/// KOK NEDEN: xUnit varsayilan olarak [Collection] atanmamis her test sinifini KENDI, AYRI
/// collection'ina koyar ve farkli collection'lari PARALEL calistirir. Bu depodaki entegrasyon test
/// siniflari (OdemeIzlemeServiceTests, PosTahsilatValorIntegrationTests, vb.) hepsi AYNI paylasilan
/// SQL Server veritabanina karsi kendi Kurum/Il/Tesis/CariKart/MuhasebeHesapPlani... satirlarini
/// olusturup DisposeAsync'te siliyor; ozellikle MuhasebeHesapPlanlari gibi FK'lerle yogun
/// iliskili ortak tablolarda bu es zamanli INSERT/DELETE trafigi SQL Server'in secip iptal ettigi
/// "deadlock victim" hatalarina yol aciyordu (bkz. "Transaction (Process ID ...) was deadlocked on
/// lock resources with another process and has been chosen as the deadlock victim").
///
/// COZUM: bu siniflari AYNI adlandirilmis collection'a alarak xUnit'in onlari birbirine gore
/// SIRAYLA (bir sonraki baslamadan onceki bitirilerek) calistirmasi saglanir - collection ICINDEKI
/// testler zaten sirali calisir, yalnizca FARKLI collection'lar paralel calisir. Boylece gercek DB'ye
/// karsi calisan entegrasyon testleri arasindaki es zamanli erisim kaynakli deadlock riski YAPISAL
/// olarak ortadan kalkar; birim testleri (bu collection'a dahil olmayanlar) mevcut paralellikte
/// kalmaya devam eder - performans regresyonu yalnizca (zaten SQL Server gerektiren ve ortam
/// degiskeni yoksa atlanan) entegrasyon testleriyle sinirlidir.
/// </summary>
[CollectionDefinition(Name)]
public class SqlServerIntegrationCollection
{
    public const string Name = "SqlServerIntegrationTests";
}
