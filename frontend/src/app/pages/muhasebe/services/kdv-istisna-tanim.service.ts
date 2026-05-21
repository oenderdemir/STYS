import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import {
    CreateKdvIstisnaTanimRequest,
    KdvIstisnaTanimDto,
    UpdateKdvIstisnaTanimRequest
} from '../models/kdv-istisna-tanim.model';

@Injectable({ providedIn: 'root' })
export class KdvIstisnaTanimService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<KdvIstisnaTanimDto[]> {
        return this.http.get<KdvIstisnaTanimDto[]>(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari`
        );
    }

    getById(id: number): Observable<KdvIstisnaTanimDto> {
        return this.http.get<KdvIstisnaTanimDto>(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`
        );
    }

    create(request: CreateKdvIstisnaTanimRequest): Observable<KdvIstisnaTanimDto> {
        return this.http.post<KdvIstisnaTanimDto>(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari`,
            request
        );
    }

    update(id: number, request: UpdateKdvIstisnaTanimRequest): Observable<KdvIstisnaTanimDto> {
        return this.http.put<KdvIstisnaTanimDto>(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`,
            request
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(
            `${this.apiBaseUrl}/ui/muhasebe/kdv-istisna-tanimlari/${id}`
        );
    }
}
