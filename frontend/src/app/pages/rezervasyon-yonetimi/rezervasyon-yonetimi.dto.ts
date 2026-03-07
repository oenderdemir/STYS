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
    baslangicTarihi: string;
    bitisTarihi: string;
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
    girisTarihi: string;
    cikisTarihi: string;
    misafirAdiSoyadi: string;
    misafirTelefon: string;
    misafirEposta: string | null;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
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

export interface RezervasyonListeDto {
    id: number;
    referansNo: string;
    tesisId: number;
    misafirAdiSoyadi: string;
    misafirTelefon: string;
    misafirEposta: string | null;
    tcKimlikNo: string | null;
    pasaportNo: string | null;
    kisiSayisi: number;
    girisTarihi: string;
    cikisTarihi: string;
    toplamUcret: number;
    paraBirimi: string;
    rezervasyonDurumu: string;
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
    kisiSayisi: number;
    girisTarihi: string;
    cikisTarihi: string;
    toplamBazUcret: number;
    toplamUcret: number;
    paraBirimi: string;
    uygulananIndirimler: UygulananIndirimDto[];
    segmentler: RezervasyonDetaySegmentDto[];
}
