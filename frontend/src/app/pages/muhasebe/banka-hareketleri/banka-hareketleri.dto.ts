export interface BankaHareketModel {
    id?: number;
    bankaAdi: string;
    hesapKoduIban: string;
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

export interface CreateBankaHareketRequest extends Omit<BankaHareketModel, 'id'> {}
export interface UpdateBankaHareketRequest extends Omit<BankaHareketModel, 'id'> {}

