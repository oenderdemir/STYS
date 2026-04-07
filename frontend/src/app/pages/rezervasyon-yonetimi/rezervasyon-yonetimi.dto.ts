export interface RezervasyonTesisDto {
    id: number;
    ad: string;
    girisSaati: string;
    cikisSaati: string;
}

export interface RezervasyonOdaTipiDto {
    id: number;
    tesisId: number;
    ad: string;
    kapasite: number;
    paylasimliMi: boolean;
}

export interface UygunOdaAramaRequestDto {
    tesisId: number;
    odaTipiId: number | null;
    kisiSayisi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
}

export interface UygunOdaDto {
    odaId: number;
    odaNo: string;
    binaId: number;
    binaAdi: string;
    odaTipiId: number;
    odaTipiAdi: string;
    kapasite: number;
    paylasimliMi: boolean;
}

export interface KonaklamaSenaryoAramaRequestDto {
    tesisId: number;
    odaTipiId: number | null;
    misafirTipiId: number;
    konaklamaTipiId: number;
    kisiSayisi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    tekKisilikFiyatUygulansinMi: boolean;
    konaklayanCinsiyetleri: Array<string | null>;
}

export interface KonaklamaSenaryoOdaAtamaDto {
    odaId: number;
    odaNo: string;
    binaId: number;
    binaAdi: string;
    odaTipiId: number;
    odaTipiAdi: string;
    paylasimliMi: boolean;
    kapasite: number;
    ayrilanKisiSayisi: number;
}

export interface KonaklamaSenaryoSegmentDto {
    baslangicTarihi: string;
    bitisTarihi: string;
    odaAtamalari: KonaklamaSenaryoOdaAtamaDto[];
}

export interface KonaklamaSenaryoDto {
    senaryoKodu: string;
    aciklama: string;
    toplamOdaSayisi: number;
    odaDegisimSayisi: number;
    toplamBazUcret: number;
    toplamNihaiUcret: number;
    paraBirimi: string;
    uygulananIndirimler?: UygulananIndirimDto[];
    segmentler: KonaklamaSenaryoSegmentDto[];
}

export interface RezervasyonMisafirTipiDto {
    id: number;
    ad: string;
}

export interface RezervasyonKonaklamaTipiDto {
    id: number;
    ad: string;
    icerikKalemleri: RezervasyonKonaklamaTipiIcerikDto[];
}

export interface RezervasyonKonaklamaTipiIcerikDto {
    hizmetKodu: string;
    hizmetAdi: string;
    miktar: number;
    periyot: string;
    periyotAdi: string;
    kullanimTipi: string;
    kullanimTipiAdi: string;
    kullanimNoktasi: string;
    kullanimNoktasiAdi: string;
    kullanimBaslangicSaati: string | null;
    kullanimBitisSaati: string | null;
    checkInGunuGecerliMi: boolean;
    checkOutGunuGecerliMi: boolean;
    aciklama: string | null;
}

export interface RezervasyonIndirimKuraliSecenekDto {
    id: number;
    kod: string;
    ad: string;
    indirimTipi: string;
    deger: number;
    kapsamTipi: string;
    oncelik: number;
    birlesebilirMi: boolean;
}

export interface UygulananIndirimDto {
    indirimKuraliId: number;
    kuralAdi: string;
    indirimTutari: number;
    sonrasiTutar: number;
}

export interface SenaryoFiyatHesaplaOdaAtamaDto {
    odaId: number;
    ayrilanKisiSayisi: number;
}

export interface SenaryoFiyatHesaplaSegmentDto {
    baslangicTarihi: string;
    bitisTarihi: string;
    odaAtamalari: SenaryoFiyatHesaplaOdaAtamaDto[];
}

export interface SenaryoFiyatHesaplaRequestDto {
    tesisId: number;
    misafirTipiId: number;
    konaklamaTipiId: number;
    kisiSayisi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    tekKisilikFiyatUygulansinMi: boolean;
    segmentler: SenaryoFiyatHesaplaSegmentDto[];
    seciliIndirimKuraliIds: number[];
}

export interface SenaryoFiyatHesaplamaSonucuDto {
    toplamBazUcret: number;
    toplamNihaiUcret: number;
    paraBirimi: string;
    uygulananIndirimler: UygulananIndirimDto[];
}

