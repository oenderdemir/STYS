namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

/// <summary>
/// Rezervasyon tahsilat fisinde alacak tarafinin hangi hesaba yazilacagini belirleyen,
/// tesis bazinda konfigure edilebilir secenekler (bkz. Tesis.RezervasyonTahsilatAlacakHesapTipi).
/// </summary>
public static class RezervasyonTahsilatAlacakHesapTipleri
{
    /// <summary>Alacak tarafi dogrudan CariKart.MuhasebeHesapPlaniId'ye yazilir.
    /// Fatura/gelir tahakkuku ile ayni cari hesap uzerinden CariHareketKapamaService ile netlesir.</summary>
    public const string Cari = "Cari";

    /// <summary>Alacak tarafi ayri bir "Alinan Siparis Avanslari" hesabina (bkz. MuhasebeAnaHesapKodlari.AlinanSiparisAvanslari) yazilir.</summary>
    public const string AlinanAvans = "AlinanAvans";

    public static readonly string[] Hepsi = [Cari, AlinanAvans];
}
