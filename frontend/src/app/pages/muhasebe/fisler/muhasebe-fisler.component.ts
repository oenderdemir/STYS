import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, take } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import {
    MuhasebeFisFilterModel,
    MuhasebeFisModel,
    MuhasebeFisSatirModel,
    UpdateMuhasebeFisSatirRequestModel,
    createDefaultFisFilter,
    normalizeFisFilter
} from '../models/muhasebe-fis.model';
import { MuhasebeFisService } from '../services/muhasebe-fis.service';
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';

interface TesisSecenek {
    label: string;
    value: number;
}

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const currentYear = new Date().getFullYear();
    const options: Array<{ label: string; value: number | null }> = [
        { label: 'Tümü', value: null }
    ];
    for (let y = currentYear - 3; y <= currentYear + 1; y++) {
        options.push({ label: String(y), value: y });
    }
    return options;
})();

const DONEM_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Tüm Dönemler', value: null },
    { label: '1. Dönem', value: 1 },
    { label: '2. Dönem', value: 2 },
    { label: '3. Dönem', value: 3 },
    { label: '4. Dönem', value: 4 },
    { label: '5. Dönem', value: 5 },
    { label: '6. Dönem', value: 6 },
    { label: '7. Dönem', value: 7 },
    { label: '8. Dönem', value: 8 },
    { label: '9. Dönem', value: 9 },
    { label: '10. Dönem', value: 10 },
    { label: '11. Dönem', value: 11 },
    { label: '12. Dönem', value: 12 }
];

const FIS_TIPI_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Mahsup', value: 'Mahsup' },
    { label: 'Tahsil', value: 'Tahsil' },
    { label: 'Tediye', value: 'Tediye' },
    { label: 'Açılış', value: 'Acilis' },
    { label: 'Kapanış', value: 'Kapanis' }
];

const DURUM_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Taslak', value: 'Taslak' },
    { label: 'Onaylı', value: 'Onayli' },
    { label: 'İptal', value: 'Iptal' },
    { label: 'Ters Kayıt', value: 'TersKayit' }
];

const PAGE_SIZE_SECENEKLERI: Array<{ label: string; value: number }> = [
    { label: '50', value: 50 },
    { label: '100', value: 100 },
    { label: '200', value: 200 },
    { label: '500', value: 500 }
];

@Component({
    selector: 'app-muhasebe-fisler',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DatePipe,
        DecimalPipe,
        ButtonModule,
        ConfirmDialogModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        TooltipModule
    ],
    templateUrl: './muhasebe-fisler.component.html',
    styleUrls: ['./muhasebe-fisler.component.scss'],
    providers: [MessageService, ConfirmationService]
})
export class MuhasebeFislerComponent implements OnInit {
    private readonly service = inject(MuhasebeFisService);
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    filter: MuhasebeFisFilterModel = createDefaultFisFilter();
    result: MuhasebeFisModel[] = [];
    totalCount = 0;

    // Detay dialog state
    detayDialogVisible = false;
    detayFis: MuhasebeFisModel | null = null;
    detayLoading = false;

    // Aksiyon loading states (per-row tracking via row id)
    onaylananFisId: number | null = null;
    iptalEdilenFisId: number | null = null;

    // Düzenle dialog state
    duzenleDialogVisible = false;
    duzenleFis: MuhasebeFisModel | null = null;
    duzenleLoading = false;
    duzenleSaving = false;
    // Editable satır kopyaları (MuhasebeFisSatirModel'in alt kümesi)
    duzenleSatirlar: UpdateMuhasebeFisSatirRequestModel[] = [];

    tesisSecenekleri: TesisSecenek[] = [];
    readonly maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    readonly donemSecenekleri = DONEM_SECENEKLERI;
    readonly fisTipiSecenekleri = FIS_TIPI_SECENEKLERI;
    readonly fisTipiDuzenleSecenekleri: Array<{ label: string; value: string }> = FIS_TIPI_SECENEKLERI
        .filter((f): f is { label: string; value: string } => f.value !== null);
    readonly durumSecenekleri = DURUM_SECENEKLERI;
    readonly pageSizeSecenekleri = PAGE_SIZE_SECENEKLERI;

    // Highlight state from query params
    highlightedFisId: number | null = null;
    highlightedFisNo: string | null = null;

    ngOnInit(): void {
        this.loadTesisler();
        this.readQueryParams();
    }