export interface RezervasyonKaydetOdaAtamaDto {
    odaId: number;
    ayrilanKisiSayisi: number;
}

export interface RezervasyonKaydetSegmentDto {
    baslangicTarihi: string;
    bitisTarihi: string;
    odaAtamalari: RezervasyonKaydetOdaAtamaDto[];
}

export interface RezervasyonKaydetRequestDto {
    tesisId: number;
    kisiSayisi: number;
    misafirTipiId: number;
    konaklamaTipiId: number;
    girisTarihi: string;
    cikisTarihi: string;
    tekKisilikFiyatUygulansinMi: boolean;
    misafirAdiSoyadi: string;
    misafirTelefon: string;
    misafirEposta: string | null;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
    misafirCinsiyeti: string | null;
    notlar: string | null;
    toplamBazUcret: number;
    toplamUcret: number;
    paraBirimi: string;
    uygulananIndirimler: UygulananIndirimDto[];
    segmentler: RezervasyonKaydetSegmentDto[];
}

export interface RezervasyonKayitSonucDto {
    id: number;
    referansNo: string;
    rezervasyonDurumu: string;
}

export interface RezervasyonCheckInUyariDto {
    odaId: number;
    odaNo: string;
    binaAdi: string;
    temizlikDurumu: string;
    mesaj: string;
    engelleyiciMi: boolean;
}

export interface RezervasyonCheckInKontrolDto {
    rezervasyonId: number;
    referansNo: string;
    checkInYapilabilir: boolean;
    uyarilar: RezervasyonCheckInUyariDto[];
}

export interface RezervasyonListeDto {
    id: number;
    referansNo: string;
    kaynak: string;
    tesisId: number;
    misafirAdiSoyadi: string;
    misafirTelefon: string;
    misafirEposta: string | null;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
    misafirCinsiyeti: string | null;
    kisiSayisi: number;
    girisTarihi: string;
    cikisTarihi: string;
    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string;
    rezervasyonDurumu: string;
    fiyatlamaOzeti: string;
    konaklayanPlaniTamamlandi: boolean;
    gelenKonaklayanSayisi: number;
    bekleyenKonaklayanSayisi: number;
    ayrilanKonaklayanSayisi: number;
    odaDegisimiGerekli: boolean;
}

export interface RezervasyonDashboardKayitDto {
    id: number;
    referansNo: string;
    misafirAdiSoyadi: string;
    kisiSayisi: number;
    girisTarihi: string;
    cikisTarihi: string;
    rezervasyonDurumu: string;
}

export interface RezervasyonKpiOzetDto {
    tarihAraligiGunSayisi: number;
    toplamRezervasyonSayisi: number;
    iptalRezervasyonSayisi: number;
    iptalOraniYuzde: number;
    toplamGeceSayisi: number;
    satilanGeceSayisi: number;
    dolulukOraniYuzde: number;
    toplamGelir: number;
    adr: number;
    revPar: number;
}

export interface RezervasyonGelirKirilimDto {
    etiket: string;
    tutar: number;
}

export interface RezervasyonKpiTrendGunDto {
    tarih: string;
    gelir: number;
    rezervasyonSayisi: number;
    iptalSayisi: number;
    satilanGeceSayisi: number;
    dolulukOraniYuzde: number;
}

export interface RezervasyonDashboardDto {
    tesisId: number;
    tarih: string;
    kpiBaslangicTarihi: string;
    kpiBitisTarihi: string;
    toplamOdaSayisi: number;
    doluOdaSayisi: number;
    bosOdaSayisi: number;
    toplamKapasite: number;
    kullanilanKapasite: number;
    serbestKapasite: number;
    kpiOzet: RezervasyonKpiOzetDto;
    odemeTipineGoreGelirKirilimi: RezervasyonGelirKirilimDto[];
    durumaGoreRezervasyonKirilimi: RezervasyonGelirKirilimDto[];
    kpiTrendGunluk: RezervasyonKpiTrendGunDto[];
    bugunCheckInler: RezervasyonDashboardKayitDto[];
    bugunCheckOutlar: RezervasyonDashboardKayitDto[];
}

export interface RezervasyonDetayOdaAtamaDto {
    odaId: number;
    odaNo: string;
    binaAdi: string;
    odaTipiAdi: string;
    ayrilanKisiSayisi: number;
    kapasite: number;
    paylasimliMi: boolean;
}

