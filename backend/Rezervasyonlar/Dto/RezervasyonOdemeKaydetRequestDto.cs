namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdemeKaydetRequestDto
{
    public decimal OdemeTutari { get; set; }

    public string OdemeTipi { get; set; } = OdemeTipleri.Nakit;

    /// <summary>Nakit hareketi doguran odeme tiplerinde (Nakit/KrediKarti/HavaleEft) zorunludur.</summary>
    public int? KasaBankaHesapId { get; set; }

    /// <summary>Rezervasyonun cari kart baglantisi henuz yoksa ve otomatik eslesme/varsayilan
    /// bulunamazsa kullanici tarafindan secilen cari kart. Ilk denemede boş gonderilebilir;
    /// sunucu CARI_KART_SECIMI_GEREKLI hatasi donerse kullanici secim yapip tekrar gonderir.</summary>
    public int? CariKartId { get; set; }

    public string? Aciklama { get; set; }
}

