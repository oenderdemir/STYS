export interface OdaTipiDolulukOzetDto {
    toplamOdaTipiSayisi: number;
    toplamOdaSayisi: number;
    toplamKapasite: number;
    toplamGunSayisi: number;
    toplamOdaGunSayisi: number;
    doluOdaGunSayisi: number;
    bosOdaGunSayisi: number;
    dolulukOrani: number;
    musaitlikOrani: number;
}

export interface OdaTipiDolulukOdaDto {
    odaId: number;
    odaNo: string;
    binaAdi: string | null;
    kapasite: number;

    toplamGunSayisi: number;
    doluGunSayisi: number;
    bosGunSayisi: number;
    dolulukOrani: number;
}

export interface OdaTipiDolulukSatirDto {
    odaTipiId: number;
    odaTipiAdi: string;
    odaSayisi: number;
    toplamKapasite: number;

    toplamGunSayisi: number;
    toplamOdaGunSayisi: number;
    doluOdaGunSayisi: number;
    bosOdaGunSayisi: number;

    dolulukOrani: number;
    musaitlikOrani: number;

    toplamRezervasyonSayisi: number;
    toplamKonaklayanKisiSayisi: number;
    toplamKisiGeceSayisi: number;

    odalar: OdaTipiDolulukOdaDto[];
}

export interface OdaTipiDolulukRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    baslangic: string;
    bitis: string;
    odaTipiId: number | null;
    odaTipiAdi: string | null;
    baslik: string;

    ozet: OdaTipiDolulukOzetDto;
    odaTipleri: OdaTipiDolulukSatirDto[];
}