    private loadTesisler(): void {
        this.loading = true;
        this.raporService.getTesisler().pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler.map(t => ({
                    label: t.ad,
                    value: t.id
                }));
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private readQueryParams(): void {
        this.route.queryParamMap
            .pipe(take(1))
            .subscribe(params => {
                const fisNoParam = params.get('fisNo');
                const idParam = params.get('id');

                if (fisNoParam) {
                    this.filter.fisNo = fisNoParam;
                    this.highlightedFisNo = fisNoParam;
                    this.ara();
                    this.messageService.add({
                        severity: UiSeverity.Info,
                        summary: 'Bilgi',
                        detail: `Fiş numarasına göre filtre uygulandı: ${fisNoParam}`,
                        life: 4000
                    });
                } else if (idParam) {
                    const id = Number(idParam);
                    if (!isNaN(id) && id > 0) {
                        this.highlightedFisId = id;
                        this.messageService.add({
                            severity: UiSeverity.Info,
                            summary: 'Bilgi',
                            detail: `Fiş listesi açıldı. Fiş Id: ${id}`,
                            life: 4000
                        });
                        this.ara();
                    }
                }
            });
    }

    ara(): void {
        const normalized = normalizeFisFilter(this.filter);
        this.loading = true;

        this.service.getFiltered(normalized).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (data) => {
                this.result = data;
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });

        this.service.countFiltered(normalized).subscribe({
            next: (count) => {
                this.totalCount = count;
            }
        });
    }

    temizle(): void {
        this.filter = createDefaultFisFilter();
        this.result = [];
        this.totalCount = 0;
        this.highlightedFisId = null;
        this.highlightedFisNo = null;
    }

    oncekiSayfa(): void {
        if (this.filter.page > 1) {
            this.filter.page--;
            this.ara();
        }
    }

    sonrakiSayfa(): void {
        const totalPages = Math.ceil(this.totalCount / this.filter.pageSize);
        if (this.filter.page < totalPages) {
            this.filter.page++;
            this.ara();
        }
    }

    onPageSizeChange(): void {
        this.filter.page = 1;
        this.ara();
    }

    goToFis(row: MuhasebeFisModel): void {
        // Directly apply highlight and re-filter when already on the same component.
        // This avoids relying on ngOnInit which doesn't re-fire on same-route navigation
        // because Angular reuses the component instance.
        this.filter.fisNo = row.fisNo;
        this.highlightedFisId = row.id;
        this.highlightedFisNo = row.fisNo;
        this.ara();
        this.router.navigate(['/muhasebe/fisler'], {
            queryParams: { id: row.id, fisNo: row.fisNo },
            replaceUrl: true
        });
    }

