import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    AgentDto,
    AgentEnrollmentCodeDto,
    AgentEnrollmentCodeRequest,
    AgentListDto,
    AgentKaydetRequest,
    AgentCommandDto,
    AgentCommandSendRequest
} from './agent-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class AgentYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAgents(kurumId?: number | null, tesisId?: number | null): Observable<AgentListDto[]> {
        let params = new HttpParams();
        if (kurumId != null && kurumId > 0) {
            params = params.set('kurumId', kurumId);
        }
        if (tesisId != null && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<AgentListDto[]>>(`${this.apiBaseUrl}/ui/agent`, { params }).pipe(
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

    getEnrollmentCodes(kurumId?: number | null, tesisId?: number | null): Observable<AgentEnrollmentCodeDto[]> {
        let params = new HttpParams();
        if (kurumId != null && kurumId > 0) {
            params = params.set('kurumId', kurumId);
        }
        if (tesisId != null && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<AgentEnrollmentCodeDto[]>>(`${this.apiBaseUrl}/ui/agent/enrollment-codes`, { params }).pipe(
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

    getCommands(agentId: number): Observable<AgentCommandDto[]> {
        return this.http.get<ApiResponse<AgentCommandDto[]>>(`${this.apiBaseUrl}/ui/agent/${agentId}/commands`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Komut listesi alınamadı.');
            })
        );
    }

    sendCommand(agentId: number, request: AgentCommandSendRequest): Observable<AgentCommandDto> {
        return this.http.post<ApiResponse<AgentCommandDto>>(`${this.apiBaseUrl}/ui/agent/${agentId}/commands`, request).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Komut gönderilemedi.');
            })
        );
    }
}
