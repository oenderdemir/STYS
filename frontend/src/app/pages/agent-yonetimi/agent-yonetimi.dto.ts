export interface AgentListDto {
    id: number;
    ad: string;
    agentKey: string;
    kurumId: number;
    kurumAd?: string;
    durum: number;
    agentVersion?: string;
    sonGorulmeTarihi?: string;
    createdAt: string;
}

export interface AgentDto extends AgentListDto {
    cihazKimligi?: string;
    tesisIds: number[];
    scopes: string[];
}

export interface AgentKaydetRequest {
    ad: string;
    kurumId: number;
    tesisIds: number[];
    scopes: string[];
}

export interface AgentEnrollmentCodeDto {
    id: number;
    code: string;
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
    kurumId: number;
    tesisIds: number[];
    allowedScopes: string[];
    maxKullanimSayisi?: number;
    expirationHours?: number;
    requiresApproval?: boolean;
}

export const AgentDurumLabels: Record<number, string> = {
    0: 'Onay Bekliyor',
    1: 'Aktif',
    2: 'Devre Dışı',
    3: 'İptal Edildi'
};

export const AgentEnrollmentDurumLabels: Record<number, string> = {
    0: 'Aktif',
    1: 'Kullanıldı',
    2: 'Süresi Doldu',
    3: 'İptal Edildi'
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
