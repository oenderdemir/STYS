import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    AgentDto,
    AgentEnrollmentCodeDto,
    AgentEnrollmentCodeRequest,
    AgentListDto,
    AgentKaydetRequest
} from './agent-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class AgentYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAgents(): Observable<AgentListDto[]> {
        return this.http.get<ApiResponse<AgentListDto[]>>(`${this.apiBaseUrl}/ui/agent`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Agent listesi alınamadı.');
            })
        );
    }

    getAgent(id: number): Observable<AgentDto> {
        return this.http.get<ApiResponse<AgentDto>>(`${this.apiBaseUrl}/ui/agent/${id}`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Agent bilgisi alınamadı.');
            })
        );
    }

    createAgent(request: AgentKaydetRequest): Observable<AgentDto> {
        return this.http.post<ApiResponse<AgentDto>>(`${this.apiBaseUrl}/ui/agent`, request).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Agent oluşturulamadı.');
            })
        );
    }

    updateAgent(id: number, request: AgentKaydetRequest): Observable<AgentDto> {
        return this.http.put<ApiResponse<AgentDto>>(`${this.apiBaseUrl}/ui/agent/${id}`, request).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Agent güncellenemedi.');
            })
        );
    }

    approveAgent(id: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent/${id}/approve`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Agent onaylanamadı.');
            })
        );
    }

    disableAgent(id: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent/${id}/disable`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Agent devre dışı bırakılamadı.');
            })
        );
    }

    revokeAgent(id: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent/${id}/revoke`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Agent iptal edilemedi.');
            })
        );
    }

    getEnrollmentCodes(): Observable<AgentEnrollmentCodeDto[]> {
        return this.http.get<ApiResponse<AgentEnrollmentCodeDto[]>>(`${this.apiBaseUrl}/ui/agent/enrollment-codes`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Enrollment kodları alınamadı.');
            })
        );
    }

    generateEnrollmentCode(request: AgentEnrollmentCodeRequest): Observable<AgentEnrollmentCodeDto> {
        return this.http.post<ApiResponse<AgentEnrollmentCodeDto>>(`${this.apiBaseUrl}/ui/agent/enrollment-codes`, request).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Enrollment kodu oluşturulamadı.');
            })
        );
    }

    revokeEnrollmentCode(enrollmentId: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent/enrollment-codes/${enrollmentId}/revoke`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Enrollment kodu iptal edilemedi.');
            })
        );
    }
}
