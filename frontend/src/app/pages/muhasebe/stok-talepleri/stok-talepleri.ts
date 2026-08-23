import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepolarService } from '../depolar/depolar.service';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { StokBakiyeModel, StokLotBakiyeModel, StokSeriBakiyeModel } from '../stok-hareketleri/stok-hareketleri.dto';
import { StokHareketleriService } from '../stok-hareketleri/stok-hareketleri.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { AddStokTalepSatirRequest, CreateStokTalepRequest, STOK_TALEP_DURUMLARI, StokTalepModel, StokTalepSatirModel } from './stok-talepleri.dto';
import { StokTalepleriService } from './stok-talepleri.service';

@Component({
    selector: 'app-stok-talepleri-page',
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
        ToastModule,
        ToolbarModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './stok-talepleri.html',
    providers: [MessageService]
})
export class StokTalepleriPage implements OnInit {
    private readonly service = inject(StokTalepleriService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly stokHareketleriService = inject(StokHareketleriService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly router = inject(Router);
    private contextInitialized = false;
    private currentTesisId: number | null = null;
    private tasinirKartMap = new Map<number, TasinirKartModel>();
    private stokBakiyeMap = new Map<string, number>();

    loading = false;
    saving = false;
    createDialogVisible = false;
    satirDialogVisible = false;
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    selectedTalepEdenDepoId?: number;
    selectedKarsilayanDepoId?: number;

    records: StokTalepModel[] = [];
    selectedTalep: StokTalepModel | null = null;
    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    createModel: CreateStokTalepRequest = { talepEdenDepoId: 0, karsilayanDepoId: 0, talepTarihi: '', aciklama: null };
    createDate: Date | null = null;
    satirModel: AddStokTalepSatirRequest = { tasinirKartId: 0, talepMiktari: 1, aciklama: null };
    lotOptionsByLine: Record<number, Array<{ label: string; value: number }>> = {};
    seriOptionsByLine: Record<number, Array<{ label: string; value: number }>> = {};

    readonly durumlar = STOK_TALEP_DURUMLARI;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.pageNumber = 1;
        this.selectedTalep = null;
        this.selectedTalepEdenDepoId = undefined;
        this.selectedKarsilayanDepoId = undefined;
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
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.requireTesisId();
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, tesisId, this.selectedTalepEdenDepoId, this.selectedKarsilayanDepoId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                if (this.selectedTalep?.id) {
                    const current = this.records.find((x) => x.id === this.selectedTalep?.id);
                    if (current?.id) {
                        this.selectTalep(current.id);
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

    onFiltersChanged(): void {
        this.selectedTalep = null;
        this.load(1, this.pageSize);
    }

    selectTalep(id: number): void {
        this.loading = true;
        this.service.getById(id).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.loadCurrentStocks();
                this.loadTrackingSelections();
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
            talepEdenDepoId: this.selectedTalepEdenDepoId ?? 0,
            karsilayanDepoId: this.selectedKarsilayanDepoId ?? 0,
            talepTarihi: this.formatDateTimeForApi(this.createDate) ?? '',
            aciklama: null
        };
        this.createDialogVisible = true;
    }

    create(): void {
        if (!this.createModel.talepEdenDepoId || !this.createModel.karsilayanDepoId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyari', detail: 'Iki depo secimi zorunludur.' });
            return;
        }

        this.createModel.talepTarihi = this.formatDateTimeForApi(this.createDate) ?? '';
        this.saving = true;
        this.service.create(this.createModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.createDialogVisible = false;
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok talebi olusturuldu.' });
                this.selectTalep(item.id!);
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    saveHeader(): void {
        if (!this.selectedTalep?.id || !this.isDraft(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.update(this.selectedTalep.id, {
            talepEdenDepoId: this.selectedTalep.talepEdenDepoId,
            karsilayanDepoId: this.selectedTalep.karsilayanDepoId,
            talepTarihi: this.selectedTalep.talepTarihi,
            aciklama: this.selectedTalep.aciklama
        }).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Talep basligi guncellendi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openAddSatir(): void {
        if (!this.selectedTalep || !this.isDraft(this.selectedTalep)) {
            return;
        }

        this.satirModel = { tasinirKartId: 0, talepMiktari: 1, aciklama: null };
        this.satirDialogVisible = true;
    }

    addSatir(): void {
        if (!this.selectedTalep?.id) {
            return;
        }

        this.saving = true;
        this.service.addSatir(this.selectedTalep.id, this.satirModel).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.satirDialogVisible = false;
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Talep satiri eklendi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    deleteSatir(satir: StokTalepSatirModel): void {
        if (!this.selectedTalep?.id || !satir.id || !this.isDraft(this.selectedTalep)) {
            return;
        }

        this.service.deleteSatir(this.selectedTalep.id, satir.id).subscribe({
            next: () => this.selectTalep(this.selectedTalep!.id!),
            error: (error: unknown) => this.showError(error)
        });
    }

    saveSatirlar(): void {
        if (!this.selectedTalep?.id || !this.canEditLines(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.updateSatirlar(this.selectedTalep.id, {
            satirlar: this.selectedTalep.satirlar.filter((x) => x.id).map((x) => ({
                id: x.id!,
                talepMiktari: x.talepMiktari,
                onaylananMiktar: x.onaylananMiktar,
                aciklama: x.aciklama
            }))
        }).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Talep satirlari kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    gonder(): void {
        if (!this.selectedTalep?.id || !this.isDraft(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.gonder(this.selectedTalep.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok talebi beklemeye alindi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onayDurumunuKaydet(): void {
        this.saveSatirlar();
    }

    reddet(): void {
        if (!this.selectedTalep?.id || !this.canApprove(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.reddet(this.selectedTalep.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok talebi reddedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    teslimEt(): void {
        if (!this.selectedTalep?.id || !this.canDeliver(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.teslimEt(this.selectedTalep.id, {
            satirlar: this.selectedTalep.satirlar.filter((x) => x.id && x.onaylananMiktar > 0).map((x) => ({
                id: x.id!,
                stokLotId: x.stokLotId,
                stokSeriId: x.stokSeriId
            }))
        }).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.loadTrackingSelections();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok talebi teslim edildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    iptal(): void {
        if (!this.selectedTalep?.id || !this.canCancel(this.selectedTalep)) {
            return;
        }

        this.saving = true;
        this.service.iptal(this.selectedTalep.id).pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (item) => {
                this.selectedTalep = item;
                this.load(this.pageNumber, this.pageSize);
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Stok talebi iptal edildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openStokHareketleri(): void {
        this.router.navigate(['/muhasebe/stok-hareketleri']);
    }

    getOnayActionLabel(): string {
        if (!this.selectedTalep) {
            return 'Onay Durumunu Kaydet';
        }

        const aktifSatirlar = this.selectedTalep.satirlar;
        if (aktifSatirlar.length > 0 && aktifSatirlar.every((x) => x.onaylananMiktar === x.talepMiktari)) {
            return 'Onayla';
        }

        if (aktifSatirlar.every((x) => x.onaylananMiktar === 0)) {
            return 'Reddet';
        }

        return 'Kısmi Onayla';
    }

    getCurrentStock(satir: StokTalepSatirModel): number {
        if (!this.selectedTalep) {
            return 0;
        }

        return this.stokBakiyeMap.get(`${this.selectedTalep.karsilayanDepoId}:${satir.tasinirKartId}`) ?? 0;
    }

    getLineSeverity(durum: string): string {
        switch (durum) {
            case 'TeslimEdildi':
                return UiSeverity.Success;
            case 'KismiOnaylandi':
                return UiSeverity.Warn;
            case 'Reddedildi':
            case 'Iptal':
                return UiSeverity.Danger;
            case 'Onaylandi':
                return UiSeverity.Info;
            default:
                return UiSeverity.Secondary;
        }
    }

    isDraft(talep: StokTalepModel): boolean {
        return talep.durum === 'Taslak';
    }

    canEditLines(talep: StokTalepModel): boolean {
        return talep.durum === 'Taslak' || talep.durum === 'Bekliyor' || talep.durum === 'Onaylandi' || talep.durum === 'KismiOnaylandi';
    }

    canApprove(talep: StokTalepModel): boolean {
        return talep.durum === 'Bekliyor' || talep.durum === 'Onaylandi' || talep.durum === 'KismiOnaylandi';
    }

    canDeliver(talep: StokTalepModel): boolean {
        return talep.durum === 'Onaylandi' || talep.durum === 'KismiOnaylandi';
    }

    canCancel(talep: StokTalepModel): boolean {
        return talep.durum !== 'TeslimEdildi' && talep.durum !== 'Iptal';
    }

    private loadCurrentStocks(): void {
        if (!this.selectedTalep) {
            this.stokBakiyeMap.clear();
            return;
        }

        const tesisId = this.requireTesisId();
        if (!tesisId) {
            return;
        }

        this.stokHareketleriService.getStokBakiye(tesisId, this.selectedTalep.karsilayanDepoId).subscribe({
            next: (items) => {
                this.stokBakiyeMap = new Map(items.map((x: StokBakiyeModel) => [`${x.depoId}:${x.tasinirKartId}`, x.bakiyeMiktari]));
                this.cdr.detectChanges();
            }
        });
    }

    private loadTrackingSelections(): void {
        if (!this.selectedTalep || !this.canDeliver(this.selectedTalep)) {
            this.lotOptionsByLine = {};
            this.seriOptionsByLine = {};
            return;
        }

        for (const satir of this.selectedTalep.satirlar.filter((x) => x.id && x.onaylananMiktar > 0)) {
            if (satir.takipTipi === 'Lot') {
                this.stokHareketleriService.getLotBakiyeleri(this.selectedTalep.karsilayanDepoId, satir.tasinirKartId).subscribe({
                    next: (items) => {
                        this.lotOptionsByLine[satir.id!] = items.map((x: StokLotBakiyeModel) => ({
                            label: `${x.lotNo} (${x.bakiyeMiktari})`,
                            value: x.stokLotId
                        }));
                        if (!satir.stokLotId && items.length === 1) {
                            satir.stokLotId = items[0].stokLotId;
                        }
                        this.cdr.detectChanges();
                    }
                });
            }

            if (satir.takipTipi === 'Seri') {
                this.stokHareketleriService.getSeriBakiyeleri(this.selectedTalep.karsilayanDepoId, satir.tasinirKartId).subscribe({
                    next: (items) => {
                        this.seriOptionsByLine[satir.id!] = items.map((x: StokSeriBakiyeModel) => ({
                            label: x.seriNo,
                            value: x.stokSeriId
                        }));
                        if (!satir.stokSeriId && items.length === 1) {
                            satir.stokSeriId = items[0].stokSeriId;
                        }
                        this.cdr.detectChanges();
                    }
                });
            }
        }
    }

    private requireTesisId(): number | null {
        return this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
    }

    private formatDateTimeForApi(value: Date | null): string | null {
        if (!value) {
            return null;
        }

        const year = value.getFullYear();
        const month = `${value.getMonth() + 1}`.padStart(2, '0');
        const day = `${value.getDate()}`.padStart(2, '0');
        const hours = `${value.getHours()}`.padStart(2, '0');
        const minutes = `${value.getMinutes()}`.padStart(2, '0');
        return `${year}-${month}-${day}T${hours}:${minutes}:00`;
    }

    private showError(error: unknown): void {
        const message = error instanceof HttpErrorResponse
            ? tryReadApiMessage(error.error) ?? error.message
            : error instanceof Error
                ? error.message
                : 'Islem sirasinda beklenmeyen bir hata olustu.';

        this.messageService.add({
            severity: UiSeverity.Danger,
            summary: 'Hata',
            detail: message
        });
    }
}
