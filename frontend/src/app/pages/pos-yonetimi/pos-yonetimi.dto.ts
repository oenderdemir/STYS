export interface PosCihaziDto {
    id: number;
    kurumId: number;
    tesisId: number;
    tesisAd?: string;
    agentId?: number;
    agentAd?: string;
    saglayici: number;
    saglayiciAd: string;
    ad: string;
    seriNo: string;
    ipAdresi?: string;
    httpPort?: number;
    httpsPort?: number;
    fingerprint?: string;
    eslesmeOnayliMi: boolean;
    aktifMi: boolean;
    sonBaglantiTarihi?: string;
    aciklama?: string;
    terminalSayisi: number;
}

export interface PosCihaziKaydetRequest {
    tesisId: number;
    agentId?: number;
    saglayici: number;
    ad: string;
    seriNo: string;
    ipAdresi?: string;
    httpPort?: number;
    httpsPort?: number;
    fingerprint?: string;
    aciklama?: string;
}

export const SaglayiciLabels: Record<number, string> = { 0: 'PAVO', 1: 'Diğer' };
