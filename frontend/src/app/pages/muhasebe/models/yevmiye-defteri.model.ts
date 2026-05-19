export interface YevmiyeDefteriSatirModel {
    fisId: number;
    fisNo: string;
    yevmiyeNo: number | null;
    fisTarihi: string | null;
    fisTipi: string;
    durum: string;
    siraNo: number;
    muhasebeHesapPlaniId: number;
    muhasebeHesapKodu: string;
    muhasebeHesapAdi: string;
    borc: number;
    alacak: number;
    satirAciklama: string;
    fisAciklama: string;
    kaynakModul: string | null;
    kaynakId: number | null;
}

export interface YevmiyeDefteriModel {
    satirlar: YevmiyeDefteriSatirModel[];
    toplamBorc: number;
    toplamAlacak: number;
}
