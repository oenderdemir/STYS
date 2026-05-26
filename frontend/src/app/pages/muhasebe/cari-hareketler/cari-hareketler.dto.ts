export interface CariHareketModel {
    id?: number;
    cariKartId: number;
    hareketTarihi: string;
    belgeTuru: string;
    belgeNo?: string | null;
    aciklama?: string | null;
    borcTutari: number;
    alacakTutari: number;
    paraBirimi: string;
    vadeTarihi?: string | null;
    durum: string;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    kapananTutar?: number;
    kalanTutar?: number;
    iliskiliCariHareketId?: number | null;
    kapandiMi?: boolean;
}

export interface CreateCariHareketRequest extends Omit<CariHareketModel, 'id'> {}
export interface UpdateCariHareketRequest extends Omit<CariHareketModel, 'id'> {}

export interface CariEkstreModel {
    cariKartId: number;
    cariKodu: string;
    unvanAdSoyad: string;
    toplamBorc: number;
    toplamAlacak: number;
    bakiye: number;
    hareketler: CariHareketModel[];
}

export interface CariBakiyeOzetModel {
    cariKartId: number;
    cariKodu: string;
    unvanAdSoyad: string;
    toplamBorc: number;
    toplamAlacak: number;
    bakiye: number;
    bakiyeYonu: 'Borclu' | 'Alacakli' | 'Sifir';
    toplamAcikBorc: number;
    toplamAcikAlacak: number;
    acikHareketSayisi: number;
    kapananHareketSayisi: number;
}

export interface CariHareketDurumOzetModel {
    cariHareketId: number;
    hareketTarihi: string;
    belgeTuru: string;
    belgeNo?: string | null;
    borcTutari: number;
    alacakTutari: number;
    kapananTutar: number;
    kalanTutar: number;
    kapandiMi: boolean;
    kaynakModul?: string | null;
    kaynakId?: number | null;
}

export const HAREKET_DURUMLARI = [{ label: 'Aktif', value: 'Aktif' }, { label: 'Iptal', value: 'Iptal' }];

