import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { getApiBaseUrl } from '../../core/config';
import { ApiResponse, tryReadApiMessage } from '../../core/api/api-response.model';
import {
    KdvUygulamaTipi,
    SatisBelgesiTipi,
    TicariBelgeCariKartLookupDto,
    TicariBelgeDetayDto,
    TicariBelgeDto,
    TicariBelgeFilterDto,
    TicariBelgeGuncelleRequest,
    TicariBelgeIadeAdayiDto,
    TicariBelgeIadeAdayiFilterDto,
    TicariBelgeKaynakSatirDto,
    TicariBelgeKdvIstisnaLookupDto,
    TicariBelgeTesisLookupDto
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

    // ── Operasyonel lookup uç noktaları (bkz. görev A/C) — yalnızca TicariBelgeYonetimi.View
    // yetkisi gerektirir; CariKartYonetimi/MuhasebeKdvIstisnaTanimlariYonetimi/TesisYonetimi
    // servislerine BAĞIMLI DEĞİLDİR. ──

    getTesisLookup(): Observable<TicariBelgeTesisLookupDto[]> {
        return this.http
            .get<ApiResponse<TicariBelgeTesisLookupDto[]>>(`${this.base}/lookups/tesisler`)
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    getCariKartLookup(tesisId: number, belgeTipi: SatisBelgesiTipi): Observable<TicariBelgeCariKartLookupDto[]> {
        const params = { tesisId: String(tesisId), belgeTipi: String(belgeTipi) };
        return this.http
            .get<ApiResponse<TicariBelgeCariKartLookupDto[]>>(`${this.base}/lookups/cari-kartlar`, { params })
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    getKdvIstisnaLookup(
        belgeTipi: SatisBelgesiTipi,
        kdvUygulamaTipi: KdvUygulamaTipi,
        belgeTarihi: string
    ): Observable<TicariBelgeKdvIstisnaLookupDto[]> {
        const params = { belgeTipi: String(belgeTipi), kdvUygulamaTipi: String(kdvUygulamaTipi), belgeTarihi };
        return this.http
            .get<ApiResponse<TicariBelgeKdvIstisnaLookupDto[]>>(`${this.base}/lookups/kdv-istisnalari`, { params })
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    getIadeAdaylari(filter: TicariBelgeIadeAdayiFilterDto): Observable<TicariBelgeIadeAdayiDto[]> {
        return this.http
            .post<ApiResponse<TicariBelgeIadeAdayiDto[]>>(`${this.base}/lookups/iade-adaylari`, filter)
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    getKaynakSatirlar(kaynakBelgeId: number, mevcutBelgeId: number | null): Observable<TicariBelgeKaynakSatirDto[]> {
        const params: Record<string, string> = mevcutBelgeId != null ? { mevcutBelgeId: String(mevcutBelgeId) } : {};
        return this.http
            .get<ApiResponse<TicariBelgeKaynakSatirDto[]>>(`${this.base}/lookups/kaynak-satirlar/${kaynakBelgeId}`, { params })
            .pipe(map(envelope => this.unwrap(envelope) ?? []));
    }

    private unwrap<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
