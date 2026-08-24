import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextBarComponent } from '../muhasebe/components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { KantinCariKartOption, KantinDepoOption, KantinKasaOption, KantinModel, KantinTasinirKartOption, KantinUrunModel } from './kantinler.dto';
import { KantinlerService } from './kantinler.service';

@Component({
    selector: 'app-kantinler-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        TextareaModule,
        ToastModule,
        ToolbarModule,
        ToggleSwitchModule,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './kantinler.html',
    providers: [MessageService]
})
export class KantinlerPage implements OnInit {
    private readonly service = inject(KantinlerService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    urunLoading = false;
    kantinler: KantinModel[] = [];
    urunler: KantinUrunModel[] = [];
    selectedKantin: KantinModel | null = null;

    depoOptions: KantinDepoOption[] = [];
    kasaOptions: KantinKasaOption[] = [];
    cariKartOptions: KantinCariKartOption[] = [];
    tasinirKartOptions: KantinTasinirKartOption[] = [];

    showKantinDialog = false;
    showUrunDialog = false;
    kantinForm: KantinModel = this.createEmptyKantin();
    urunForm: KantinUrunModel = this.createEmptyUrun();

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.selectedKantin = null;
        this.urunler = [];
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

    load(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        this.loading = true;
        this.service.getAll(tesisId)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.kantinler = items;
                    if (this.selectedKantin) {
                        const current = items.find((x) => x.id === this.selectedKantin?.id) ?? null;
                        this.selectedKantin = current;
                    }

                    if (!this.selectedKantin && items.length > 0) {
                        this.selectKantin(items[0]);
                        return;
                    }

                    if (this.selectedKantin?.id) {
                        this.loadUrunler(this.selectedKantin.id);
                    } else {
                        this.urunler = [];
                    }

                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    loadReferences(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.service.getDepolar(tesisId).subscribe({
            next: (items) => {
                this.depoOptions = items;
                this.cdr.detectChanges();
            }
        });

        this.service.getNakitKasalar(tesisId).subscribe({
            next: (items) => {
                this.kasaOptions = items;
                this.cdr.detectChanges();
            }
        });

        this.service.getPerakendeCariKartlar(tesisId).subscribe({
            next: (items) => {
                this.cariKartOptions = items;
                this.cdr.detectChanges();
            }
        });

        this.service.getTasinirKartlar(tesisId).subscribe({
            next: (items) => {
                this.tasinirKartOptions = items;
                this.cdr.detectChanges();
            }
        });
    }

    selectKantin(kantin: KantinModel): void {
        this.selectedKantin = kantin;
        if (kantin.id) {
            this.loadUrunler(kantin.id);
        }
    }

    openNewKantin(): void {
        this.kantinForm = this.createEmptyKantin();
        this.showKantinDialog = true;
    }

    editKantin(kantin: KantinModel): void {
        this.kantinForm = { ...kantin };
        this.showKantinDialog = true;
    }

    saveKantin(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        const payload: KantinModel = {
            ...this.kantinForm,
            tesisId
        };

        const request$ = payload.id
            ? this.service.update(payload.id, payload)
            : this.service.create(payload);

        request$.subscribe({
            next: (saved) => {
                this.showKantinDialog = false;
                this.selectedKantin = saved;
                this.load();
                this.showSuccess('Kantin kaydedildi.');
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openNewUrun(): void {
        if (!this.selectedKantin?.id) {
            return;
        }

        this.urunForm = {
            ...this.createEmptyUrun(),
            kantinId: this.selectedKantin.id
        };
        this.showUrunDialog = true;
    }

    editUrun(urun: KantinUrunModel): void {
        this.urunForm = { ...urun };
        this.showUrunDialog = true;
    }

    saveUrun(): void {
        if (!this.selectedKantin?.id) {
            return;
        }

        const payload: KantinUrunModel = {
            ...this.urunForm,
            kantinId: this.selectedKantin.id
        };

        const request$ = payload.id
            ? this.service.updateUrun(this.selectedKantin.id, payload.id, payload)
            : this.service.createUrun(this.selectedKantin.id, payload);

        request$.subscribe({
            next: () => {
                this.showUrunDialog = false;
                this.loadUrunler(this.selectedKantin!.id!);
                this.showSuccess('Kantin ürünü kaydedildi.');
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadUrunler(kantinId: number): void {
        this.urunLoading = true;
        this.service.getUrunler(kantinId)
            .pipe(finalize(() => {
                this.urunLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.urunler = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    getAktifSeverity(aktifMi: boolean): 'success' | 'secondary' {
        return aktifMi ? 'success' : 'secondary';
    }

    getDepoLabel(depoId?: number | null): string {
        const depo = this.depoOptions.find((x) => x.id === depoId);
        return depo ? `${depo.kod} - ${depo.ad}` : '';
    }

    getKasaLabel(kasaId?: number | null): string {
        const kasa = this.kasaOptions.find((x) => x.id === kasaId);
        return kasa ? `${kasa.kod} - ${kasa.ad}` : '';
    }

    getCariKartLabel(cariKartId?: number | null): string {
        const cari = this.cariKartOptions.find((x) => x.id === cariKartId);
        return cari ? `${cari.cariKodu} - ${cari.unvanAdSoyad}` : '';
    }

    getKartLabel(tasinirKartId?: number | null): string {
        const kart = this.tasinirKartOptions.find((x) => x.id === tasinirKartId);
        return kart ? `${kart.stokKodu} - ${kart.ad}` : '';
    }

    private createEmptyKantin(): KantinModel {
        return {
            tesisId: this.currentTesisId ?? 0,
            depoId: 0,
            varsayilanNakitKasaId: null,
            perakendeCariKartId: null,
            kod: '',
            ad: '',
            aktifMi: true,
            aciklama: null
        };
    }

    private createEmptyUrun(): KantinUrunModel {
        return {
            kantinId: this.selectedKantin?.id ?? 0,
            tasinirKartId: 0,
            satisFiyati: 0,
            aktifMi: true,
            kdvOrani: 0,
            mevcutStok: 0,
            barkod: null,
            aciklama: null,
            siraNo: null
        };
    }

    private showSuccess(detail: string): void {
        this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail });
        this.cdr.detectChanges();
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
