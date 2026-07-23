import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    ManuelAktarimGuncellemeRequest,
    POS_VALOR_DURUM_LABELLARI,
    POS_VALOR_DURUM_SEVERITY,
    PosTahsilatValorModel,
    PosTahsilatValorOzetModel,
    PosTahsilatValorTopluOnayBilgisiModel
} from './pos-tahsilat-valor.dto';
import { PosTahsilatValorService } from './pos-tahsilat-valor.service';

@Component({
    selector: 'app-pos-tahsilat-valor-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule, TooltipModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './pos-tahsilat-valor.html',
    providers: [ConfirmationService, MessageService]
})
export class PosTahsilatValorPage implements OnInit {
    private readonly service = inject(PosTahsilatValorService);
    private readonly confirmationService = inject(ConfirmationService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    records: PosTahsilatValorModel[] = [];
    selectedRecords: PosTahsilatValorModel[] = [];
    pageNumber = 1;
    pageSize = 20;
    totalRecords = 0;
    ozet: PosTahsilatValorOzetModel | null = null;

    readonly durumLabellari = POS_VALOR_DURUM_LABELLARI;
    readonly durumSeverity = POS_VALOR_DURUM_SEVERITY;

    manuelDialogVisible = false;
    manuelKayit: PosTahsilatValorModel | null = null;
    manuelKomisyonTutari: number | null = null;
    manuelNetTutar: number | null = null;
    manuelAciklama = '';

    duzeltmeDialogVisible = false;
    duzeltmeKayit: PosTahsilatValorModel | null = null;
    duzeltmeAciklama = '';

    topluOnayBilgisi: PosTahsilatValorTopluOnayBilgisiModel | null = null;
    topluOnayModu: 'secili' | 'valoru-gelenler' | null = null;
    topluOnayDialogVisible = false;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.pageNumber = 1;
            this.load(1, this.pageSize);
            this.loadOzet();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için POS valör listesi yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.load(1, this.pageSize);
                this.loadOzet();
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

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, tesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
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

    loadOzet(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        this.service.getOzet(tesisId).subscribe({
            next: (ozet) => {
                this.ozet = ozet;
                this.cdr.detectChanges();
            }
        });
    }

    refresh(): void {
        this.load();
        this.loadOzet();
    }

    hesabaAktar(item: PosTahsilatValorModel): void {
        if (!item.id) {
            return;
        }

        if (item.durum === 'MutabakatBekliyor') {
            this.openManuelDialog(item);
            return;
        }

        this.service.hesabaAktar(item.id, null).subscribe({
            next: (sonuc) => this.handleTekSonuc(sonuc),
            error: (error: unknown) => this.showError(error)
        });
    }

    openManuelDialog(item: PosTahsilatValorModel): void {
        this.manuelKayit = item;
        this.manuelKomisyonTutari = item.komisyonTutari;
        this.manuelNetTutar = item.netTutar;
        this.manuelAciklama = '';
        this.manuelDialogVisible = true;
    }

    manuelAktarOnayla(): void {
        if (!this.manuelKayit?.id) {
            return;
        }

        const guncelleme: ManuelAktarimGuncellemeRequest = {
            komisyonTutari: this.manuelKomisyonTutari,
            netTutar: this.manuelNetTutar,
            aciklama: this.manuelAciklama || null
        };

        this.service.hesabaAktar(this.manuelKayit.id, guncelleme).subscribe({
            next: (sonuc) => {
                this.manuelDialogVisible = false;
                this.handleTekSonuc(sonuc);
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    yenidenDene(item: PosTahsilatValorModel): void {
        if (!item.id) {
            return;
        }

        this.service.yenidenDene(item.id).subscribe({
            next: (sonuc) => this.handleTekSonuc(sonuc),
            error: (error: unknown) => this.showError(error)
        });
    }

    openDuzeltmeDialog(item: PosTahsilatValorModel): void {
        this.duzeltmeKayit = item;
        this.duzeltmeAciklama = '';
        this.duzeltmeDialogVisible = true;
    }

    duzeltmeOnayla(): void {
        if (!this.duzeltmeKayit?.id || !this.duzeltmeAciklama.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Açıklama zorunludur.' });
            return;
        }

        this.service.duzeltmeTersKayit(this.duzeltmeKayit.id, this.duzeltmeAciklama.trim()).subscribe({
            next: () => {
                this.duzeltmeDialogVisible = false;
                this.refresh();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Düzeltme/ters kayıt oluşturuldu.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openTopluOnaySecili(): void {
        if (this.selectedRecords.length === 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Seçim Yok', detail: 'Lütfen en az bir kayıt seçin.' });
            return;
        }

        this.topluOnayModu = 'secili';
        const idler = this.selectedRecords.map((x) => x.id!).filter(Boolean);
        this.service.getTopluOnayBilgisi({ valorIdler: idler }).subscribe({
            next: (bilgi) => {
                this.topluOnayBilgisi = bilgi;
                this.topluOnayDialogVisible = true;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openTopluOnayValoruGelenler(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        this.topluOnayModu = 'valoru-gelenler';
        this.service.getTopluOnayBilgisi({ tesisId, sadeceValoruGelenler: true }).subscribe({
            next: (bilgi) => {
                this.topluOnayBilgisi = bilgi;
                this.topluOnayDialogVisible = true;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    topluOnayOnayla(): void {
        this.topluOnayDialogVisible = false;
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;

        const request$ = this.topluOnayModu === 'secili'
            ? this.service.seciliHesaplaraAktar(this.selectedRecords.map((x) => x.id!).filter(Boolean))
            : this.service.valoruGelenleriHesabaAktar(tesisId);

        request$.subscribe({
            next: (sonuc) => {
                this.selectedRecords = [];
                this.refresh();
                this.messageService.add({
                    severity: sonuc.hatali.length > 0 ? UiSeverity.Warn : UiSeverity.Success,
                    summary: 'Toplu Aktarım Tamamlandı',
                    detail: `${sonuc.basarili.length} kayıt başarılı, ${sonuc.hatali.length} kayıt hatalı.`
                });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getDurumLabel(durum: string): string {
        return this.durumLabellari[durum] ?? durum;
    }

    getDurumSeverity(durum: string) {
        return this.durumSeverity[durum] ?? 'secondary';
    }

    getValoreKalanText(item: PosTahsilatValorModel): string {
        if (item.durum === 'Aktarildi') {
            return 'Aktarıldı';
        }
        if (item.bugunValorGunuMu) {
            return 'Bugün';
        }
        if (item.valorGectiMi) {
            return `${Math.abs(item.valoreKalanGun)} gün gecikti`;
        }
        return `${item.valoreKalanGun} gün kaldı`;
    }

    private handleTekSonuc(sonuc: { basarili: boolean; hataMesaji?: string | null }): void {
        this.refresh();
        if (sonuc.basarili) {
            this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Hesaba aktarım tamamlandı.' });
        } else {
            this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: sonuc.hataMesaji ?? 'Aktarım başarısız.' });
        }
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
