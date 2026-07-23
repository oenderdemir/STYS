using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.PosTahsilatValorleri.Dtos;

public class PosTahsilatValorDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int TahsilatOdemeBelgesiId { get; set; }
    public string? TahsilatBelgeNo { get; set; }
    public int KrediKartiHesapId { get; set; }
    public string? KrediKartiHesapAdi { get; set; }
    public int? BagliBankaHesapId { get; set; }
    public string? BagliBankaHesapAdi { get; set; }
    public int? KomisyonGiderHesapPlaniId { get; set; }
    public DateTime OdemeTarihi { get; set; }
    public int ValorGunSayisi { get; set; }
    public string ValorGunTuru { get; set; } = string.Empty;
    public DateOnly BeklenenValorTarihi { get; set; }
    public bool OtomatikAktarimMi { get; set; }
    public decimal? KomisyonOraniSnapshot { get; set; }
    public decimal BrutTutar { get; set; }
    public decimal KomisyonTutari { get; set; }
    public decimal NetTutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = string.Empty;
    public DateTime? AktarimTarihi { get; set; }
    public int? MuhasebeFisId { get; set; }
    public int? TersKayitMuhasebeFisId { get; set; }
    public string? HataMesaji { get; set; }
    public string? Aciklama { get; set; }
    public int DenemeSayisi { get; set; }

    // Backend'de IValorTarihHesaplamaService.DegerlendirDurum ile hesaplanir, Angular tekrar
    // hesaplamaz.
    public int ValoreKalanGun { get; set; }
    public bool ValorGectiMi { get; set; }
    public bool BugunValorGunuMu { get; set; }
    public bool AktarilabilirMi { get; set; }
}

public class PosTahsilatValorOzetDto
{
    public decimal ValorBekleyenToplam { get; set; }
    public int ValorBekleyenAdet { get; set; }
    public decimal BugunValoruGelenToplam { get; set; }
    public int BugunValoruGelenAdet { get; set; }
    public decimal ValoruGecmisToplam { get; set; }
    public int ValoruGecmisAdet { get; set; }
    public decimal AktarilanToplam { get; set; }
    public int AktarilanAdet { get; set; }
    public int HataliAdet { get; set; }
}

public class ManuelAktarimGuncellemeDto
{
    public decimal? KomisyonTutari { get; set; }
    public decimal? NetTutar { get; set; }
    public int? KomisyonGiderHesapPlaniIdOverride { get; set; }
    public string? Aciklama { get; set; }
}

public class PosTahsilatValorAktarimSonucDto
{
    public int Id { get; set; }
    public bool Basarili { get; set; }
    public string? HataMesaji { get; set; }
    public int? MuhasebeFisId { get; set; }
}

public class PosTahsilatValorToplamAktarimSonucDto
{
    public List<PosTahsilatValorAktarimSonucDto> Basarili { get; set; } = [];
    public List<PosTahsilatValorAktarimSonucDto> Hatali { get; set; } = [];
}

public class PosTahsilatValorTopluOnayBilgisiRequest
{
    public List<int>? ValorIdler { get; set; }
    public int? TesisId { get; set; }
    public bool SadeceValoruGelenler { get; set; } = true;
}

public class PosTahsilatValorTopluOnayBilgisiDto
{
    public int Adet { get; set; }
    public decimal ToplamBrut { get; set; }
    public decimal ToplamKomisyon { get; set; }
    public decimal ToplamNet { get; set; }
}

public class DuzeltmeTersKayitRequest
{
    public string Aciklama { get; set; } = string.Empty;
}
