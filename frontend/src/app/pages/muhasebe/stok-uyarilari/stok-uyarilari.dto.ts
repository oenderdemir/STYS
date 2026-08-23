export interface StokUyariModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    mevcutMiktar: number;
    minimumStokMiktari?: number | null;
    kritikStokMiktari?: number | null;
    durum: string;
}

