import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CreateKasaBankaHesapRequest, KASA_BANKA_HESAP_TIPLERI, KasaBankaHesapModel, KasaBankaHesapTipi, MuhasebeTesisModel, UpdateKasaBankaHesapRequest } from './kasa-banka-hesaplari.dto';
import { KasaBankaHesaplariService } from './kasa-banka-hesaplari.service';

@Component({
    selector: 'app-kasa-banka-hesaplari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './kasa-banka-hesaplari.html',
    providers: [MessageService]
})
export class KasaBankaHesaplariPage implements OnInit {
    private readonly service = inject(KasaBankaHesaplariService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    tipDialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    records: KasaBankaHesapModel[] = [];
    filteredRecords: KasaBankaHesapModel[] = [];
    model: KasaBankaHesapModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    readonly hesapTipleri = KASA_BANKA_HESAP_TIPLERI;
    tesisler: MuhasebeTesisModel[] = [];
    tesisSecenekleri: Array<{ label: string; value: number | null }> = [];
    selectedTesisId: number | null = null;
    tipFilter: KasaBankaHesapTipi | null = null;
    secilenYeniTip: KasaBankaHesapTipi = 'NakitKasa';
    bagliBankaSecenekleri: Array<{ label: string; value: number }> = [];

    ngOnInit(): void {
        this.loadTesisler();
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.selectedTesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.applyClientFilter();
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    openCreate(): void {
        this.secilenYeniTip = 'NakitKasa';
        this.tipDialogVisible = true;
    }

    continueCreate(): void {
        this.tipDialogVisible = false;
        this.dialogMode = 'create';
        this.model = this.createEmpty(this.secilenYeniTip);
        this.model.tesisId = this.selectedTesisId;
        this.dialogVisible = true;
        this.refreshBagliBankaSecenekleri();
    }

    openEdit(item: KasaBankaHesapModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
        this.refreshBagliBankaSecenekleri();
    }

    onTipChange(): void {
        const next = this.model.tip;
        const defaults = this.createEmpty(next);
        this.model = {
            ...this.model,
            paraBirimi: defaults.paraBirimi,
            valorGunSayisi: defaults.valorGunSayisi,
            bankaAdi: next === 'NakitKasa' ? null : this.model.bankaAdi,
            subeAdi: next === 'NakitKasa' ? null : this.model.subeAdi,
            hesapNo: next === 'NakitKasa' ? null : this.model.hesapNo,
            iban: next === 'NakitKasa' ? null : this.model.iban,
            musteriNo: next === 'NakitKasa' ? null : this.model.musteriNo,
            hesapTuru: next === 'NakitKasa' ? null : this.model.hesapTuru,
            kartAdi: next === 'KrediKarti' ? this.model.kartAdi : null,
            kartNoMaskeli: next === 'KrediKarti' ? this.model.kartNoMaskeli : null,
            kartLimiti: next === 'KrediKarti' ? this.model.kartLimiti : null,
            hesapKesimGunu: next === 'KrediKarti' ? this.model.hesapKesimGunu : null,
            sonOdemeGunu: next === 'KrediKarti' ? this.model.sonOdemeGunu : null,
            bagliBankaHesapId: next === 'KrediKarti' ? this.model.bagliBankaHesapId : null,
            sorumluKisi: next === 'NakitKasa' ? this.model.sorumluKisi : null,
            lokasyon: next === 'NakitKasa' ? this.model.lokasyon : null
        };
        this.refreshBagliBankaSecenekleri();
    }

    save(): void {
        if (!this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Ad zorunludur.' });
            return;
        }

        if (!this.model.tesisId || this.model.tesisId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tesis secimi zorunludur.' });
            return;
        }

        if (this.model.valorGunSayisi < 0 || this.model.valorGunSayisi > 365) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Deger', detail: 'Valör süresi 0-365 araliginda olmalidir.' });
            return;
        }

        if (this.model.tip === 'DovizHesabi' && !this.model.paraBirimi?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Doviz hesabi icin para birimi zorunludur.' });
            return;
        }

        const payload: CreateKasaBankaHesapRequest | UpdateKasaBankaHesapRequest = {
            tesisId: this.model.tesisId ?? null,
            tip: this.model.tip,
            kod: null,
            ad: this.model.ad.trim(),
            muhasebeHesapPlaniId: null,
            paraBirimi: this.model.paraBirimi?.trim() || null,
            valorGunSayisi: this.model.valorGunSayisi,
            kartAdi: this.model.kartAdi?.trim() || null,
            kartNoMaskeli: this.model.kartNoMaskeli?.trim() || null,
            kartLimiti: this.model.kartLimiti ?? null,
            hesapKesimGunu: this.model.hesapKesimGunu ?? null,
            sonOdemeGunu: this.model.sonOdemeGunu ?? null,
            bagliBankaHesapId: this.model.bagliBankaHesapId ?? null,
            bankaAdi: this.model.bankaAdi?.trim() || null,
            subeAdi: this.model.subeAdi?.trim() || null,
            hesapNo: this.model.hesapNo?.trim() || null,
            iban: this.model.iban?.trim() || null,
            musteriNo: this.model.musteriNo?.trim() || null,
            hesapTuru: this.model.hesapTuru?.trim() || null,
            sorumluKisi: this.model.sorumluKisi?.trim() || null,
            lokasyon: this.model.lokasyon?.trim() || null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateKasaBankaHesapRequest)
            : this.service.create(payload as CreateKasaBankaHesapRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: KasaBankaHesapModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => {
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getTipLabel(tip: string): string {
        return this.hesapTipleri.find((x) => x.value === tip)?.label ?? tip;
    }

    onTesisFilterChange(): void {
        this.pageNumber = 1;
        this.load(1, this.pageSize);
    }

    getTesisAdi(tesisId?: number | null): string {
        if (!tesisId) {
            return '-';
        }
        return this.tesisler.find((x) => x.id === tesisId)?.ad ?? `#${tesisId}`;
    }

    isTipOrTesisLocked(): boolean {
        return this.dialogMode === 'edit' && !!this.model.muhasebeHesapPlaniId;
    }

    private refreshBagliBankaSecenekleri(): void {
        const candidates = this.records
            .filter((x) => x.id !== this.model.id && (x.tip === 'Banka' || x.tip === 'DovizHesabi') && x.aktifMi)
            .filter((x) => !this.model.tesisId || x.tesisId === this.model.tesisId)
            .map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
        this.bagliBankaSecenekleri = candidates;
    }

    private createEmpty(tip: KasaBankaHesapTipi = 'NakitKasa'): KasaBankaHesapModel {
        const isKredi = tip === 'KrediKarti';
        return {
            tesisId: null,
            tip,
            kod: '',
            ad: '',
            muhasebeHesapPlaniId: null,
            anaMuhasebeHesapKodu: null,
            muhasebeHesapSiraNo: null,
            paraBirimi: tip === 'DovizHesabi' ? '' : 'TRY',
            valorGunSayisi: isKredi ? 1 : 0,
            kartAdi: null,
            kartNoMaskeli: null,
            kartLimiti: null,
            hesapKesimGunu: null,
            sonOdemeGunu: null,
            bagliBankaHesapId: null,
            muhasebeTamKod: null,
            muhasebeHesapAdi: null,
            bankaAdi: null,
            subeAdi: null,
            hesapNo: null,
            iban: null,
            musteriNo: null,
            hesapTuru: null,
            sorumluKisi: null,
            lokasyon: null,
            aktifMi: true,
            aciklama: null
        };
    }

    applyClientFilter(): void {
        let list = [...this.records];
        if (this.selectedTesisId) {
            list = list.filter((x) => x.tesisId === this.selectedTesisId);
        }
        if (this.tipFilter) {
            list = list.filter((x) => x.tip === this.tipFilter);
        }
        this.filteredRecords = list;
    }

    private loadTesisler(): void {
        this.service.getTesisler().subscribe({
            next: (items) => {
                this.tesisler = [...items].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? ''));
                this.tesisSecenekleri = [{ label: 'Tum Tesisler', value: null }, ...this.tesisler.map((x) => ({ label: x.ad, value: x.id }))];
                if (!this.selectedTesisId && this.tesisler.length > 0) {
                    this.selectedTesisId = this.tesisler[0].id;
                }
                this.load(1, this.pageSize);
            },
            error: (error: unknown) => {
                this.showError(error);
                this.load(1, this.pageSize);
            }
        });
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
