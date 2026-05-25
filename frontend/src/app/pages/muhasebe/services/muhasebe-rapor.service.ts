import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { MizanFilterModel, MizanKarsilastirmaModel, MizanModel } from '../models/mizan.model';
import { MuavinDefterFilterModel, MuavinDefterModel } from '../models/muavin-defter.model';
import { MuhasebeFisFilterModel } from '../models/muhasebe-fis.model';
import { YevmiyeDefteriModel } from '../models/yevmiye-defteri.model';

@Injectable({ providedIn: 'root' })
export class MuhasebeRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getHizliMizan(filter: MizanFilterModel): Observable<MizanModel> {
        return this.http
            .post<ApiResponse<MizanModel>>(`${this.apiBaseUrl}/ui/muhasebe/fisler/mizan-bakiye`, filter)
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Hizli mizan alinamadi.');
                })
            );
    }

    karsilastirMizan(filter: MizanFilterModel): Observable<MizanKarsilastirmaModel> {
        return this.http
            .post<ApiResponse<MizanKarsilastirmaModel>>(`${this.apiBaseUrl}/ui/muhasebe/fisler/mizan-karsilastir`, filter)
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Mizan karsilastirma alinamadi.');
                })
            );
    }

    exportMizanBakiyeExcel(filter: MizanFilterModel): Observable<Blob> {
        return this.http.post(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/mizan-bakiye/export-excel`,
            filter,
            { responseType: 'blob' }
        );
    }

    getYevmiyeDefteri(filter: MuhasebeFisFilterModel): Observable<YevmiyeDefteriModel> {
        return this.http
            .post<ApiResponse<YevmiyeDefteriModel>>(`${this.apiBaseUrl}/ui/muhasebe/fisler/yevmiye-defteri`, filter)
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Yevmiye defteri alinamadi.');
                })
            );
    }

    exportYevmiyeDefteriExcel(filter: MuhasebeFisFilterModel): Observable<Blob> {
        return this.http.post(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/yevmiye-defteri/export-excel`,
            filter,
            { responseType: 'blob' }
        );
    }

    getMuavinDefter(filter: MuavinDefterFilterModel): Observable<MuavinDefterModel> {
        return this.http
            .post<ApiResponse<MuavinDefterModel>>(`${this.apiBaseUrl}/ui/muhasebe/fisler/muavin-defter`, filter)
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Muavin defter alinamadi.');
                })
            );
    }

    exportMuavinDefterExcel(filter: MuavinDefterFilterModel): Observable<Blob> {
        return this.http.post(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/muavin-defter/export-excel`,
            filter,
            { responseType: 'blob' }
        );
    }
}
