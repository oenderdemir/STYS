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
    transactionSequence: number;
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
    kasaBankaHesapId?: number | null;
    kasaBankaHesapAd?: string | null;
    saglayiciKodu: string;
    acquirerId?: string | null;
    acquirerName?: string | null;
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

export type PavoOperationalReadiness =
    | 'Ready'
    | 'AgentOffline'
    | 'DeviceOffline'
    | 'NotProvisioned'
    | 'ReProvisionRequired'
    | 'PairingInvalid'
    | 'NoActiveTerminal'
    | 'NoAccountMapping'
    | 'Disabled'
    | 'OwnershipConflict';

export interface PosTerminalOperationalReadinessDto {
    id: number;
    terminalId: string;
    acquirerId?: string | null;
    acquirerName?: string | null;
    active: boolean;
    kasaBankaHesapId?: number | null;
    accountMapped: boolean;
    paymentReady: boolean;
    statusMessage?: string | null;
}

export interface PosOperationalReadinessDto {
    posCihaziId: number;
    status: PavoOperationalReadiness;
    ready: boolean;
    agentOnline: boolean;
    deviceOnline: boolean;
    provisioned: boolean;
    inSync: boolean;
    pairingValid: boolean;
    hasActiveTerminal: boolean;
    hasAccountMapping: boolean;
    disabled: boolean;
    ownershipConflict: boolean;
    agentLastHeartbeatAt?: string | null;
    deviceLastConnectionAt?: string | null;
    lastError?: string | null;
    activeTerminalCount: number;
    accountMappedTerminalCount: number;
    terminals: PosTerminalOperationalReadinessDto[];
    reasons: string[];
}

export interface PosTerminalKaydetRequest {
    posCihaziId?: number | null;
    kasaBankaHesapId?: number | null;
    saglayiciKodu: string;
    ad: string;
    terminalId: string;
    merchantId?: string | null;
    serialNumber: string;
    sourceFingerprint?: string | null;
    sourceTerminalReference?: string | null;
    aktifMi: boolean;
}

export interface PosPaymentBaslatRequestDto {
    posTerminalId: number;
    tutar: number;
    paraBirimi?: string | null;
    aciklama?: string | null;
    posOdemeIslemiId?: number | null;
    idempotencyKey: string;
}

export interface PosOdemeIslemiDto {
    id: number;
    posCihaziId?: number | null;
    rezervasyonId: number;
    posTerminalId: number;
    kasaBankaHesapId: number;
    agentCommandId?: string | null;
    saglayiciKodu: string;
    saglayiciIslemId?: string | null;
    saglayiciDurumKodu?: string | null;
    islemReferansi: string;
    saleReference?: string | null;
    tutar: number;
    paraBirimi: string;
    durum: string;
    pavoResultCode?: string | null;
    pavoMessage?: string | null;
    hataMesaji?: string | null;
    acquirerId?: string | null;
    terminalId?: string | null;
    merchantId?: string | null;
    retrievalReferenceNo?: string | null;
    acquirerReference?: string | null;
    authorizationCode?: string | null;
    baslatilmaTarihi?: string | null;
    tamamlanmaTarihi?: string | null;
    sonSorgulamaTarihi?: string | null;
    sorgulamaDenemeSayisi: number;
    rezervasyonOdemeId?: number | null;
    tamamlandiMi: boolean;
}

export const SaglayiciLabels: Record<number, string> = { 0: 'PAVO', 1: 'Diğer' };
