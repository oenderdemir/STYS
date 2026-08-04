using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.5 renderer testleri için kapsam içi (geçerli), deterministik bir V2 snapshot üretir.
/// Testler bu temel snapshot'ı `with` ifadesiyle değiştirerek belirli senaryoları izole eder.
/// </summary>
internal static class EBelgeUblRendererTestVerisi
{
    public static EBelgeCanonicalSnapshotV2 GecerliSnapshot()
    {
        var satir1 = new EBelgeCanonicalSatirV2
        {
            SiraNo = 1,
            SatirTipi = SatisBelgesiSatirTipi.Konaklama,
            Aciklama = "Konaklama Hizmeti",
            Miktar = 2m,
            Birim = "Adet",
            BirimKodu = "C62",
            BirimFiyat = 1000m,
            IndirimOrani = 0m,
            IndirimTutari = 0m,
            Matrah = 2000m,
            KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
            KdvOrani = 10m,
            KdvTutari = 200m,
            SatirToplami = 2200m,
        };

        var satir2 = new EBelgeCanonicalSatirV2
        {
            SiraNo = 2,
            SatirTipi = SatisBelgesiSatirTipi.Urun,
            Aciklama = "Ek Ürün",
            Miktar = 1m,
            Birim = "Adet",
            BirimKodu = "C62",
            BirimFiyat = 500m,
            IndirimOrani = 10m,
            IndirimTutari = 50m,
            Matrah = 450m,
            KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m,
            KdvTutari = 90m,
            SatirToplami = 540m,
        };

        return new EBelgeCanonicalSnapshotV2
        {
            Metadata = new EBelgeCanonicalSnapshotMetadataV1
            {
                SnapshotSchemaVersion = EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion,
                BelgeVersiyonu = 1,
                EBelgeKaydiDurumu = EBelgeKaydiDurumu.SnapshotHazir,
                EBelgeKanali = EBelgeKanali.EArsiv,
                KararKaynagi = "Test",
                KararZamaniUtc = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc),
            },
            Belge = new EBelgeCanonicalBelgeV2
            {
                SatisBelgesiId = 1,
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                BelgeTarihi = new DateTime(2026, 9, 15),
                FaturaKesimTarihi = new DateTime(2026, 9, 15, 11, 0, 0),
                ResmiFaturaNo = "EAR2026000000001",
                EBelgeUuid = "a1b2c3d4-e5f6-4789-a012-b3c4d5e6f789",
                ProfileID = "EARSIVFATURA",
                InvoiceTypeCode = "SATIS",
                FaturaTarihiTrt = new DateOnly(2026, 9, 15),
                FaturaSaatiTrt = new TimeOnly(11, 0, 0),
            },
            Kurum = new EBelgeCanonicalKurumV2
            {
                KurumId = 1,
                KurumUnvani = "Örnek Otel Turizm A.Ş.",
                VergiNo = "1234567890",
                VergiDairesi = "Merkez Vergi Dairesi",
                Adres = "Örnek Mah. Örnek Cad. No:1",
                Ilce = "Muratpaşa",
                Il = "Antalya",
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = "07100",
                SokakAdi = "Örnek Cad.",
                BinaNo = "1",
                Telefon = "+902425555555",
                Eposta = "info@ornekotel.test",
            },
            Tesis = new EBelgeCanonicalTesisV1
            {
                TesisId = 1,
                TesisUnvani = "Örnek Otel",
                Adres = "Tesis Adresi",
                Telefon = "+902425555555",
                Eposta = "tesis@ornekotel.test",
            },
            Alici = new EBelgeCanonicalAliciV2
            {
                MusteriUnvan = "Alıcı Firma Ltd. Şti.",
                MusteriVergiNo = "9876543210",
                MusteriVergiDairesi = "Kadıköy Vergi Dairesi",
                MusteriAdres = "Alıcı Mah. Alıcı Sok. No:2",
                Ilce = "Kadıköy",
                Il = "İstanbul",
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = "34700",
                SokakAdi = "Alıcı Sok.",
                BinaNo = "2",
                MusteriEposta = "alici@ornek.test",
                MusteriTelefon = "+902165555555",
                KurumsalMi = true,
            },
            CariKart = new EBelgeCanonicalCariKartV1
            {
                CariKartId = 1,
                CariKodu = "C0001",
                EFaturaMukellefiMi = false,
                EArsivKapsamindaMi = true,
            },
            Iade = new EBelgeCanonicalIadeV1(),
            Odeme = new EBelgeCanonicalOdemeV1
            {
                ParaBirimi = "TRY",
                Kur = 1m,
                OdemeTuru = null,
                VadeTarihi = null,
            },
            ToplamMatrah = 2450m,
            ToplamKdv = 290m,
            GenelToplam = 2740m,
            Satirlar = new List<EBelgeCanonicalSatirV2> { satir1, satir2 },
        };
    }

    public static GibKuralSeti KuralSetiYukle() =>
        new EBelgeUblKuralSetiYukleyici(EBelgeUblKuralSetiTestYardimcisi.KuralSetiKokDizin()).Yukle();
}
