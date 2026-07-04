export interface KonaklamaKisiSayisiOdaDto {
    odaId: number;
    odaNo: string;
    odaTipiAdi: string | null;
    kapasite: number;
}

export interface KonaklamaKisiSayisiHucreDto {
    odaId: number;
    odaNo: string;
    kisiSayisi: number;
}

export interface KonaklamaKisiSayisiYilSatiriDto {
    yil: number;
    hucreler: KonaklamaKisiSayisiHucreDto[];
    toplamKisiSayisi: number;
}

export interface KonaklamaKisiSayisiRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    ay: number;
    ayAdi: string;
    baslangicYil: number;
    bitisYil: number;
    baslik: string;
    odalar: KonaklamaKisiSayisiOdaDto[];
    yillar: KonaklamaKisiSayisiYilSatiriDto[];
}
