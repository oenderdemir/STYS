export interface MusteriMenuModel {
    restoran: MusteriRestoranOzetModel;
    kategoriler: MusteriMenuKategoriModel[];
}

export interface MusteriRestoranOzetModel {
    id: number;
    ad: string;
    aciklama?: string | null;
}

export interface MusteriMenuKategoriModel {
    id: number;
    ad: string;
    siraNo: number;
    urunler: MusteriMenuUrunModel[];
}

export interface MusteriMenuUrunModel {
    id: number;
    kategoriId: number;
    ad: string;
    aciklama?: string | null;
    fiyat: number;
    paraBirimi: string;
    hazirlamaSuresiDakika: number;
}
