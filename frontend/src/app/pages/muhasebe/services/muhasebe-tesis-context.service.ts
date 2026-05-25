import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, effect, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';

// ── Model ──

/** Canonical tesis model for the muhasebe context — the single source of truth. */
export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

/** Convenience option shape consumed by p‑select / p‑autocomplete. */
export interface MuhasebeTesisSecenek {
    label: string;
    value: number;
}

// ── Local‑storage helpers ──

const LS_KEY = 'stys_muhasebe_calisma_tesisi';

function readPersistedTesis(): MuhasebeTesisModel | null {
    try {
        const raw = localStorage.getItem(LS_KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw);
        if (parsed && typeof parsed.id === 'number' && typeof parsed.ad === 'string') {
            return parsed as MuhasebeTesisModel;
        }
    } catch {
        // corrupted entry — clear it
        localStorage.removeItem(LS_KEY);
    }
    return null;
}

function persistTesis(tesis: MuhasebeTesisModel | null): void {
    if (tesis) {
        localStorage.setItem(LS_KEY, JSON.stringify({ id: tesis.id, ad: tesis.ad }));
    } else {
        localStorage.removeItem(LS_KEY);
    }
}

// ── Service ──

/**
 * Merkezî "Çalışma Tesisi" kontekst servisi.
 *
 * Tüm muhasebe ekranları bu servis üzerinden seçili tesisi okur.
 * Seçim localStorage'da kalıcıdır — sayfa yenilense bile korunur.
 *
 * Kullanım:
 * 1. ngOnInit'te `initialize()` çağır.
 * 2. `seciliTesis` signal'ini oku.
 * 3. Eğer null ise `MuhasebeTesisSecimDialogComponent` ile seçim yaptır.
 */
@Injectable({ providedIn: 'root' })
export class MuhasebeTesisContextService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    // ── State signals ──

    /** Şu an seçili olan çalışma tesisi (null = henüz seçilmemiş). */
    readonly seciliTesis = signal<MuhasebeTesisModel | null>(readPersistedTesis());

    /** Kullanılabilir tüm tesislerin listesi. */
    readonly tesisler = signal<MuhasebeTesisModel[]>([]);

    /** Tesis listesi yükleniyor mu? */
    readonly tesislerLoading = signal(false);

    /** Tesis listesi yüklenirken oluşan hata mesajı. */
    readonly tesislerError = signal<string | null>(null);

    /** p‑select / p‑autocomplete için uygun seçenek dizisi. */
    readonly tesisSecenekleri: Signal<MuhasebeTesisSecenek[]> = signal([]);

    // ── Internal effect to keep secenekler in sync ──

    private readonly _syncEffect = effect(() => {
        const list = this.tesisler();
        (this.tesisSecenekleri as ReturnType<typeof signal>).set(
            list.map(t => ({ label: t.ad, value: t.id }))
        );
    }, { allowSignalWrites: true });

    // ── Init ──

    /**
     * Tesis listesini backend'den yükler ve localStorage'daki seçimi
     * listeyle eşleştirir (silinmiş bir tesis seçili kalmış olabilir).
     */
    initialize(): Observable<MuhasebeTesisModel[]> {
        this.tesislerLoading.set(true);
        this.tesislerError.set(null);

        return this.http
            .get<ApiResponse<MuhasebeTesisModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`)
            .pipe(
                map(envelope => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Tesis listesi alınamadı.');
                }),
                tap({
                    next: (list) => {
                        this.tesisler.set(list);
                        this.tesislerLoading.set(false);

                        // Mevcut localStorage seçimini doğrula
                        const current = this.seciliTesis();
                        if (current) {
                            const stillExists = list.some(t => t.id === current.id);
                            if (!stillExists) {
                                this.clearTesis();
                            }
                        }
                    },
                    error: (err) => {
                        this.tesislerError.set(err.message ?? 'Tesis listesi yüklenemedi.');
                        this.tesislerLoading.set(false);
                    }
                })
            );
    }

    // ── Actions ──

    /** Çalışma tesisini değiştir ve localStorage'a yaz. */
    selectTesis(tesis: MuhasebeTesisModel): void {
        const allowed = this.tesisler().some(t => t.id === tesis.id);
        if (!allowed) {
            this.clearTesis();
            throw new Error('Seçilen tesis için yetkiniz bulunmamaktadır.');
        }

        this.seciliTesis.set(tesis);
        persistTesis(tesis);
    }

    /** Çalışma tesisi seçimini temizle. */
    clearTesis(): void {
        this.seciliTesis.set(null);
        persistTesis(null);
    }

    /**
     * Seçili tesisi döndürür. Eğer seçili tesis yoksa hata fırlatır —
     * çağıran taraf önce `seciliTesis` sinyalini kontrol etmelidir.
     */
    requireSeciliTesis(): MuhasebeTesisModel {
        const tesis = this.seciliTesis();
        if (!tesis) {
            throw new Error('Çalışma tesisi seçilmemiş. Lütfen önce bir tesis seçiniz.');
        }
        return tesis;
    }

    /**
     * Seçili tesisin ID'sini döndürür. Seçili tesis yoksa hata fırlatır.
     */
    requireSeciliTesisId(): number {
        return this.requireSeciliTesis().id;
    }
}
