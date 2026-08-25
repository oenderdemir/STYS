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
import { KantinCariKartOption, KantinDepoOption, KantinKasaOption, KantinModel, KantinOdemeHesapOption, KantinSatisNoktasiModel, KantinTasinirKartOption, KantinUrunModel } from './kantinler.dto';
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
    styles: [`
        :host {
            display: block;
        }

        .kantin-shell {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .kantin-hero {
            border-radius: 1.5rem;
            padding: 1.5rem;
            background:
                radial-gradient(circle at top left, rgba(16, 185, 129, 0.18), transparent 28%),
                linear-gradient(135deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
            border: 1px solid rgba(148, 163, 184, 0.2);
            box-shadow: 0 18px 45px rgba(15, 23, 42, 0.08);
        }

        .hero-kicker {
            display: inline-flex;
            align-items: center;
            gap: 0.5rem;
            padding: 0.45rem 0.8rem;
            border-radius: 999px;
            background: rgba(15, 23, 42, 0.06);
            color: #334155;
            font-size: 0.85rem;
            font-weight: 600;
            letter-spacing: 0.02em;
        }

        .hero-title {
            margin: 0;
            color: #0f172a;
            font-size: 2rem;
            line-height: 1.1;
        }

        .hero-copy {
            margin: 0.75rem 0 0;
            max-width: 54rem;
            color: #475569;
            line-height: 1.6;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            gap: 1rem;
        }

        .stat-card {
            min-height: 7rem;
            border-radius: 1.25rem;
            padding: 1.1rem 1.2rem;
            background: rgba(255, 255, 255, 0.82);
            border: 1px solid rgba(148, 163, 184, 0.18);
        }

        .stat-label {
            color: #64748b;
            font-size: 0.82rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.06em;
        }

        .stat-value {
            margin-top: 0.65rem;
            color: #0f172a;
            font-size: 1.9rem;
            font-weight: 700;
        }

        .section-card {
            height: 100%;
            border-radius: 1.4rem;
            background: rgba(255, 255, 255, 0.94);
            border: 1px solid rgba(148, 163, 184, 0.16);
            box-shadow: 0 18px 38px rgba(15, 23, 42, 0.06);
            overflow: hidden;
        }

        .section-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            gap: 1rem;
            padding: 1.2rem 1.35rem 0;
        }

        .section-title {
            margin: 0;
            color: #0f172a;
            font-size: 1.25rem;
            font-weight: 700;
        }

        .section-subtitle {
            margin: 0.35rem 0 0;
            color: #64748b;
            font-size: 0.95rem;
        }

        .section-body {
            padding: 1.2rem 1.35rem 1.35rem;
        }

        .detail-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 1rem;
        }

        .detail-tile {
            border-radius: 1rem;
            padding: 1rem;
            background: linear-gradient(180deg, rgba(248, 250, 252, 0.92), rgba(255, 255, 255, 0.98));
            border: 1px solid rgba(226, 232, 240, 0.9);
        }

        .detail-label {
            color: #64748b;
            font-size: 0.82rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }

        .detail-value {
            margin-top: 0.55rem;
            color: #0f172a;
            font-size: 1.1rem;
            font-weight: 600;
            line-height: 1.4;
            word-break: break-word;
        }

        .empty-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 0.85rem;
            min-height: 16rem;
            padding: 2rem;
            text-align: center;
            color: #64748b;
        }

        .empty-state i {
            font-size: 2rem;
            color: #10b981;
        }

        .empty-title {
            margin: 0;
            color: #0f172a;
            font-size: 1.1rem;
            font-weight: 700;
        }

        .soft-panel {
            border-radius: 1rem;
            padding: 1rem 1.1rem;
            background: rgba(248, 250, 252, 0.8);
            border: 1px solid rgba(226, 232, 240, 0.9);
        }

        .dialog-shell {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .dialog-banner {
            padding: 1rem 1.1rem;
            border-radius: 1rem;
            background: linear-gradient(135deg, rgba(16, 185, 129, 0.12), rgba(59, 130, 246, 0.08));
            border: 1px solid rgba(148, 163, 184, 0.18);
        }

        .dialog-banner-title {
            margin: 0;
            color: #0f172a;
            font-size: 1.05rem;
            font-weight: 700;
        }

        .dialog-banner-copy {
            margin: 0.35rem 0 0;
            color: #475569;
            line-height: 1.5;
        }

        .dialog-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 1rem;
        }

        .dialog-field {
            display: flex;
            flex-direction: column;
            gap: 0.45rem;
        }

        .dialog-field.full {
            grid-column: 1 / -1;
        }

        .dialog-label {
            color: #334155;
            font-size: 0.92rem;
            font-weight: 600;
        }

        .dialog-help {
            color: #64748b;
            font-size: 0.82rem;
            line-height: 1.4;
        }

        .dialog-switch-row {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 1rem;
            padding: 0.95rem 1rem;
            border-radius: 1rem;
            background: rgba(248, 250, 252, 0.9);
            border: 1px solid rgba(226, 232, 240, 0.9);
        }

        .dialog-footer {
            display: flex;
            justify-content: flex-end;
            gap: 0.75rem;
        }

        @media (max-width: 991px) {
            .stats-grid,
            .detail-grid,
            .dialog-grid {
                grid-template-columns: 1fr;
            }

            .section-header {
                flex-direction: column;
                align-items: stretch;
            }
        }
    `],
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
    satisNoktasiLoading = false;
    kantinler: KantinModel[] = [];
    urunler: KantinUrunModel[] = [];
    satisNoktalari: KantinSatisNoktasiModel[] = [];
    selectedKantin: KantinModel | null = null;

    depoOptions: KantinDepoOption[] = [];
    kasaOptions: KantinKasaOption[] = [];
    posOptions: KantinOdemeHesapOption[] = [];
    cariKartOptions: KantinCariKartOption[] = [];
    tasinirKartOptions: KantinTasinirKartOption[] = [];

    showKantinDialog = false;
    showUrunDialog = false;
    showSatisNoktasiDialog = false;
    kantinForm: KantinModel = this.createEmptyKantin();
    urunForm: KantinUrunModel = this.createEmptyUrun();
    satisNoktasiForm: KantinSatisNoktasiModel = this.createEmptySatisNoktasi();

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
                        this.loadSatisNoktalari(this.selectedKantin.id);
                    } else {
                        this.urunler = [];
                        this.satisNoktalari = [];
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

        this.service.getPosHesaplari(tesisId).subscribe({
            next: (items) => {
                this.posOptions = items;
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
        this.satisNoktalari = [];
        if (kantin.id) {
            this.loadUrunler(kantin.id);
            this.loadSatisNoktalari(kantin.id);
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

    openNewSatisNoktasi(): void {
        if (!this.selectedKantin?.id) {
            return;
        }

        this.satisNoktasiForm = {
            ...this.createEmptySatisNoktasi(),
            kantinId: this.selectedKantin.id
        };
        this.showSatisNoktasiDialog = true;
    }

    editSatisNoktasi(nokta: KantinSatisNoktasiModel): void {
        this.satisNoktasiForm = { ...nokta };
        this.showSatisNoktasiDialog = true;
    }

    saveSatisNoktasi(): void {
        if (!this.selectedKantin?.id) {
            return;
        }

        const kantinId = this.selectedKantin.id;
        const payload: KantinSatisNoktasiModel = {
            ...this.satisNoktasiForm,
            kantinId
        };

        const request$ = payload.id
            ? this.service.updateSatisNoktasi(kantinId, payload.id, payload)
            : this.service.createSatisNoktasi(kantinId, payload);

        request$.subscribe({
            next: () => {
                this.showSatisNoktasiDialog = false;
                this.loadSatisNoktalari(kantinId);
                this.showSuccess('Satış noktası kaydedildi.');
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadSatisNoktalari(kantinId: number): void {
        this.satisNoktasiLoading = true;
        this.service.getSatisNoktalari(kantinId)
            .pipe(finalize(() => {
                this.satisNoktasiLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.satisNoktalari = items;
                    this.cdr.detectChanges();
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

    get aktifKantinSayisi(): number {
        return this.kantinler.filter((x) => x.aktifMi).length;
    }

    get pasifKantinSayisi(): number {
        return this.kantinler.filter((x) => !x.aktifMi).length;
    }

    get aktifUrunSayisi(): number {
        return this.urunler.filter((x) => x.aktifMi).length;
    }

    get selectedKantinDisplay(): string {
        if (!this.selectedKantin) {
            return 'Henüz kantin seçilmedi';
        }

        return `${this.selectedKantin.kod} - ${this.selectedKantin.ad}`;
    }

    getDepoLabel(depoId?: number | null): string {
        const depo = this.depoOptions.find((x) => x.id === depoId);
        return depo ? `${depo.kod} - ${depo.ad}` : '';
    }

    getKasaLabel(kasaId?: number | null): string {
        const kasa = this.kasaOptions.find((x) => x.id === kasaId);
        return kasa ? `${kasa.kod} - ${kasa.ad}` : '';
    }

    getPosLabel(posHesapId?: number | null): string {
        const pos = this.posOptions.find((x) => x.id === posHesapId);
        return pos ? `${pos.kod} - ${pos.ad}` : '';
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
            perakendeCariKartId: null,
            kod: '',
            ad: '',
            aktifMi: true,
            aciklama: null
        };
    }

    private createEmptySatisNoktasi(): KantinSatisNoktasiModel {
        return {
            kantinId: this.selectedKantin?.id ?? 0,
            kod: '',
            ad: '',
            varsayilanNakitKasaId: null,
            varsayilanPosHesapId: null,
            varsayilanMi: false,
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