    openDetay(row: MuhasebeFisModel): void {
        this.detayFis = null;
        this.detayDialogVisible = true;
        this.detayLoading = true;
        this.cdr.detectChanges();

        this.service.getById(row.id).pipe(finalize(() => {
            this.detayLoading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (fis) => {
                this.detayFis = fis;
            },
            error: (error: unknown) => {
                this.showError(error);
                this.detayDialogVisible = false;
            }
        });
    }

    onayla(row: MuhasebeFisModel): void {
        this.confirmationService.confirm({
            message: 'Bu fişi onaylamak istiyor musunuz? Onaylanan fiş muhasebe bakiyelerine işlenecektir.',
            header: 'Onaylama Onayı',
            icon: 'pi pi-check-circle',
            acceptLabel: 'Evet, Onayla',
            rejectLabel: 'Vazgeç',
            accept: () => {
                this.onaylananFisId = row.id;
                this.cdr.detectChanges();

                this.service.onayla(row.id).pipe(finalize(() => {
                    this.onaylananFisId = null;
                    this.cdr.detectChanges();
                })).subscribe({
                    next: (onaylanan) => {
                        this.messageService.add({
                            severity: UiSeverity.Success,
                            summary: 'Başarılı',
                            detail: `Fiş onaylandı: ${onaylanan.fisNo}`,
                            life: 4000
                        });
                        this.ara();
                    },
                    error: (error: unknown) => {
                        this.showError(error);
                    }
                });
            }
        });
    }

    iptal(row: MuhasebeFisModel): void {
        this.confirmationService.confirm({
            message: 'Bu fişi iptal etmek istiyor musunuz? Sistem ters kayıt oluşturabilir.',
            header: 'İptal Onayı',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet, İptal Et',
            rejectLabel: 'Vazgeç',
            accept: () => {
                this.iptalEdilenFisId = row.id;
                this.cdr.detectChanges();

                this.service.iptal(row.id).pipe(finalize(() => {
                    this.iptalEdilenFisId = null;
                    this.cdr.detectChanges();
                })).subscribe({
                    next: () => {
                        this.messageService.add({
                            severity: UiSeverity.Success,
                            summary: 'Başarılı',
                            detail: 'Fiş iptal edildi.',
                            life: 4000
                        });
                        this.ara();
                    },
                    error: (error: unknown) => {
                        this.showError(error);
                    }
                });
            }
        });
    }

    // ─── Düzenle dialog ───────────────────────────────────────────

    openDuzenle(row: MuhasebeFisModel): void {
        this.duzenleFis = null;
        this.duzenleSatirlar = [];
        this.duzenleDialogVisible = true;
        this.duzenleLoading = true;
        this.cdr.detectChanges();

        this.service.getById(row.id).pipe(finalize(() => {
            this.duzenleLoading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (fis) => {
                this.duzenleFis = fis;
                // Satırları editable modele kopyala
                this.duzenleSatirlar = (fis.satirlar ?? []).map(s => ({
                    muhasebeHesapPlaniId: s.muhasebeHesapPlaniId,
                    siraNo: s.siraNo,
                    borc: s.borc ?? 0,
                    alacak: s.alacak ?? 0,
                    paraBirimi: s.paraBirimi ?? 'TRY',
                    kur: s.kur ?? 1,
                    cariKartId: s.cariKartId ?? null,
                    tasinirKartId: s.tasinirKartId ?? null,
                    depoId: s.depoId ?? null,
                    kasaBankaHesapId: s.kasaBankaHesapId ?? null,
                    aciklama: s.aciklama ?? null
                }));
            },
            error: (error: unknown) => {
                this.showError(error);
                this.duzenleDialogVisible = false;
            }
        });
    }

    harici(): void {
        this.duzenleDialogVisible = false;
        this.duzenleFis = null;
        this.duzenleSatirlar = [];
    }

    kaydet(): void {
        if (!this.duzenleFis) return;

        // Validasyon
        const hata = this.validateDuzenle();
        if (hata) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Doğrulama Hatası',
                detail: hata,
                life: 5000
            });
            return;
        }

        this.duzenleSaving = true;
        this.cdr.detectChanges();

        const request = {
            tesisId: this.duzenleFis.tesisId,
            fisTarihi: this.duzenleFis.fisTarihi,
            maliYil: this.duzenleFis.maliYil,
            donem: this.duzenleFis.donem,
            fisTipi: this.duzenleFis.fisTipi,
            aciklama: this.duzenleFis.aciklama?.trim() || null,
            satirlar: this.duzenleSatirlar.map((s, i) => ({
                ...s,
                siraNo: i + 1,
                aciklama: s.aciklama?.trim() || null
            }))
        };

