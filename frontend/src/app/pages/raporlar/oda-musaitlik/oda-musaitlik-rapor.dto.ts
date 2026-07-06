export interface OdaMusaitlikOzetDto {
    toplamOdaSayisi: number;
    tamamenBosOdaSayisi: number;
    tamamenDoluOdaSayisi: number;
    kismenMusaitOdaSayisi: number;

    toplamGunSayisi: number;
    toplamOdaGunSayisi: number;
    bosOdaGunSayisi: number;
    doluOdaGunSayisi: number;
    musaitlikOrani: number;
}

export interface OdaMusaitlikGunDto {
    tarih: string;
    gunAdi: string;
    bosMu: boolean;
    doluMu: boolean;
    rezervasyonId: number | null;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;
    rezervasyonDurumu: string | null;
    rezervasyonDurumuLabel: string | null;
}

export interface OdaMusaitlikOdaDto {
    odaId: number;
    odaNo: string;
    binaAdi: string | null;
    odaTipiAdi: string | null;
    kapasite: number;

    musaitlikDurumu: string;
    musaitlikDurumuLabel: string;

    toplamGunSayisi: number;
    bosGunSayisi: number;
    doluGunSayisi: number;
    musaitlikOrani: number;

    gunler: OdaMusaitlikGunDto[];
}

export interface OdaMusaitlikRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    baslangic: string;
    bitis: string;
    durum: string;
    odaTipiId: number | null;
    odaTipiAdi: string | null;
    kapasite: number | null;
    baslik: string;

    ozet: OdaMusaitlikOzetDto;
    odalar: OdaMusaitlikOdaDto[];
}
