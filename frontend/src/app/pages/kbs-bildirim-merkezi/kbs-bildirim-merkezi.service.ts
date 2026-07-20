import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { RezervasyonTesisDto } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { KbsBildirim, KbsGunlukOzet, KbsSayfaliSonuc, KbsTesisAyari } from './kbs-bildirim-merkezi.dto';

@Injectable({ providedIn: 'root' })
export class KbsBildirimMerkeziService {
    private readonly http = inject(HttpClient); private readonly base = getApiBaseUrl();
    tesisler(): Observable<RezervasyonTesisDto[]> { return this.unwrap(this.http.get<ApiResponse<RezervasyonTesisDto[]>>(`${this.base}/ui/rezervasyon/tesisler`), 'Tesisler alinamadi.'); }
    liste(tesisId: number | null, durum: string | null, sayfa: number, sayfaBoyutu: number): Observable<KbsSayfaliSonuc<KbsBildirim>> {
        let params = new HttpParams().set('sayfa', sayfa).set('sayfaBoyutu', sayfaBoyutu); if (tesisId) params = params.set('tesisId', tesisId); if (durum) params = params.set('durum', durum);
        return this.unwrap(this.http.get<ApiResponse<KbsSayfaliSonuc<KbsBildirim>>>(`${this.base}/ui/kbs/bildirimler`, { params }), 'KBS bildirimleri alinamadi.');
    }
    ozet(tesisId: number | null): Observable<KbsGunlukOzet> { let params = new HttpParams(); if (tesisId) params = params.set('tesisId', tesisId); return this.unwrap(this.http.get<ApiResponse<KbsGunlukOzet>>(`${this.base}/ui/kbs/ozet`, { params }), 'KBS ozeti alinamadi.'); }
    ayar(tesisId: number): Observable<KbsTesisAyari | null> { return this.unwrapNullable(this.http.get<ApiResponse<KbsTesisAyari | null>>(`${this.base}/ui/kbs/tesisler/${tesisId}/ayar`)); }
    ayarKaydet(ayar: KbsTesisAyari): Observable<KbsTesisAyari> { return this.unwrap(this.http.put<ApiResponse<KbsTesisAyari>>(`${this.base}/ui/kbs/tesisler/${ayar.tesisId}/ayar`, ayar), 'KBS ayari kaydedilemedi.'); }
    tekrarDene(id: number): Observable<void> { return this.http.post<void>(`${this.base}/ui/kbs/bildirimler/${id}/tekrar-dene`, {}); }
    excel(tesisId: number, tip: 'Giris' | 'Cikis'): Observable<HttpResponse<Blob>> { return this.http.get(`${this.base}/ui/kbs/tesisler/${tesisId}/egm-excel/${tip}`, { observe: 'response', responseType: 'blob' }); }
    yuklemeOnayla(tesisId: number, manifestHash: string): Observable<void> { return this.http.post<void>(`${this.base}/ui/kbs/tesisler/${tesisId}/egm-yukleme-onayi/${manifestHash}`, {}); }
    private unwrap<T>(source: Observable<ApiResponse<T>>, message: string): Observable<T> { return source.pipe(map(x => { if (x.success && x.data != null) return x.data; throw new Error(tryReadApiMessage(x) ?? message); })); }
    private unwrapNullable<T>(source: Observable<ApiResponse<T | null>>): Observable<T | null> { return source.pipe(map(x => x.data ?? null)); }
}
