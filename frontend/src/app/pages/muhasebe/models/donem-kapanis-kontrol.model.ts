export interface DonemKapanisKontrolFilterModel {
    tesisId: number;
    maliYil: number;
    donemNo: number;
}

export interface DonemKapanisKontrolModel {
    donemId?: number | null;
    tesisId: number;
    maliYil: number;
    donemNo: number;
    donemVarMi: boolean;
    donemKapaliMi: boolean;
    kapatilabilirMi: boolean;
    taslakFisSayisi: number;
    dengesizTaslakFisSayisi: number;
    onayliFisSayisi: number;
    iptalFisSayisi: number;
    tersKayitFisSayisi: number;
    yevmiyeNoEksikOnayliFisSayisi: number;
    dengesizOnayliFisSayisi: number;
    toplamBorc: number;
    toplamAlacak: number;
    fark: number;
    maddeler: DonemKapanisKontrolMaddeModel[];
    problemliFisler: DonemKapanisKontrolFisOzetModel[];
}

export interface DonemKapanisKontrolMaddeModel {
    kod: string;
    baslik: string;
    mesaj: string;
    severity: string;
    basariliMi: boolean;
    bloklayiciMi: boolean;
    route?: string;
}

export interface DonemKapanisKontrolFisOzetModel {
    id: number;
    fisNo: string;
    yevmiyeNo?: number;
    fisTarihi: string;
    fisTipi: string;
    durum: string;
    toplamBorc: number;
    toplamAlacak: number;
    problemTipi: string;
}

export function createDefaultKapanisFilter(): DonemKapanisKontrolFilterModel {
    return {
        tesisId: 0,
        maliYil: new Date().getFullYear(),
        donemNo: new Date().getMonth() + 1
    };
}
