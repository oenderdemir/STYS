import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    BeyanEdilenOdemeEslesmeModel,
    BeyanEdilenOdemeKarsilastirmaFilterModel,
    CariHareketDokumFilterModel,
    CariHareketDokumModel,
    OdemeAdayiModel,
    OdemeAramaFilterModel,
    OdemeAramaSatiriModel,
    OdemeCaprazAramaFilterModel,
    OdemeDetayModel
} from './odeme-izleme.dto';

@Injectable({ providedIn: 'root' })
export class OdemeIzlemeService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = `${getApiBaseUrl()}/ui/muhasebe/odeme-izleme`;

    ara(pageNumber: number, pageSize: number, filter: OdemeAramaFilterModel): Observable<PagedResponseDto<OdemeAramaSatiriModel>> {
        let params = this.buildFilterParams(filter).set('pageNumber', pageNumber).set('pageSize', pageSize);
        return this.http.get<ApiResponse<PagedResponseDto<OdemeAramaSatiriModel>>>(this.baseUrl, { params }).pipe(map(this.unwrapOne));
    }

    /** Capraz-kaynak arastirma - odeme belgesi OLMAYAN kayitlari da bulur. */
    caprazAra(pageNumber: number, pageSize: number, filter: OdemeCaprazAramaFilterModel): Observable<PagedResponseDto<OdemeAdayiModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (filter.tesisId && filter.tesisId > 0) params = params.set('tesisId', filter.tesisId);
        if (filter.tarihBaslangic) params = params.set('tarihBaslangic', filter.tarihBaslangic);
        if (filter.tarihBitis) params = params.set('tarihBitis', filter.tarihBitis);
        if (filter.tutarMin != null) params = params.set('tutarMin', filter.tutarMin);
        if (filter.tutarMax != null) params = params.set('tutarMax', filter.tutarMax);
        if (filter.cariKartId) params = params.set('cariKartId', filter.cariKartId);
        if (filter.kopuklukTipi) params = params.set('kopuklukTipi', filter.kopuklukTipi);
        if (filter.sadeceKopukOlanlar != null) params = params.set('sadeceKopukOlanlar', filter.sadeceKopukOlanlar);
        return this.http.get<ApiResponse<PagedResponseDto<OdemeAdayiModel>>>(`${this.baseUrl}/capraz-arama`, { params }).pipe(map(this.unwrapOne));
    }

    getDetay(id: number): Observable<OdemeDetayModel> {
        return this.http.get<ApiResponse<OdemeDetayModel>>(`${this.baseUrl}/${id}`).pipe(map(this.unwrapOne));
    }

    getCariHareketDokumu(filter: CariHareketDokumFilterModel): Observable<CariHareketDokumModel> {
        let params = new HttpParams().set('cariKartId', filter.cariKartId);
        if (filter.tarihBaslangic) params = params.set('tarihBaslangic', filter.tarihBaslangic);
        if (filter.tarihBitis) params = params.set('tarihBitis', filter.tarihBitis);
        return this.http.get<ApiResponse<CariHareketDokumModel>>(`${this.baseUrl}/cari-hareket-dokumu`, { params }).pipe(map(this.unwrapOne));
    }

    karsilastir(filter: BeyanEdilenOdemeKarsilastirmaFilterModel): Observable<BeyanEdilenOdemeEslesmeModel[]> {
        let params = new HttpParams()
            .set('tarih', filter.tarih)
            .set('tutar', filter.tutar)
            .set('paraBirimi', filter.paraBirimi)
            .set('tarihToleransGun', filter.tarihToleransGun ?? 1);
        if (filter.tesisId) params = params.set('tesisId', filter.tesisId);
        if (filter.odemeYontemi) params = params.set('odemeYontemi', filter.odemeYontemi);
        if (filter.kasaBankaHesapId) params = params.set('kasaBankaHesapId', filter.kasaBankaHesapId);
        if (filter.belgeNoTahmini) params = params.set('belgeNoTahmini', filter.belgeNoTahmini);
        if (filter.cariKartId) params = params.set('cariKartId', filter.cariKartId);
        return this.http.get<ApiResponse<BeyanEdilenOdemeEslesmeModel[]>>(`${this.baseUrl}/karsilastir`, { params }).pipe(map(this.unwrapOne));
    }

    private buildFilterParams(filter: OdemeAramaFilterModel): HttpParams {
        let params = new HttpParams();
        if (filter.tesisId && filter.tesisId > 0) params = params.set('tesisId', filter.tesisId);
        if (filter.belgeNo) params = params.set('belgeNo', filter.belgeNo);
        if (filter.cariKartId) params = params.set('cariKartId', filter.cariKartId);
        if (filter.cariAramaMetni) params = params.set('cariAramaMetni', filter.cariAramaMetni);
        if (filter.tarihBaslangic) params = params.set('tarihBaslangic', filter.tarihBaslangic);
        if (filter.tarihBitis) params = params.set('tarihBitis', filter.tarihBitis);
        if (filter.tutarMin != null) params = params.set('tutarMin', filter.tutarMin);
        if (filter.tutarMax != null) params = params.set('tutarMax', filter.tutarMax);
        if (filter.paraBirimi) params = params.set('paraBirimi', filter.paraBirimi);
        if (filter.odemeYontemi) params = params.set('odemeYontemi', filter.odemeYontemi);
        if (filter.belgeTipi) params = params.set('belgeTipi', filter.belgeTipi);
        if (filter.kasaBankaHesapId) params = params.set('kasaBankaHesapId', filter.kasaBankaHesapId);
        if (filter.durum) params = params.set('durum', filter.durum);
        if (filter.valorDurumu) params = params.set('valorDurumu', filter.valorDurumu);
        if (filter.sadeceFissizOlanlar) params = params.set('sadeceFissizOlanlar', filter.sadeceFissizOlanlar);
        if (filter.muhasebeFisNo) params = params.set('muhasebeFisNo', filter.muhasebeFisNo);
        if (filter.rezervasyonReferansNo) params = params.set('rezervasyonReferansNo', filter.rezervasyonReferansNo);
        if (filter.olusturulmaBaslangic) params = params.set('olusturulmaBaslangic', filter.olusturulmaBaslangic);
        if (filter.olusturulmaBitis) params = params.set('olusturulmaBitis', filter.olusturulmaBitis);
        if (filter.olusturanKullanici) params = params.set('olusturanKullanici', filter.olusturanKullanici);
        if (filter.maliYil) params = params.set('maliYil', filter.maliYil);
        if (filter.donem) params = params.set('donem', filter.donem);
        if (filter.iban) params = params.set('iban', filter.iban);
        if (filter.sadeceIptalEdilmisOlanlar != null) params = params.set('sadeceIptalEdilmisOlanlar', filter.sadeceIptalEdilmisOlanlar);
        return params;
    }

    private unwrapOne<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data !== undefined && envelope.data !== null) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
