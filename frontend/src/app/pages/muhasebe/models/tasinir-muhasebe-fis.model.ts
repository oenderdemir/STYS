export interface TasinirMuhasebeFisiOlusturRequestModel {
    tesisId: number | null;
    maliYil: number | null;
    donem: number | null;
    fisTarihi: string;
    tasinirKodu: string;
    tutar: number | null;
    alacakHesapKodu: string | null;
    aciklama: string | null;
    belgeNo: string | null;
    referansTipi: string | null;
    referansId: string | null;
    kdvOrani: number | null;
    kdvHesapKodu: string | null;
    kdvDahilMi: boolean;
}

export function createDefaultTasinirFisRequest(): TasinirMuhasebeFisiOlusturRequestModel {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, '0');
    const dd = String(today.getDate()).padStart(2, '0');
    return {
        tesisId: null,
        maliYil: yyyy,
        donem: null,
        fisTarihi: `${yyyy}-${mm}-${dd}`,
        tasinirKodu: '',
        tutar: null,
        alacakHesapKodu: null,
        aciklama: null,
        belgeNo: null,
        referansTipi: null,
        referansId: null,
        kdvOrani: null,
        kdvHesapKodu: null,
        kdvDahilMi: false
    };
}

export interface TasinirMuhasebeFisiOlusturResultModel {
    muhasebeFisId: number;
    fisNo: string;
    durum: string;
    borcHesapKodu: string;
    borcHesapAdi: string;
    alacakHesapKodu: string;
    alacakHesapAdi: string;
    toplamBorc: number;
    toplamAlacak: number;
    mesaj: string;
    matrah: number;
    kdvTutari: number;
    genelToplam: number;
    kdvHesapKodu: string | null;
    kdvHesapAdi: string | null;
}
