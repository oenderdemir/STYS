export interface OdaTemizlikTesisDto {
    id: number;
    ad: string;
}

export interface OdaTemizlikKayitDto {
    odaId: number;
    tesisId: number;
    tesisAdi: string;
    binaId: number;
    binaAdi: string;
    odaNo: string;
    odaTipiId: number;
    odaTipiAdi: string;
    temizlikDurumu: string;
}
