import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { AuthService } from '../../auth';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepolarService } from '../depolar/depolar.service';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { StokLotBakiyeModel, StokSeriBakiyeModel } from '../stok-hareketleri/stok-hareketleri.dto';
import { StokHareketleriService } from '../stok-hareketleri/stok-hareketleri.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { AddSarfFisiSatirRequest, CreateSarfFisiRequest, SARF_FISI_DURUMLARI, SarfBirimSecenekModel, SarfFisiModel, SarfFisiSatirModel } from './sarf-fisleri.dto';
import { SarfFisleriService } from './sarf-fisleri.service';

@Component({
    selector: 'app-sarf-fisleri-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        TextareaModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './sarf-fisleri.html',
    providers: [MessageService]
})
export class SarfFisleriPage implements OnInit {
    private readonly service = inject(SarfFisleriService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly stokHareketleriService = inject(StokHareketleriService);
    private readonly authService = inject(AuthService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;
    private tasinirKartMap = new Map<number, TasinirKartModel>();

    loading = false;
    saving = false;
    createDialogVisible = false;
    satirDialogVisible = false;
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    selectedDepoId?: number;

    records: SarfFisiModel[] = [];
    selectedFisi: SarfFisiModel | null = null;
    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    birimOptions: Array<{ label: string; value: number }> = [];

    createModel: CreateSarfFisiRequest = { depoId: 0, sarfTarihi: '', isletmeAlaniId: null, aciklama: null };
    createDate: Date | null = null;
    satirModel: AddSarfFisiSatirRequest = { tasinirKartId: 0, miktar: 1, stokLotId: null, stokSeriId: null, aciklama: null };
    lotOptions: Array<{ label: string; value: number }> = [];
    seriOptions: Array<{ label: string; value: number }> = [];

    readonly durumlar = SARF_FISI_DURUMLARI;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.selectedDepoId = undefined;
        this.selectedFisi = null;
        this.loadReferences();
        this.load();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    get canView(): boolean {
        return this.authService.hasPermission('SarfYonetimi.View') || this.canCreate || this.canFinalize || this.canCancel;
    }

    get canCreate(): boolean {
        return this.authService.hasPermission('SarfYonetimi.Create');
    }

    get canFinalize(): boolean {
        return this.authService.hasPermission('SarfYonetimi.Finalize');
    }

    get canCancel(): boolean {
        return this.authService.hasPermission('SarfYonetimi.Cancel');
    }

    loadReferences(): void {
        const tesisId = this.requireTesisId();
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

        this.service.getBirimler(tesisId).subscribe({
            next: (items: SarfBirimSecenekModel[]) => {
                this.birimOptions = items.map((x) => ({ label: x.ad, value: x.id }));
                this.cdr.detectChanges();
            }
        });
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.requireTesisId();
        if (!tesisId || !this.canView) {
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
                if (this.selectedFisi?.id) {
                    const current = this.records.find((x) => x.id === this.selectedFisi?.id);
                    if (current?.id) {
                        this.selectFisi(current.id);
                    }
                }
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

    selectFisi(id: number): void {
        this.loading = true;
        this.service.getById(id).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedFisi = item;
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openCreate(): void {
        if (!this.canCreate) {
            return;
        }

        this.createDate = new Date();
        this.createModel = {
            depoId: this.selectedDepoId ?? 0,
            sarfTarihi: this.formatDateTimeForApi(this.createDate) ?? '',
            isletmeAlaniId: null,
            aciklama: null
        };
        this.createDialogVisible = true;
    }

    create(): void {
        this.createModel.sarfTarihi = this.formatDateTimeForApi(this.createDate) ?? '';
        this.saving = true;
        this.service.create(this.createModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.createDialogVisible = false;
                this.selectedFisi = item;
                this.load();
                this.selectFisi(item.id!);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Sarf fişi oluşturuldu.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    saveSatirlar(): void {
        if (!this.selectedFisi?.id || !this.canCreate || !this.isDraft(this.selectedFisi)) {
            return;
        }

        this.saving = true;
        this.service.updateSatirlar(this.selectedFisi.id, {
            satirlar: this.selectedFisi.satirlar.filter((x) => x.id).map((x) => ({
                id: x.id!,
                miktar: x.miktar,
                stokLotId: x.stokLotId,
                stokSeriId: x.stokSeriId,
                aciklama: x.aciklama
            }))
        }).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedFisi = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Sarf satırları kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openAddSatir(): void {
        if (!this.selectedFisi || !this.canCreate || !this.isDraft(this.selectedFisi)) {
            return;
        }

        this.satirModel = { tasinirKartId: 0, miktar: 1, stokLotId: null, stokSeriId: null, aciklama: null };
        this.lotOptions = [];
        this.seriOptions = [];
        this.satirDialogVisible = true;
    }

    onSatirKartChange(): void {
        this.satirModel.stokLotId = null;
        this.satirModel.stokSeriId = null;
        this.lotOptions = [];
        this.seriOptions = [];

        const kart = this.getSelectedKart();
        if (kart?.takipTipi === 'Seri') {
            this.satirModel.miktar = 1;
        }

        if (!this.selectedFisi?.depoId || !kart?.id) {
            return;
        }

        if (kart.takipTipi === 'Lot') {
            this.stokHareketleriService.getLotBakiyeleri(this.selectedFisi.depoId, kart.id).subscribe({
                next: (items: StokLotBakiyeModel[]) => {
                    this.lotOptions = items.map((x) => ({ label: `${x.lotNo} (${x.bakiyeMiktari})`, value: x.stokLotId }));
                    this.cdr.detectChanges();
                }
            });
        }

        if (kart.takipTipi === 'Seri') {
            this.stokHareketleriService.getSeriBakiyeleri(this.selectedFisi.depoId, kart.id).subscribe({
                next: (items: StokSeriBakiyeModel[]) => {
                    this.seriOptions = items.map((x) => ({ label: x.seriNo, value: x.stokSeriId }));
                    this.cdr.detectChanges();
                }
            });
        }
    }

    addSatir(): void {
        if (!this.selectedFisi?.id) {
            return;
        }

        this.saving = true;
        this.service.addSatir(this.selectedFisi.id, this.satirModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedFisi = item;
                this.satirDialogVisible = false;
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Sarf satırı eklendi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    deleteSatir(satir: SarfFisiSatirModel): void {
        if (!this.selectedFisi?.id || !satir.id || !this.canCreate || !this.isDraft(this.selectedFisi)) {
            return;
        }

        this.service.deleteSatir(this.selectedFisi.id, satir.id).subscribe({
            next: () => this.selectFisi(this.selectedFisi!.id!),
            error: (error: unknown) => this.showError(error)
        });
    }

    kesinlestir(): void {
        if (!this.selectedFisi?.id || !this.canFinalize || !this.isDraft(this.selectedFisi)) {
            return;
        }

        this.saving = true;
        this.service.updateSatirlar(this.selectedFisi.id, {
            satirlar: this.selectedFisi.satirlar.filter((x) => x.id).map((x) => ({
                id: x.id!,
                miktar: x.miktar,
                stokLotId: x.stokLotId,
                stokSeriId: x.stokSeriId,
                aciklama: x.aciklama
            }))
        }).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (saved) => {
                this.selectedFisi = saved;
                this.service.kesinlestir(saved.id!).subscribe({
                    next: (item) => {
                        this.selectedFisi = item;
                        this.load(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Sarf fişi kesinleştirildi.' });
                    },
                    error: (error: unknown) => this.showError(error)
                });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    iptal(): void {
        if (!this.selectedFisi?.id || !this.canCancel || !this.isDraft(this.selectedFisi)) {
            return;
        }

        this.saving = true;
        this.service.iptal(this.selectedFisi.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedFisi = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Sarf fişi iptal edildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getTrackingLabel(row: Pick<SarfFisiSatirModel, 'lotNo' | 'seriNo'>): string {
        if (row.lotNo?.trim()) {
            return row.lotNo;
        }

        if (row.seriNo?.trim()) {
            return row.seriNo;
        }

        return '-';
    }

    getSelectedDepoLabel(): string {
        if (!this.selectedFisi) {
            return '-';
        }

        return this.depoOptions.find((x) => x.value === this.selectedFisi!.depoId)?.label ?? String(this.selectedFisi.depoId);
    }

    isDraft(row: Pick<SarfFisiModel, 'durum'>): boolean {
        return row.durum === 'Taslak';
    }

    getSelectedKart(): TasinirKartModel | undefined {
        return this.tasinirKartMap.get(this.satirModel.tasinirKartId);
    }

    isLotTrackedSelectedCard(): boolean {
        return this.getSelectedKart()?.takipTipi === 'Lot';
    }

    isSeriTrackedSelectedCard(): boolean {
        return this.getSelectedKart()?.takipTipi === 'Seri';
    }

    private requireTesisId(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyarı', detail: 'Önce çalışma tesisini seçiniz.' });
            return null;
        }
    }

    private formatDateTimeForApi(value: Date | null): string | null {
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

    private showError(error: unknown): void {
        const message = error instanceof HttpErrorResponse
            ? tryReadApiMessage(error.error) ?? error.message
            : error instanceof Error
                ? error.message
                : 'İşlem başarısız.';

        this.messageService.add({ severity: UiSeverity.Danger, summary: 'Hata', detail: message });
    }
}
