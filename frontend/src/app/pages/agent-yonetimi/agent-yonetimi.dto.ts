export interface AgentListDto {
    id: number;
    ad: string;
    agentKey: string;
    kurumId: number;
    kurumAd?: string;
    durum: number;
    agentVersion?: string;
    contractVersion?: string;
    minimumSupportedAgentVersion?: string;
    recommendedAgentVersion?: string;
    supportedContractVersion?: string;
    compatibilityStatus: number;
    sonGorulmeTarihi?: string;
    lastHeartbeatAt?: string;
    onlineMi: boolean;
    createdAt: string;
}

export interface AgentDto extends AgentListDto {
    cihazKimligi?: string;
    tesisIds: number[];
    scopes: string[];
}

export interface AgentKaydetRequest {
    ad: string;
    tesisIds: number[];
    scopes: string[];
}

export interface AgentEnrollmentCodeDto {
    id: number;
    /** Plaintext code. Present only in the response that creates it; null on every later read. */
    code: string | null;
    /** Non-secret prefix used to identify a code in listings. */
    codePrefix: string;
    kurumId: number;
    kurumAd?: string;
    tesisIds: number[];
    allowedScopes: string[];
    kullanimSayisi: number;
    maxKullanimSayisi: number;
    expiresAt: string;
    durum: number;
    agentId?: number;
    createdAt: string;
}

export interface AgentEnrollmentCodeRequest {
    tesisIds: number[];
    allowedScopes: string[];
    maxKullanimSayisi?: number;
    expirationHours?: number;
    requiresApproval?: boolean;
}

export interface AgentInstallationSessionCreateRequest {
    tesisId: number;
    agentDisplayName: string;
    targetRid: string;
    scopes: string[];
    requiresApproval: boolean;
    expirationHours?: number;
}

export interface AgentInstallationSessionCreateResponse {
    session: AgentInstallationSessionModel;
    enrollmentCode: string;
}

export interface AgentInstallationSessionModel {
    id: number;
    kurumId: number;
    tesisId: number;
    tesisAd?: string;
    agentDisplayName: string;
    targetRid: string;
    scopes: string[];
    status: number;
    enrollmentId?: number;
    enrolledAgentId?: number;
    expiresAt: string;
    completedAt?: string | null;
    cancelledAt?: string | null;
    createdAt: string;
    updatedAt?: string | null;
}

export const AgentDurumLabels: Record<number, string> = {
    0: 'Onay Bekliyor',
    1: 'Aktif',
    2: 'Devre Dışı',
    3: 'İptal Edildi',
    4: 'Reddedildi'
};

export const AgentCompatibilityStatusLabels: Record<number, string> = {
    0: 'Bilinmiyor',
    1: 'Destekleniyor',
    2: 'Güncelleme Var',
    3: 'Güncelleme Gerekli',
    4: 'Sözleşme Uyuşmuyor'
};

export const AgentEnrollmentDurumLabels: Record<number, string> = {
    0: 'Aktif',
    1: 'Kullanıldı',
    2: 'Süresi Doldu',
    3: 'İptal Edildi'
};

export const AgentInstallationSessionStatusLabels: Record<number, string> = {
    0: 'Oluşturuldu',
    1: 'Paket Hazır',
    2: 'Paket İndirildi',
    3: 'Enrollment Bekleniyor',
    4: 'Onay Bekleniyor',
    5: 'Kayıt Tamamlandı',
    6: 'Online',
    7: 'Tamamlandı',
    8: 'Süresi Doldu',
    9: 'İptal Edildi',
    10: 'Başarısız'
};

export interface AgentCommandDto {
    id: string;
    agentId: number;
    commandType: string;
    payload?: string;
    status: number;
    priority: number;
    scheduledAt?: string;
    expiresAt?: string;
    retryCount: number;
    maxRetryCount: number;
    correlationId: string;
    idempotencyKey: string;
    resultPayload?: string;
    createdAt: string;
}

export interface AgentCommandSendRequest {
    agentId: number;
    commandType: string;
    payload?: string;
    priority: number;
    expirationMinutes?: number;
    maxRetryCount?: number;
}
