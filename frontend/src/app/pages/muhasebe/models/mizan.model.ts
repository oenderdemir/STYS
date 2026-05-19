export interface MizanFilterModel {
    tesisId: number | null;
    maliYil: number | null;
    donem: number | null;
    hesapKoduBaslangic: string | null;
    hesapKoduBitis: string | null;
    sadeceHareketGorenHesaplar: boolean;
    altHesaplariDahilEt: boolean;
    page: number;
    pageSize: number;
}

export function createDefaultMizanFilter(): MizanFilterModel {
    const currentYear = new Date().getFullYear();
    return {
        tesisId: null,
        maliYil: currentYear,
        donem: null,
        hesapKoduBaslangic: null,
        hesapKoduBitis: null,
        sadeceHareketGorenHesaplar: true,
        altHesaplariDahilEt: true,
        page: 1,
        pageSize: 500
    };
}

export function normalizeMizanFilter(filter: MizanFilterModel): MizanFilterModel {
    const normalized = { ...filter };
    if (!normalized.hesapKoduBaslangic?.trim()) {
        normalized.hesapKoduBaslangic = null;
    }
    if (!normalized.hesapKoduBitis?.trim()) {
        normalized.hesapKoduBitis = null;
    }
    if (normalized.page < 1) {
        normalized.page = 1;
    }
    if (normalized.pageSize < 1) {
        normalized.pageSize = 500;
    }
    return normalized;
}

export interface MizanSatirModel {
    muhasebeHesapPlaniId: number;
    hesapKodu: string;
    hesapAdi: string;
    detayHesapMi: boolean;
    hareketGorebilirMi: boolean;
    toplamBorc: number;
    toplamAlacak: number;
    borcBakiye: number;
    alacakBakiye: number;
    bakiye: number;
    bakiyeTipi: string; // 'Borc' | 'Alacak' | 'Sifir'
    konsolideSatirMi: boolean;
    seviye: number;
}

export interface MizanModel {
    tesisId: number;
    genelToplamBorc: number;
    genelToplamAlacak: number;
    genelBorcBakiye: number;
    genelAlacakBakiye: number;
    satirlar: MizanSatirModel[];
}

export interface MizanKarsilastirmaModel {
    tesisId: number;
    maliYil?: number | null;
    donem?: number | null;

    eskiGenelToplamBorc: number;
    hizliGenelToplamBorc: number;
    genelToplamBorcFark: number;

    eskiGenelToplamAlacak: number;
    hizliGenelToplamAlacak: number;
    genelToplamAlacakFark: number;

    eskiGenelBorcBakiye: number;
    hizliGenelBorcBakiye: number;
    genelBorcBakiyeFark: number;

    eskiGenelAlacakBakiye: number;
    hizliGenelAlacakBakiye: number;
    genelAlacakBakiyeFark: number;

    eskiSatirSayisi: number;
    hizliSatirSayisi: number;
    farkliSatirSayisi: number;

    eslesiyorMu: boolean;

    farklar: MizanKarsilastirmaSatirModel[];
}

export interface MizanKarsilastirmaSatirModel {
    hesapKodu: string;
    hesapAdi: string;

    eskiMizandaVarMi: boolean;
    hizliMizandaVarMi: boolean;

    eskiToplamBorc: number;
    hizliToplamBorc: number;
    toplamBorcFark: number;

    eskiToplamAlacak: number;
    hizliToplamAlacak: number;
    toplamAlacakFark: number;

    eskiBorcBakiye: number;
    hizliBorcBakiye: number;
    borcBakiyeFark: number;

    eskiAlacakBakiye: number;
    hizliAlacakBakiye: number;
    alacakBakiyeFark: number;

    farkTipi: string;
}
