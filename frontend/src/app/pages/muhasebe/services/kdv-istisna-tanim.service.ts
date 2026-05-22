import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateKdvIstisnaTanimRequest,
    KdvIstisnaTanimDto,
    KdvIstisnaTanimFilterDto,
    UpdateKdvIstisnaTanimRequest
} from '../models/kdv-istisna-tanim.model';

@Injectable({ providedIn: 'root' })
export class KdvIstisnaTanimService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    filter(filter: KdvIstisnaTanimFilterDto): Observable<KdvIstisnaTanimDto[]> {
        return this.http
            .post<ApiResponse<KdvIstisnaTanimDto[]>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/filter`,
                filter
            )
            .pipe(
                map(envelope => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(
                        tryReadApiMessage(envelope) ?? 'KDV istisna tanımları alınamadı.'
                    );
                })
            );
    }

    getById(id: number): Observable<KdvIstisnaTanimDto> {
        return this.http
            .get<ApiResponse<KdvIstisnaTanimDto>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`
            )
            .pipe(
                map(envelope => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(
                        tryReadApiMessage(envelope) ?? 'KDV istisna tanımı alınamadı.'
                    );
                })
            );
    }

    create(request: CreateKdvIstisnaTanimRequest): Observable<KdvIstisnaTanimDto> {
        return this.http
            .post<ApiResponse<KdvIstisnaTanimDto>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari`,
                request
            )
            .pipe(
                map(envelope => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(
                        tryReadApiMessage(envelope) ?? 'KDV istisna tanımı oluşturulamadı.'
                    );
                })
            );
    }

    update(id: number, request: UpdateKdvIstisnaTanimRequest): Observable<KdvIstisnaTanimDto> {
        return this.http
            .put<ApiResponse<KdvIstisnaTanimDto>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`,
                request
            )
            .pipe(
                map(envelope => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(
                        tryReadApiMessage(envelope) ?? 'KDV istisna tanımı güncellenemedi.'
                    );
                })
            );
    }

    delete(id: number): Observable<void> {
        return this.http
            .delete<ApiResponse<void>>(
                `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`
            )
            .pipe(
                map(envelope => {
                    if (envelope.success) {
                        return;
                    }
                    throw new Error(
                        tryReadApiMessage(envelope) ?? 'KDV istisna tanımı silinemedi.'
                    );
                })
            );
    }
}
