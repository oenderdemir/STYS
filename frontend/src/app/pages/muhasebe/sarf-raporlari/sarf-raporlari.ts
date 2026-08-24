import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepolarService } from '../depolar/depolar.service';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { SarfBirimSecenekModel, SarfOdaSecenekModel } from '../sarf-fisleri/sarf-fisleri.dto';
import { SarfFisleriService } from '../sarf-fisleri/sarf-fisleri.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { SarfDetayPagedModel, SARF_RAPOR_DURUMLARI, SarfRaporFilterModel, SarfTuketimDetayRaporSatirModel, SarfTuketimKullanimYeriOzetModel, SarfTuketimMalzemeOzetModel } from './sarf-raporlari.dto';
import { SarfRaporlariService } from './sarf-raporlari.service';

@Component({
    selector: 'app-sarf-raporlari-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DatePickerModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './sarf-raporlari.html',
    providers: [MessageService]
})
export class SarfRaporlariPage implements OnInit {
    private readonly service = inject(SarfRaporlariService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartlariService = inject(TasinirKartlariService);
    private readonly sarfFisleriService = inject(SarfFisleriService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    detailLoading = false;
    exporting = false;
    activeTab: 'detay' | 'malzeme' | 'kullanim' = 'detay';
    pageNumber = 1;
    pageSize = 20;
    totalRecords = 0;

    detayRows: SarfTuketimDetayRaporSatirModel[] = [];
    malzemeRows: SarfTuketimMalzemeOzetModel[] = [];
    kullanimRows: SarfTuketimKullanimYeriOzetModel[] = [];

    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    birimOptions: Array<{ label: string; value: number }> = [];
    odaOptions: Array<{ label: string; value: number }> = [];
    durumOptions = [{ label: 'Kesinleşti', value: 'Kesinlesti' }, ...SARF_RAPOR_DURUMLARI.filter(x => x.value !== 'Kesinlesti')];

    selectedBaslangic = this.ayinIlkGunu();
    selectedBitis = this.ayinSonGunu();
    selectedDepoId?: number;
    selectedTasinirKartId?: number;
    selectedIsletmeAlaniId?: number;
    selectedOdaId?: number;
    selectedDurum = 'Kesinlesti';
    sarfNedeni = '';

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.clearFilters(false);
        this.loadReferences();
        this.loadAll();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
                this.loadAll();
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
                this.depoOptions = items.filter(x => x.aktifMi).map(x => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });

        this.tasinirKartlariService.getAll(tesisId).subscribe({
            next: (items) => {
                this.tasinirKartOptions = items.filter(x => x.aktifMi).map(x => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });

        this.sarfFisleriService.getBirimler(tesisId).subscribe({
            next: (items: SarfBirimSecenekModel[]) => {
                this.birimOptions = items.map(x => ({ label: x.ad, value: x.id }));
                this.cdr.detectChanges();
            }
        });

        this.sarfFisleriService.getOdalar(tesisId).subscribe({
            next: (items: SarfOdaSecenekModel[]) => {
                this.odaOptions = items.map(x => ({ label: x.ad, value: x.id }));
                this.cdr.detectChanges();
            }
        });
    }

    loadAll(): void {
        this.pageNumber = 1;
        this.loadDetay();
        this.loadOzetler();
    }

    loadDetay(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const filter = this.buildFilter();
        if (!filter) {
            return;
        }

        this.detailLoading = true;
        this.service.getDetay(filter, pageNumber, pageSize)
            .pipe(finalize(() => {
                this.detailLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (paged: SarfDetayPagedModel) => {
                    this.detayRows = paged.items;
                    this.pageNumber = paged.pageNumber;
                    this.pageSize = paged.pageSize;
                    this.totalRecords = paged.totalCount;
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    loadOzetler(): void {
        const filter = this.buildFilter();
        if (!filter) {
            return;
        }

        this.loading = true;
        forkJoin({
            malzeme: this.service.getMalzemeOzet(filter),
            kullanim: this.service.getKullanimYeriOzet(filter)
        }).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: ({ malzeme, kullanim }) => {
                this.malzemeRows = malzeme;
                this.kullanimRows = kullanim;
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.loadDetay(nextPageNumber, nextPageSize);
    }

    clearFilters(reload = true): void {
        this.selectedBaslangic = this.ayinIlkGunu();
        this.selectedBitis = this.ayinSonGunu();
        this.selectedDepoId = undefined;
        this.selectedTasinirKartId = undefined;
        this.selectedIsletmeAlaniId = undefined;
        this.selectedOdaId = undefined;
        this.selectedDurum = 'Kesinlesti';
        this.sarfNedeni = '';
        if (reload) {
            this.loadAll();
        }
    }

    exportActiveTab(): void {
        const filter = this.buildFilter();
        if (!filter) {
            return;
        }

        this.exporting = true;
        const request = this.activeTab === 'malzeme'
            ? this.service.exportMalzemeOzetExcel(filter)
            : this.activeTab === 'kullanim'
                ? this.service.exportKullanimYeriOzetExcel(filter)
                : this.service.exportDetayExcel(filter);

        request.pipe(finalize(() => {
            this.exporting = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (blob) => this.downloadBlob(blob, this.buildFileName()),
            error: (error: unknown) => this.showError(error)
        });
    }

    onTabChange(value: string | number | undefined): void {
        if (value === 'malzeme' || value === 'kullanim' || value === 'detay') {
            this.activeTab = value;
        }
    }

    getDurumSeverity(durum: string): 'success' | 'danger' | 'warn' | 'secondary' {
        switch (durum) {
            case 'Kesinlesti':
                return 'success';
            case 'IptalEdildi':
                return 'warn';
            case 'Taslak':
                return 'secondary';
            default:
                return 'secondary';
        }
    }

    formatKullanimYeri(row: SarfTuketimDetayRaporSatirModel): string {
        const parts = [row.isletmeAlaniAd, row.odaAd].filter(x => !!x);
        return parts.length > 0 ? parts.join(' / ') : '-';
    }

    private buildFilter(): SarfRaporFilterModel | null {
        const tesisId = this.requireTesisId();
        if (!tesisId) {
            return null;
        }

        if (this.selectedBaslangic && this.selectedBitis && this.selectedBaslangic > this.selectedBitis) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyarı', detail: 'Başlangıç tarihi bitiş tarihinden büyük olamaz.' });
            this.cdr.detectChanges();
            return null;
        }

        return {
            tesisId,
            baslangicTarihi: this.formatDate(this.selectedBaslangic),
            bitisTarihi: this.formatDate(this.selectedBitis),
            depoId: this.selectedDepoId ?? null,
            tasinirKartId: this.selectedTasinirKartId ?? null,
            isletmeAlaniId: this.selectedIsletmeAlaniId ?? null,
            odaId: this.selectedOdaId ?? null,
            sarfNedeni: this.sarfNedeni?.trim() || null,
            durum: this.selectedDurum
        };
    }

    private requireTesisId(): number | null {
        return this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
    }

    private ayinIlkGunu(): Date {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth(), 1);
    }

    private ayinSonGunu(): Date {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth() + 1, 0);
    }

    private formatDate(date: Date | null | undefined): string | null {
        if (!date) return null;
        const year = date.getFullYear();
        const month = `${date.getMonth() + 1}`.padStart(2, '0');
        const day = `${date.getDate()}`.padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    private buildFileName(): string {
        const start = this.formatDate(this.selectedBaslangic) ?? 'tum-tarih';
        const end = this.formatDate(this.selectedBitis) ?? 'tum-tarih';
        switch (this.activeTab) {
            case 'malzeme':
                return `sarf-tuketim-malzeme-ozet-${start}-${end}.xlsx`;
            case 'kullanim':
                return `sarf-tuketim-kullanim-yeri-ozet-${start}-${end}.xlsx`;
            default:
                return `sarf-tuketim-detay-${start}-${end}.xlsx`;
        }
    }

    private downloadBlob(blob: Blob, fileName: string): void {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        window.URL.revokeObjectURL(url);
    }

    private showError(error: unknown): void {
        const detail = error instanceof HttpErrorResponse
            ? error.error?.message ?? error.message
            : error instanceof Error
                ? error.message
                : 'İşlem tamamlanamadı.';

        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail });
        this.cdr.detectChanges();
    }
}