        this.service.update(this.duzenleFis.id, request).pipe(finalize(() => {
            this.duzenleSaving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.messageService.add({
                    severity: UiSeverity.Success,
                    summary: 'Başarılı',
                    detail: 'Fiş güncellendi.',
                    life: 4000
                });
                this.duzenleDialogVisible = false;
                this.duzenleFis = null;
                this.duzenleSatirlar = [];
                // Detay dialog açıksa kapat
                this.detayDialogVisible = false;
                this.detayFis = null;
                this.ara();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private validateDuzenle(): string | null {
        if (!this.duzenleFis) return 'Fiş bilgisi bulunamadı.';

        if (this.duzenleFis.durum !== 'Taslak') {
            return 'Sadece taslak fişler düzenlenebilir.';
        }

        if (!this.duzenleFis.fisTarihi) {
            return 'Fiş tarihi zorunludur.';
        }

        if (this.duzenleFis.maliYil < 2000 || this.duzenleFis.maliYil > 2100) {
            return 'Mali yıl 2000-2100 aralığında olmalıdır.';
        }

        if (this.duzenleFis.donem && (this.duzenleFis.donem < 1 || this.duzenleFis.donem > 12)) {
            return 'Dönem 1-12 aralığında olmalıdır.';
        }

        if (this.duzenleSatirlar.length < 2) {
            return 'Fişte en az iki satır olmalıdır.';
        }

        for (let i = 0; i < this.duzenleSatirlar.length; i++) {
            const s = this.duzenleSatirlar[i];
            if (!s.muhasebeHesapPlaniId || s.muhasebeHesapPlaniId <= 0) {
                return `Her satırda muhasebe hesabı seçilmelidir. (Satır ${i + 1})`;
            }
            if (s.borc > 0 && s.alacak > 0) {
                return `Bir satırda hem borç hem alacak olamaz. (Satır ${i + 1})`;
            }
            if (s.borc < 0 || s.alacak < 0) {
                return `Borç veya alacak negatif olamaz. (Satır ${i + 1})`;
            }
            if (s.borc === 0 && s.alacak === 0) {
                return `Her satırda borç veya alacak tutarı girilmelidir. (Satır ${i + 1})`;
            }
        }

        const toplamBorc = this.duzenleSatirlar.reduce((sum, s) => sum + (s.borc ?? 0), 0);
        const toplamAlacak = this.duzenleSatirlar.reduce((sum, s) => sum + (s.alacak ?? 0), 0);
        if (Math.abs(toplamBorc - toplamAlacak) > 0.009) {
            return 'Fiş dengede olmalıdır. Toplam borç ve toplam alacak eşit olmalıdır.';
        }

        return null;
    }

    // ─── Düzenle dialog denge helpers ──────────────────────────────

    getDuzenleToplamBorc(): number {
        return this.duzenleSatirlar.reduce((sum, s) => sum + (s.borc ?? 0), 0);
    }

    getDuzenleToplamAlacak(): number {
        return this.duzenleSatirlar.reduce((sum, s) => sum + (s.alacak ?? 0), 0);
    }

    getDuzenleFark(): number {
        return this.getDuzenleToplamBorc() - this.getDuzenleToplamAlacak();
    }

    getDuzenleDenklikSeverity(): 'success' | 'danger' {
        return Math.abs(this.getDuzenleFark()) <= 0.009 ? 'success' : 'danger';
    }

    getDuzenleDenklikLabel(): string {
        return Math.abs(this.getDuzenleFark()) <= 0.009 ? 'Dengede' : 'Dengesiz';
    }

    // ─── Ortak ────────────────────────────────────────────────────

    isHighlighted(row: MuhasebeFisModel): boolean {
        if (this.highlightedFisId && row.id === this.highlightedFisId) {
            return true;
        }
        if (this.highlightedFisNo && row.fisNo === this.highlightedFisNo) {
            return true;
        }
        return false;
    }

    getDetayToplamBorc(): number {
        if (!this.detayFis?.satirlar?.length) return 0;
        return this.detayFis.satirlar.reduce((sum, s) => sum + (s.borc ?? 0), 0);
    }

    getDetayToplamAlacak(): number {
        if (!this.detayFis?.satirlar?.length) return 0;
        return this.detayFis.satirlar.reduce((sum, s) => sum + (s.alacak ?? 0), 0);
    }

    getDetayFark(): number {
        return this.getDetayToplamBorc() - this.getDetayToplamAlacak();
    }

    getDenklikSeverity(): 'success' | 'danger' {
        return this.getDetayFark() === 0 ? 'success' : 'danger';
    }

    getDenklikLabel(): string {
        return this.getDetayFark() === 0 ? 'Dengede' : 'Dengesiz';
    }

    getDurumLabel(durum: string): string {
        switch (durum) {
            case 'Taslak':   return 'Taslak';
            case 'Onayli':   return 'Onaylı';
            case 'Iptal':    return 'İptal';
            case 'TersKayit': return 'Ters Kayıt';
            default:         return durum;
        }
    }

    getFisTipiLabel(fisTipi: string): string {
        switch (fisTipi) {
            case 'Mahsup':  return 'Mahsup';
            case 'Tahsil':  return 'Tahsil';
            case 'Tediye':  return 'Tediye';
            case 'Acilis':  return 'Açılış';
            case 'Kapanis': return 'Kapanış';
            default:        return fisTipi;
        }
    }

    getDurumSeverity(durum: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (durum) {
            case 'Onayli':
                return 'success';
            case 'Taslak':
                return 'warn';
            case 'Iptal':
                return 'danger';
            case 'TersKayit':
                return 'info';
            default:
                return 'secondary';
        }
    }

    getFisTipiSeverity(fisTipi: string): 'info' | 'secondary' {
        switch (fisTipi) {
            case 'Mahsup':
                return 'info';
            default:
                return 'secondary';
        }
    }

    getSayfaDurumu(): string {
        const start = (this.filter.page - 1) * this.filter.pageSize + 1;
        const end = Math.min(this.filter.page * this.filter.pageSize, this.totalCount);
        return `${start}-${end} / ${this.totalCount}`;
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error) ?? 'Bir hata oluştu.';
        this.messageService.add({
            severity: UiSeverity.Error,
            summary: 'Hata',
            detail: message,
            life: 6000
        });
    }
}
