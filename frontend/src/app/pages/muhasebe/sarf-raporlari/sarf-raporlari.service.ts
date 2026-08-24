import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { SarfDetayPagedModel, SarfRaporFilterModel, SarfTuketimDetayRaporSatirModel, SarfTuketimKullanimYeriOzetModel, SarfTuketimMalzemeOzetModel } from './sarf-raporlari.dto';

@Injectable({ providedIn: 'root' })
export class SarfRaporlariService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getDetay(filter: SarfRaporFilterModel, pageNumber: number, pageSize: number): Observable<SarfDetayPagedModel> {
        const params = this.buildParams(filter)
            .set('pageNumber', pageNumber)
            .set('pageSize', pageSize);

        return this.http
            .get<ApiResponse<PagedResponseDto<SarfTuketimDetayRaporSatirModel>>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/detay`, { params })
            .pipe(map(this.unwrap<SarfDetayPagedModel>('Sarf detay raporu alınamadı.')));
    }

    getMalzemeOzet(filter: SarfRaporFilterModel): Observable<SarfTuketimMalzemeOzetModel[]> {
        return this.http
            .get<ApiResponse<SarfTuketimMalzemeOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/malzeme-ozet`, { params: this.buildParams(filter) })
            .pipe(map(this.unwrap<SarfTuketimMalzemeOzetModel[]>('Malzeme bazlı sarf özeti alınamadı.')));
    }

    getKullanimYeriOzet(filter: SarfRaporFilterModel): Observable<SarfTuketimKullanimYeriOzetModel[]> {
        return this.http
            .get<ApiResponse<SarfTuketimKullanimYeriOzetModel[]>>(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/kullanim-yeri-ozet`, { params: this.buildParams(filter) })
            .pipe(map(this.unwrap<SarfTuketimKullanimYeriOzetModel[]>('Kullanım yeri bazlı sarf özeti alınamadı.')));
    }

    exportDetayExcel(filter: SarfRaporFilterModel): Observable<Blob> {
        return this.http.get(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/detay/excel`, {
            params: this.buildParams(filter),
            responseType: 'blob'
        });
    }

    exportMalzemeOzetExcel(filter: SarfRaporFilterModel): Observable<Blob> {
        return this.http.get(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/malzeme-ozet/excel`, {
            params: this.buildParams(filter),
            responseType: 'blob'
        });
    }

    exportKullanimYeriOzetExcel(filter: SarfRaporFilterModel): Observable<Blob> {
        return this.http.get(`${this.apiBaseUrl}/ui/muhasebe/sarf-raporlari/kullanim-yeri-ozet/excel`, {
            params: this.buildParams(filter),
            responseType: 'blob'
        });
    }

    private buildParams(filter: SarfRaporFilterModel): HttpParams {
        let params = new HttpParams().set('tesisId', filter.tesisId);

        if (filter.baslangicTarihi) params = params.set('baslangicTarihi', filter.baslangicTarihi);
        if (filter.bitisTarihi) params = params.set('bitisTarihi', filter.bitisTarihi);
        if (filter.depoId) params = params.set('depoId', filter.depoId);
        if (filter.tasinirKartId) params = params.set('tasinirKartId', filter.tasinirKartId);
        if (filter.isletmeAlaniId) params = params.set('isletmeAlaniId', filter.isletmeAlaniId);
        if (filter.odaId) params = params.set('odaId', filter.odaId);
        if (filter.sarfNedeni && filter.sarfNedeni.trim().length > 0) params = params.set('sarfNedeni', filter.sarfNedeni.trim());
        if (filter.durum && filter.durum.trim().length > 0) params = params.set('durum', filter.durum.trim());

        return params;
    }

    private unwrap<T>(fallback: string) {
        return (envelope: ApiResponse<T>): T => {
            if (envelope.success && envelope.data) {
                return envelope.data;
            }

            throw new Error(tryReadApiMessage(envelope) ?? fallback);
        };
    }
}
