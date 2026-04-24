export type MalzemeKayitTipi =
    | 'MalzemeleriAyriKayittaTut'
    | 'FiyatFarkliMalzemeleriAyriKayittaTut'
    | 'MalzemeleriAyniKayittaTut';

export interface DepoCikisGrupModel {
    id?: number;
    depoId?: number;
    cikisGrupAdi: string;
    karOrani: number;
    lokasyonId?: number | null;
}

export interface DepoModel {
    id?: number;
    tesisId?: number | null;
    ustDepoId?: number | null;
    muhasebeHesapPlaniId?: number | null;
    kod: string;
    ad: string;
    malzemeKayitTipi: MalzemeKayitTipi;
    satisFiyatlariniGoster: boolean;
    avansGenel: boolean;
    aktifMi: boolean;
    aciklama?: string | null;
    cikisGruplari: DepoCikisGrupModel[];
}

export interface CreateDepoRequest extends Omit<DepoModel, 'id'> {}
export interface UpdateDepoRequest extends Omit<DepoModel, 'id'> {}

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

export interface MuhasebeHesapLookupModel {
    id: number;
    kod: string;
    ad: string;
}

export const MALZEME_KAYIT_TIPI_OPTIONS: Array<{ label: string; value: MalzemeKayitTipi }> = [
    { label: 'Malzemeleri Ayri Kayitta Tut', value: 'MalzemeleriAyriKayittaTut' },
    { label: 'Fiyat Farkli Malzemeleri Ayri Kayitta Tut', value: 'FiyatFarkliMalzemeleriAyriKayittaTut' },
    { label: 'Malzemeleri Ayni Kayitta Tut', value: 'MalzemeleriAyniKayittaTut' }
];
