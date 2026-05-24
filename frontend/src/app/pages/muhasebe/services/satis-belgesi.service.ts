import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../../core/api/api-response.model';
import {
    SatisBelgesiDto,
    CreateSatisBelgesiRequest,
    UpdateSatisBelgesiRequest,
    SatisBelgesiFilterDto,
    SatisBelgesiRedRequest
} from '../models/satis-belgesi.model';

@Injectable({ providedIn: 'root' })
export class SatisBelgesiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly base = `${this.apiBaseUrl}/ui/muhasebe/satis-belgeleri`;

    getById(id: number): Observable<SatisBelgesiDto> {
        return this.http
            .get<ApiResponse<SatisBelgesiDto>>(`${this.base}/${id}`)
            .pipe(map(envelope => this.unwrap(envelope)));
    }

    filter(filter: SatisBelgesiFilterDto): Observable<SatisBelgesiDto[]> {
        return this.http
            .post<ApiResponse<SatisBelgesiDto[]>>(`${this.base}/filter`, filter)
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    create(request: CreateSatisBelgesiRequest): Observable<SatisBelgesiDto> {
        return this.http
            .post<ApiResponse<SatisBelgesiDto>>(this.base, request)
            .pipe(map(envelope => this.unwrap(envelope)));
    }

    update(id: number, request: UpdateSatisBelgesiRequest): Observable<SatisBelgesiDto> {
        return this.http
            .put<ApiResponse<SatisBelgesiDto>>(`${this.base}/${id}`, request)
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

    muhasebeOnayla(id: number): Observable<void> {
        return this.http
            .post<ApiResponse<void>>(`${this.base}/${id}/muhasebe-onayla`, {})
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.'); }));
    }

    reddet(id: number, request: SatisBelgesiRedRequest): Observable<void> {
        return this.http
            .post<ApiResponse<void>>(`${this.base}/${id}/reddet`, request)
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.'); }));
    }

    iptalEt(id: number): Observable<void> {
        return this.http
            .post<ApiResponse<void>>(`${this.base}/${id}/iptal`, {})
            .pipe(map(envelope => { if (!envelope.success) throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.'); }));
    }

    muhasebeFisiOlustur(id: number): Observable<SatisBelgesiDto> {
        return this.http
            .post<ApiResponse<SatisBelgesiDto>>(`${this.base}/${id}/muhasebe-fisi-olustur`, {})
            .pipe(map(envelope => this.unwrap(envelope)));
    }

    private unwrap<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
