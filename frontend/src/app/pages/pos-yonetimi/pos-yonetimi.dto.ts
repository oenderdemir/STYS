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
    targetFingerprint?: string;
    pairingId?: number | null;
    pairingCode?: string | null;
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

export interface PosSaglayiciDto {
    kod: string;
    ad: string;
    eslesmeDestekliyorMu: boolean;
}

export interface PosTerminalDto {
    id: number;
    kurumId: number;
    tesisId: number;
    tesisAd?: string | null;
    posCihaziId?: number | null;
    posCihaziAd?: string | null;
    kasaBankaHesapId: number;
    kasaBankaHesapAd?: string | null;
    saglayiciKodu: string;
    ad: string;
    terminalId: string;
    merchantId?: string | null;
    serialNumber: string;
    sourceFingerprint?: string | null;
    sourceTerminalReference?: string | null;
    eslesmeOnayliMi: boolean;
    aktifMi: boolean;
    pairingId?: number | null;
    pairingCode?: string | null;
}

export interface PosTerminalKaydetRequest {
    posCihaziId?: number | null;
    kasaBankaHesapId: number;
    saglayiciKodu: string;
    ad: string;
    terminalId: string;
    merchantId?: string | null;
    serialNumber: string;
    sourceFingerprint?: string | null;
    sourceTerminalReference?: string | null;
    aktifMi: boolean;
}

export const SaglayiciLabels: Record<number, string> = { 0: 'PAVO', 1: 'Diğer' };