export interface RezervasyonDetaySegmentDto {
    segmentSirasi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    odaAtamalari: RezervasyonDetayOdaAtamaDto[];
}

export interface RezervasyonDetayDto {
    id: number;
    referansNo: string;
    tesisId: number;
    rezervasyonDurumu: string;
    misafirAdiSoyadi: string;
    misafirCinsiyeti: string | null;
    kisiSayisi: number;
    girisTarihi: string;
    cikisTarihi: string;
    konaklamaTipiAdi: string | null;
    tekKisilikFiyatUygulandiMi: boolean;
    fiyatlamaOzeti: string;
    konaklamaTipiIcerikKalemleri: RezervasyonKonaklamaTipiIcerikDto[];
    konaklamaHaklari: RezervasyonKonaklamaHakkiDto[];
    konaklamaUcreti: number;
    ekHizmetToplami: number;
    toplamBazUcret: number;
    toplamUcret: number;
    paraBirimi: string;
    uygulananIndirimler: UygulananIndirimDto[];
    ekHizmetler: RezervasyonEkHizmetDto[];
    segmentler: RezervasyonDetaySegmentDto[];
}

export interface RezervasyonKonaklamaHakkiDto {
    id: number;
    hizmetKodu: string;
    hizmetAdi: string;
    miktar: number;
    periyot: string;
    periyotAdi: string;
    kullanimTipi: string;
    kullanimTipiAdi: string;
    kullanimNoktasi: string;
    kullanimNoktasiAdi: string;
    kullanimBaslangicSaati: string | null;
    kullanimBitisSaati: string | null;
    checkInGunuGecerliMi: boolean;
    checkOutGunuGecerliMi: boolean;
    hakTarihi: string | null;
    aciklama: string | null;
    durum: string;
    tuketilenMiktar: number;
    kalanMiktar: number | null;
    sonTuketimTarihi: string | null;
    tuketimNoktalari: RezervasyonKonaklamaHakkiTuketimNoktasiDto[];
    tuketimKayitlari: RezervasyonKonaklamaHakkiTuketimKaydiDto[];
}

export interface RezervasyonKonaklamaHakkiDurumGuncelleRequestDto {
    durum: string;
}

export interface RezervasyonKonaklamaHakkiTuketimKaydiDto {
    id: number;
    isletmeAlaniId: number | null;
    tuketimTarihi: string;
    miktar: number;
    kullanimTipi: string;
    kullanimNoktasi: string;
    kullanimNoktasiAdi: string;
    tuketimNoktasiAdi: string | null;
    aciklama: string | null;
    createdBy: string;
    createdAt: string | null;
}

export interface RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto {
    isletmeAlaniId: number | null;
    tuketimTarihi: string;
    miktar: number;
    aciklama: string | null;
}

export interface RezervasyonKonaklamaHakkiTuketimNoktasiDto {
    id: number;
    ad: string;
    binaAdi: string;
    sinifKod: string;
    sinifAd: string;
}

export interface RezervasyonDegisiklikGecmisiDto {
    id: number;
    islemTipi: string;
    aciklama: string | null;
    oncekiDegerJson: string | null;
    yeniDegerJson: string | null;
    createdAt: string;
    createdBy: string;
}

export interface RezervasyonKonaklayanPlanDto {
    rezervasyonId: number;
    kisiSayisi: number;
    segmentler: RezervasyonKonaklayanSegmentDto[];
    konaklayanlar: RezervasyonKonaklayanKisiDto[];
}

export interface RezervasyonKonaklayanSegmentDto {
    segmentId: number;
    segmentSirasi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    odaSecenekleri: RezervasyonKonaklayanOdaSecenekDto[];
}

export interface RezervasyonKonaklayanOdaSecenekDto {
    odaId: number;
    odaNo: string;
    binaAdi: string;
    odaTipiAdi: string;
    ayrilanKisiSayisi: number;
    paylasimliMi: boolean;
}

export interface RezervasyonKonaklayanKisiDto {
    siraNo: number;
    adSoyad: string;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
    cinsiyet: string | null;
    katilimDurumu: string;
    atamalar: RezervasyonKonaklayanKisiAtamaDto[];
}

export interface RezervasyonKonaklayanKisiAtamaDto {
    segmentId: number;
    odaId: number | null;
    yatakNo: number | null;
}

