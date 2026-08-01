import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../core/api/api-response.model';
import {
    TicariBelgeDetayDto,
    TicariBelgeDto,
    TicariBelgeFilterDto,
    TicariBelgeGuncelleRequest
} from './ticari-belge.models';

@Injectable({ providedIn: 'root' })
export class TicariBelgeService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly base = `${this.apiBaseUrl}/ui/ticari-belgeler`;

    getById(id: number): Observable<TicariBelgeDetayDto> {
        return this.http
            .get<ApiResponse<TicariBelgeDetayDto>>(`${this.base}/${id}`)
            .pipe(map(envelope => this.unwrap(envelope)));
    }

    filter(filter: TicariBelgeFilterDto): Observable<TicariBelgeDto[]> {
        return this.http
            .post<ApiResponse<TicariBelgeDto[]>>(`${this.base}/filter`, filter)
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    update(id: number, request: TicariBelgeGuncelleRequest): Observable<TicariBelgeDetayDto> {
        return this.http
            .put<ApiResponse<TicariBelgeDetayDto>>(`${this.base}/${id}`, request)
            .pipe(map(envelope => this.unwrap(envelope)));
    }

    delete(id: number): Observable<void> {
        return this.http
            .delete<ApiResponse<void>>(`${this.base}/${id}`)
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'Silme başarısız.'); }));
    }

    muhasebeOnayinaGonder(id: number): Observable<void> {
        return this.http
            .post<ApiResponse<void>>(`${this.base}/${id}/muhasebe-onayina-gonder`, {})
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.'); }));
    }

    iptalEt(id: number): Observable<void> {
        return this.http
            .post<ApiResponse<void>>(`${this.base}/${id}/iptal`, {})
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.'); }));
    }

    private unwrap<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
