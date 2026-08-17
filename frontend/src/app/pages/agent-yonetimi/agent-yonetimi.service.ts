import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    AgentDto,
    AgentEnrollmentCodeDto,
    AgentEnrollmentCodeRequest,
    AgentInstallationSessionCreateRequest,
    AgentInstallationSessionCreateResponse,
    AgentInstallationSessionModel,
    AgentListDto,
    AgentKaydetRequest,
    AgentCommandDto,
    AgentCommandSendRequest,
    AgentReleaseDto,
    AgentReleasePublishForm
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

    getEnrollmentPolicy(): Observable<{ kurumId: number; requiresApproval: boolean }> {
        return this.http.get<ApiResponse<{ kurumId: number; requiresApproval: boolean }>>(`${this.apiBaseUrl}/ui/agent/enrollment-policy`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Kurum enrollment politikası alınamadı.');
            })
        );
    }

    rejectAgent(id: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent/${id}/reject`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Agent reddedilemedi.');
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

    getInstallations(): Observable<AgentInstallationSessionModel[]> {
        return this.http.get<ApiResponse<AgentInstallationSessionModel[]>>(`${this.apiBaseUrl}/ui/agent-installations`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Kurulum oturumları alınamadı.');
            })
        );
    }

    getInstallation(id: number): Observable<AgentInstallationSessionModel> {
        return this.http.get<ApiResponse<AgentInstallationSessionModel>>(`${this.apiBaseUrl}/ui/agent-installations/${id}`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Kurulum oturumu alınamadı.');
            })
        );
    }

    createInstallation(request: AgentInstallationSessionCreateRequest): Observable<AgentInstallationSessionCreateResponse> {
        return this.http.post<ApiResponse<AgentInstallationSessionCreateResponse>>(`${this.apiBaseUrl}/ui/agent-installations`, request).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Kurulum oturumu oluşturulamadı.');
            })
        );
    }

    downloadInstallationPackage(id: number): Observable<Blob> {
        return this.http.get(`${this.apiBaseUrl}/ui/agent-installations/${id}/package`, { responseType: 'blob' });
    }

    cancelInstallation(id: number): Observable<void> {
        return this.http.post<ApiResponse<void>>(`${this.apiBaseUrl}/ui/agent-installations/${id}/cancel`, {}).pipe(
            map((r) => {
                if (!r.success) throw new Error(tryReadApiMessage(r) ?? 'Kurulum oturumu iptal edilemedi.');
            })
        );
    }

    getReleases(): Observable<AgentReleaseDto[]> {
        return this.http.get<ApiResponse<AgentReleaseDto[]>>(`${this.apiBaseUrl}/ui/agent-releases`).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Sürüm listesi alınamadı.');
            })
        );
    }

    /**
     * Uploads the package as multipart/form-data. SHA-256 and package size are computed by the
     * server from the uploaded bytes, so they are intentionally not sent from here.
     */
    publishRelease(form: AgentReleasePublishForm, packageFile: File): Observable<AgentReleaseDto> {
        const body = new FormData();
        body.append('Version', form.version);
        body.append('ContractVersion', form.contractVersion);
        body.append('RuntimeIdentifier', form.runtimeIdentifier);
        body.append('ReleaseNotes', form.releaseNotes ?? '');
        body.append('Enabled', String(form.enabled));
        body.append('package', packageFile, packageFile.name);

        return this.http.post<ApiResponse<AgentReleaseDto>>(`${this.apiBaseUrl}/ui/agent-releases`, body).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Sürüm yayınlanamadı.');
            })
        );
    }

    setReleaseEnabled(releaseId: number, enabled: boolean): Observable<AgentReleaseDto> {
        const action = enabled ? 'enable' : 'disable';
        return this.http.post<ApiResponse<AgentReleaseDto>>(`${this.apiBaseUrl}/ui/agent-releases/${releaseId}/${action}`, {}).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Sürüm durumu değiştirilemedi.');
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

    stageUpgrade(agentId: number): Observable<AgentCommandDto> {
        return this.http.post<ApiResponse<AgentCommandDto>>(`${this.apiBaseUrl}/ui/agent/${agentId}/stage-upgrade`, {}).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Güncelleme hazırlanamadı.');
            })
        );
    }

    applyUpgrade(agentId: number): Observable<AgentCommandDto> {
        return this.http.post<ApiResponse<AgentCommandDto>>(`${this.apiBaseUrl}/ui/agent/${agentId}/apply-upgrade`, {}).pipe(
            map((r) => {
                if (r.success && r.data) return r.data;
                throw new Error(tryReadApiMessage(r) ?? 'Güncelleme uygulanamadı.');
            })
        );
    }
}
