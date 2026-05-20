import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { CreateMuhasebeDonemRequest, MuhasebeDonemDto, UpdateMuhasebeDonemRequest } from '../models/muhasebe-donem.model';

@Injectable({ providedIn: 'root' })
export class MuhasebeDonemService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(): Observable<MuhasebeDonemDto[]> {
        return this.http.get<MuhasebeDonemDto[]>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler`
        );
    }

    getById(id: number): Observable<MuhasebeDonemDto> {
        return this.http.get<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/${id}`
        );
    }

    getAktif(tesisId: number, tarih?: string): Observable<MuhasebeDonemDto> {
        let params: Record<string, string> = { tesisId: String(tesisId) };
        if (tarih) {
            params['tarih'] = tarih;
        }
        return this.http.get<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/aktif`,
            { params }
        );
    }

    create(request: CreateMuhasebeDonemRequest): Observable<MuhasebeDonemDto> {
        return this.http.post<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler`,
            request
        );
    }

    update(id: number, request: UpdateMuhasebeDonemRequest): Observable<MuhasebeDonemDto> {
        return this.http.put<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/${id}`,
            request
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/${id}`
        );
    }

    kapat(id: number): Observable<MuhasebeDonemDto> {
        return this.http.post<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/${id}/kapat`,
            {}
        );
    }

    ac(id: number): Observable<MuhasebeDonemDto> {
        return this.http.post<MuhasebeDonemDto>(
            `${this.apiBaseUrl}/ui/muhasebe/donemler/${id}/ac`,
            {}
        );
    }
}
