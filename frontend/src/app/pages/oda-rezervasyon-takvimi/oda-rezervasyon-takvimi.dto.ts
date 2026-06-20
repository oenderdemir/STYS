export interface OdaRezervasyonTakvimiDto {
    tesisId: number;
    tesisAdi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    gunSayisi: number;
    gunler: OdaRezervasyonTakvimGunDto[];
    odaTipleri: OdaRezervasyonOdaTipiGrupDto[];
    ozet: OdaRezervasyonTakvimOzetDto;
}

export interface OdaRezervasyonTakvimGunDto {
    tarih: string;
    gunAdi: string;
    kisaGunAdi: string;
    gun: number;
    ay: number;
    yil: number;
    bugunMu: boolean;
    doluOdaSayisi: number;
    bosOdaSayisi: number;
    checkInSayisi: number;
    checkOutSayisi: number;
    kisiSayisi: number;
}

export interface OdaRezervasyonOdaTipiGrupDto {
    odaTipiId: number;
    odaTipiAdi: string;
    odalar: OdaRezervasyonOdaSatiriDto[];
}

export interface OdaRezervasyonOdaSatiriDto {
    odaId: number;
    odaNo: string;
    odaTipiId: number;
    odaTipiAdi: string;
    kapasite: number;
    temizlikDurumu: string | null;
    bloklar: OdaRezervasyonBlokDto[];
}

export interface OdaRezervasyonBlokDto {
    blokTipi: string;
    rezervasyonId: number | null;
    odaKullanimBlokId: number | null;
    baslik: string;
    altBaslik: string | null;
    baslangicTarihi: string;
    bitisTarihi: string;
    baslangicGunIndex: number;
    gunUzunlugu: number;
    geceSayisi: number;
    solKenaraDevamEdiyor: boolean;
    sagKenaraDevamEdiyor: boolean;
    durum: string;
    renkTipi: string;
    toplamUcret: number | null;
    odenenTutar: number | null;
    kalanTutar: number | null;
    paraBirimi: string | null;
    checkInBugunMu: boolean;
    checkOutBugunMu: boolean;
    odaDegisimiGerekli: boolean;
    odemeEksikMi: boolean;
    uyarilar: string[];
}

export interface OdaRezervasyonTakvimOzetDto {
    toplamOdaSayisi: number;
    doluOdaSayisi: number;
    bosOdaSayisi: number;
    bugunCheckInSayisi: number;
    bugunCheckOutSayisi: number;
    yarinCheckInSayisi: number;
    bugunKisiSayisi: number;
    yarinGelecekKisiSayisi: number;
    donemToplamGelir: number;
    paraBirimi: string;
}
