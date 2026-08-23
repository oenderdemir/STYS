import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { concatMap, finalize, Observable, tap } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepolarService } from '../depolar/depolar.service';
import { StokMaliyetPolitikasiDialogComponent } from '../components/stok-maliyet-politikasi-dialog/stok-maliyet-politikasi-dialog.component';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { parseApiDate } from '../models/muhasebe-fis.model';
import { CurrentStokMaliyetPolitikasiModel } from '../stok-hareketleri/stok-hareketleri.dto';
import { StokMaliyetPolitikasiService } from '../services/stok-maliyet-politikasi.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { AddStokSayimSatirRequest, CreateStokSayimRequest, STOK_SAYIM_DURUMLARI, StokSayimModel, StokSayimSatirModel } from './stok-sayimlari.dto';
import { StokSayimlariService } from './stok-sayimlari.service';

@Component({
    selector: 'app-stok-sayimlari-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        StokMaliyetPolitikasiDialogComponent,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './stok-sayimlari.html',
    providers: [MessageService, ConfirmationService]
})
export class StokSayimlariPage implements OnInit {
    private readonly service = inject(StokSayimlariService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly stokMaliyetPolitikasiService = inject(StokMaliyetPolitikasiService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;
    private tasinirKartMap = new Map<number, TasinirKartModel>();

    loading = false;
    saving = false;
    maliyetPolitikasiSaving = false;
    createDialogVisible = false;
    satirDialogVisible = false;
    maliyetPolitikasiDialogVisible = false;
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    selectedDepoId?: number;
    showOnlyDifferences = false;

    records: StokSayimModel[] = [];
    selectedSayim: StokSayimModel | null = null;
    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    currentMaliyetPolitikasi: CurrentStokMaliyetPolitikasiModel | null = null;
    secilenMaliyetYontemi = 'AgirlikliOrtalama';

    createModel: CreateStokSayimRequest = { depoId: 0, sayimTarihi: '', aciklama: null };
    createDate: Date | null = null;

    satirModel: AddStokSayimSatirRequest = { tasinirKartId: 0, sayilanMiktar: 1, lotNo: null, seriNo: null, sonKullanmaTarihi: null };
    satirDate: Date | null = null;

    readonly durumlar = STOK_SAYIM_DURUMLARI;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.pageNumber = 1;
        this.selectedDepoId = undefined;
        this.selectedSayim = null;
        this.loadReferences();
        if (tesisId) {
            this.loadCurrentMaliyetPolitikasi(tesisId);
        }
        this.load();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
                if (this.currentTesisId) {
                    this.loadCurrentMaliyetPolitikasi(this.currentTesisId);
                }
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    loadReferences(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.depolarService.getAll(tesisId).subscribe({
            next: (items) => {
                this.depoOptions = items.filter((x) => x.aktifMi).map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });

        this.tasinirKartService.getAll().subscribe({
            next: (items) => {
                const aktifler = items.filter((x) => x.aktifMi && (!x.tesisId || x.tesisId === tesisId));
                this.tasinirKartOptions = aktifler.map((x) => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
                this.tasinirKartMap = new Map(aktifler.map((x) => [x.id!, x]));
                this.cdr.detectChanges();
            }
        });
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, tesisId, this.selectedDepoId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                if (this.selectedSayim?.id) {
                    const current = this.records.find((x) => x.id === this.selectedSayim?.id);
                    if (current) {
                        this.selectSayim(current.id!);
                    }
                }
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    onDepoFilterChange(): void {
        this.selectedSayim = null;
        this.load(1, this.pageSize);
    }

    selectSayim(id: number): void {
        this.loading = true;
        this.service.getById(id).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedSayim = item;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openCreate(): void {
        if (!this.requireTesisId()) {
            return;
        }

        this.createDate = new Date();
        this.createModel = {
            depoId: this.selectedDepoId ?? 0,
            sayimTarihi: this.formatDateTimeForApi(this.createDate) ?? '',
            aciklama: null
        };
        this.createDialogVisible = true;
    }

    create(): void {
        if (!this.createModel.depoId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyari', detail: 'Depo seçimi zorunludur.' });
            return;
        }

        this.createModel.sayimTarihi = this.formatDateTimeForApi(this.createDate) ?? '';
        this.saving = true;
        this.service.create(this.createModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.createDialogVisible = false;
                this.selectedSayim = item;
                this.load();
                this.selectSayim(item.id!);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok sayımı oluşturuldu.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    saveSatirlar(): void {
        if (!this.selectedSayim?.id || !this.isDraft(this.selectedSayim)) {
            return;
        }

        this.saving = true;
        this.persistSatirlar$().pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Sayım satırları kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private persistSatirlar$(): Observable<StokSayimModel> {
        if (!this.selectedSayim?.id || !this.isDraft(this.selectedSayim)) {
            throw new Error('Taslak stok sayımı seçilmedi.');
        }

        return this.service.updateSatirlar(this.selectedSayim.id, {
            satirlar: this.selectedSayim.satirlar.filter((x) => x.id).map((x) => ({ id: x.id!, sayilanMiktar: x.sayilanMiktar }))
        }).pipe(
            tap((item) => {
                this.selectedSayim = item;
                this.load(this.pageNumber, this.pageSize);
            }));
    }

    openAddSatir(): void {
        if (!this.selectedSayim || !this.isDraft(this.selectedSayim)) {
            return;
        }

        this.satirModel = { tasinirKartId: 0, sayilanMiktar: 1, lotNo: null, seriNo: null, sonKullanmaTarihi: null };
        this.satirDate = null;
        this.satirDialogVisible = true;
    }

    addSatir(): void {
        if (!this.selectedSayim?.id) {
            return;
        }

        this.satirModel.sonKullanmaTarihi = this.formatDateOnlyForApi(this.satirDate);
        this.saving = true;
        this.service.addSatir(this.selectedSayim.id, this.satirModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedSayim = item;
                this.satirDialogVisible = false;
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Sayım satırı eklendi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    deleteSatir(satir: StokSayimSatirModel): void {
        if (!this.selectedSayim?.id || !satir.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Bu sayım satırı silinsin mi?',
            accept: () => {
                this.service.deleteSatir(this.selectedSayim!.id!, satir.id!).subscribe({
                    next: () => this.selectSayim(this.selectedSayim!.id!),
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    refresh(): void {
        if (!this.selectedSayim?.id) {
            return;
        }

        this.saving = true;
        this.service.refresh(this.selectedSayim.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedSayim = item;
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Sayım snapshot yenilendi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    kesinlestir(): void {
        if (!this.selectedSayim?.id || !this.isDraft(this.selectedSayim)) {
            return;
        }

        this.saving = true;
        this.persistSatirlar$().pipe(
            concatMap((savedItem) => {
                this.selectedSayim = savedItem;
                return this.service.kesinlestir(savedItem.id!);
            }),
            finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            })
        ).subscribe({
            next: (item) => {
                this.selectedSayim = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Sayım kesinleştirildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    iptal(): void {
        if (!this.selectedSayim?.id) {
            return;
        }

        this.saving = true;
        this.service.iptal(this.selectedSayim.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedSayim = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Sayım iptal edildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getVisibleSatirlar(): StokSayimSatirModel[] {
        if (!this.selectedSayim) {
            return [];
        }

        return this.showOnlyDifferences
            ? this.selectedSayim.satirlar.filter((x) => x.farkMiktari !== 0)
            : this.selectedSayim.satirlar;
    }

    getTrackingLabel(row: Pick<StokSayimSatirModel, 'lotNo' | 'seriNo'>): string {
        if (row.lotNo?.trim()) {
            return row.lotNo;
        }

        if (row.seriNo?.trim()) {
            return row.seriNo;
        }

        return '-';
    }

    getMaliyetPolitikasiLabel(): string {
        if (!this.currentMaliyetPolitikasi) {
            return 'Maliyet yöntemi yükleniyor';
        }

        if (!this.currentMaliyetPolitikasi.politikaSecildiMi || !this.currentMaliyetPolitikasi.maliyetYontemi) {
            return 'Maliyet yöntemi seçilmedi';
        }

        return `${this.currentMaliyetPolitikasi.maliYil} Maliyet Yöntemi: ${this.getMaliyetYontemiLabel(this.currentMaliyetPolitikasi.maliyetYontemi)}`;
    }

    getMaliyetYontemiLabel(value: string | null | undefined): string {
        switch (value) {
            case 'AgirlikliOrtalama':
                return 'Ağırlıklı Ortalama';
            case 'FIFO':
                return 'FIFO';
            case 'LIFO':
                return 'LIFO';
            default:
                return value ?? '-';
        }
    }

    saveMaliyetPolitikasi(): void {
        const tesisId = this.requireTesisId();
        if (tesisId === null || !this.currentMaliyetPolitikasi) {
            return;
        }

        this.maliyetPolitikasiSaving = true;
        this.stokMaliyetPolitikasiService.upsert({
            tesisId,
            maliYil: this.currentMaliyetPolitikasi.maliYil,
            maliyetYontemi: this.secilenMaliyetYontemi
        }).pipe(finalize(() => {
            this.maliyetPolitikasiSaving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (result) => {
                this.currentMaliyetPolitikasi = {
                    tesisId: result.tesisId,
                    maliYil: result.maliYil,
                    maliyetYontemi: result.maliyetYontemi,
                    politikaSecildiMi: true
                };
                this.maliyetPolitikasiDialogVisible = false;
                this.messageService.add({
                    severity: UiSeverity.Success,
                    summary: 'Maliyet Yöntemi Kaydedildi',
                    detail: `${result.maliYil} mali yılı için stok maliyet yöntemi kaydedildi.`
                });
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getDurumSeverity(durum: string): 'success' | 'info' | 'danger' {
        switch (durum) {
            case 'Kesinlesti':
                return 'success';
            case 'Iptal':
                return 'danger';
            default:
                return 'info';
        }
    }

    isDraft(row: Pick<StokSayimModel, 'durum'>): boolean {
        return row.durum === 'Taslak';
    }

    getSelectedDepoLabel(): string {
        if (!this.selectedSayim) {
            return '-';
        }

        return this.depoOptions.find((x) => x.value === this.selectedSayim!.depoId)?.label ?? String(this.selectedSayim.depoId);
    }

    isLotTrackedSelectedCard(): boolean {
        const kart = this.satirModel.tasinirKartId ? this.tasinirKartMap.get(this.satirModel.tasinirKartId) : null;
        return kart?.takipTipi === 'Lot';
    }

    isSeriTrackedSelectedCard(): boolean {
        const kart = this.satirModel.tasinirKartId ? this.tasinirKartMap.get(this.satirModel.tasinirKartId) : null;
        return kart?.takipTipi === 'Seri';
    }

    onSatirKartChange(): void {
        if (this.isSeriTrackedSelectedCard()) {
            this.satirModel.sayilanMiktar = 1;
            this.satirModel.lotNo = null;
            this.satirModel.sonKullanmaTarihi = null;
        } else if (this.isLotTrackedSelectedCard()) {
            this.satirModel.seriNo = null;
        } else {
            this.satirModel.lotNo = null;
            this.satirModel.sonKullanmaTarihi = null;
            this.satirModel.seriNo = null;
        }
    }

    private requireTesisId(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyari', detail: 'Önce çalışma tesisini seçiniz.' });
            return null;
        }
    }

    private formatDateTimeForApi(value: Date | null | undefined): string | null {
        if (!value) {
            return null;
        }

        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        const hour = String(value.getHours()).padStart(2, '0');
        const minute = String(value.getMinutes()).padStart(2, '0');
        const second = String(value.getSeconds()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}:${second}`;
    }

    private formatDateOnlyForApi(value: Date | null | undefined): string | null {
        if (!value) {
            return null;
        }

        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}T00:00:00`;
    }

    toDate(value: string | null | undefined): Date | null {
        return parseApiDate(value);
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }

    private loadCurrentMaliyetPolitikasi(tesisId: number, tarih: string = new Date().toISOString()): void {
        this.stokMaliyetPolitikasiService.getCurrent(tesisId, tarih).subscribe({
            next: (item) => {
                this.currentMaliyetPolitikasi = item;
                this.secilenMaliyetYontemi = item.maliyetYontemi ?? 'AgirlikliOrtalama';
                this.maliyetPolitikasiDialogVisible = !item.politikaSecildiMi;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.currentMaliyetPolitikasi = null;
                this.maliyetPolitikasiDialogVisible = false;
                this.showError(error);
            }
        });
    }
}
