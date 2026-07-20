namespace STYS.Kbs.Dtos;

public record KbsGirisTalebi(int TesisId, int RezervasyonKonaklayanId, string Ad, string Soyad, string? KimlikNo, string? BelgeNo, string? UyrukKodu, DateTime GirisTarihi);
public record KbsCikisTalebi(int TesisId, int RezervasyonKonaklayanId, DateTime CikisTarihi);
public record KbsOdaGuncellemeTalebi(int TesisId, int RezervasyonKonaklayanId, string OdaNo, DateTime OlayTarihi);
public record KbsSonuc(bool Basarili, string Kod, string Aciklama, string? HataSinifi = null, bool SonucuBelirsiz = false);
public record KbsBaglantiTestSonucu(bool Basarili, string Aciklama, bool CanliCagriYapildi = false);
public record KbsFiiliOlaySonucDto(int KonaklayanId, DateTime OlayTarihi, long? BildirimId, bool ZatenKayitli);
public record KbsOdaDegisikligiRequestDto(string OdaNo, DateTime? OlayTarihi);
public record KbsTesisAyariDto(int TesisId, string KollukSistemi, string EntegrasyonTipi, string? TesisKodu, string? SecretReference, bool AktifMi, bool CanliGonderimAktifMi, DateTime? SonBaglantiKontrolTarihi, string? SonBaglantiKontrolSonucu);
public record KbsTesisAyariGuncelleDto(string KollukSistemi, string EntegrasyonTipi, string? TesisKodu, string? SecretReference, bool AktifMi, bool CanliGonderimAktifMi);
public record KbsBildirimListeDto(long Id, int TesisId, int RezervasyonId, int RezervasyonKonaklayanId, string Kisi, string BildirimTipi, string Saglayici, string Durum, int DenemeSayisi, string? SonHataMesaji, DateTime? GonderimTarihi, DateTime? TamamlanmaTarihi);
public record KbsSayfaliSonucDto<T>(IReadOnlyList<T> Kayitlar, int Toplam, int Sayfa, int SayfaBoyutu);
public record KbsGunlukOzetDto(int Basarili, int Bekleyen, int Hatali, int MudahaleGerekli);