export interface RezervasyonKonaklayanPlanKaydetRequestDto {
    konaklayanlar: RezervasyonKonaklayanKisiKaydetDto[];
}

export interface RezervasyonKonaklayanKisiKaydetDto {
    siraNo: number;
    adSoyad: string;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
    cinsiyet: string | null;
    katilimDurumu: string | null;
    atamalar: RezervasyonKonaklayanKisiAtamaKaydetDto[];
}

export interface RezervasyonKonaklayanKisiAtamaKaydetDto {
    segmentId: number;
    odaId: number | null;
    yatakNo: number | null;
}

export interface RezervasyonOdaDegisimAdayOdaDto {
    odaId: number;
    odaNo: string;
    binaAdi: string;
    odaTipiAdi: string;
    paylasimliMi: boolean;
    kapasite: number;
    kalanKapasite: number;
    onerilenYatakNolari: number[];
}

export interface RezervasyonOdaDegisimKonaklayanDto {
    siraNo: number;
    adSoyad: string;
    mevcutYatakNo: number | null;
}

export interface RezervasyonOdaDegisimKayitDto {
    rezervasyonSegmentOdaAtamaId: number;
    segmentId: number;
    segmentSirasi: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    ayrilanKisiSayisi: number;
    mevcutOdaId: number;
    mevcutOdaNo: string;
    mevcutBinaAdi: string;
    mevcutOdaTipiAdi: string;
    mevcutOdaPaylasimliMi: boolean;
    mevcutOdaKapasitesi: number;
    problemliMi: boolean;
    tasinacakKonaklayanlar: RezervasyonOdaDegisimKonaklayanDto[];
    adayOdalar: RezervasyonOdaDegisimAdayOdaDto[];
}

export interface RezervasyonOdaDegisimSecenekDto {
    rezervasyonId: number;
    referansNo: string;
    kayitlar: RezervasyonOdaDegisimKayitDto[];
}

export interface RezervasyonOdaDegisimKaydetAtamaDto {
    rezervasyonSegmentOdaAtamaId: number;
    yeniOdaId: number;
}

export interface RezervasyonOdaDegisimKaydetRequestDto {
    atamalar: RezervasyonOdaDegisimKaydetAtamaDto[];
}

export interface RezervasyonOdemeDto {
    id: number;
    odemeTarihi: string;
    odemeTutari: number;
    paraBirimi: string;
    odemeTipi: string;
    aciklama: string | null;
}

export interface RezervasyonOdemeOzetDto {
    rezervasyonId: number;
    referansNo: string;
    konaklamaUcreti: number;
    ekHizmetToplami: number;
    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string;
    ekHizmetler: RezervasyonEkHizmetDto[];
    odemeler: RezervasyonOdemeDto[];
}

export interface RezervasyonOdemeKaydetRequestDto {
    odemeTutari: number;
    odemeTipi: string;
    aciklama: string | null;
}

export interface RezervasyonEkHizmetDto {
    id: number;
    rezervasyonKonaklayanId: number;
    ekHizmetId: number;
    ekHizmetTarifeId: number;
    konaklayanAdiSoyadi: string;
    tarifeAdi: string;
    hizmetTarihi: string;
    miktar: number;
    birimAdi: string;
    birimFiyat: number;
    toplamTutar: number;
    paraBirimi: string;
    odaNo: string;
    binaAdi: string;
    yatakNo: number | null;
    aciklama: string | null;
}

export interface RezervasyonEkHizmetMisafirSecenekDto {
    rezervasyonKonaklayanId: number;
    siraNo: number;
    adSoyad: string;
}

export interface RezervasyonEkHizmetTarifeSecenekDto {
    id: number;
    ekHizmetId: number;
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    birimFiyat: number;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    paketIcerikHizmetKodu?: string | null;
    paketIcerigiUyariMesaji: string | null;
}

export interface RezervasyonEkHizmetSecenekleriDto {
    rezervasyonId: number;
    referansNo: string;
    paketCakismaPolitikasi: string;
    misafirler: RezervasyonEkHizmetMisafirSecenekDto[];
    tarifeler: RezervasyonEkHizmetTarifeSecenekDto[];
}

export interface RezervasyonEkHizmetKaydetRequestDto {
    rezervasyonKonaklayanId: number;
    ekHizmetTarifeId: number;
    hizmetTarihi: string;
    miktar: number;
    birimFiyat: number | null;
    aciklama: string | null;
}
