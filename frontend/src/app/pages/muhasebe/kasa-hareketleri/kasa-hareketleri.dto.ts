export interface KasaHareketModel {
    id?: number;
    kasaKodu: string;
    kasaBankaHesapId?: number | null;
    hareketTarihi: string;
    hareketTipi: string;
    tutar: number;
    paraBirimi: string;
    aciklama?: string | null;
    belgeNo?: string | null;
    cariKartId?: number | null;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    durum: string;
}

export interface CreateKasaHareketRequest extends Omit<KasaHareketModel, 'id'> {}
export interface UpdateKasaHareketRequest extends Omit<KasaHareketModel, 'id'> {}

export const KASA_HAREKET_TIPLERI = [
    { label: 'Tahsilat', value: 'Tahsilat' },
    { label: 'Odeme', value: 'Odeme' },
    { label: 'Devir', value: 'Devir' },
    { label: 'Duzeltme', value: 'Duzeltme' }
];

