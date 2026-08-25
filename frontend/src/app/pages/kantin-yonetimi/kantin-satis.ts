import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextBarComponent } from '../muhasebe/components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { StokHareketleriService } from '../muhasebe/stok-hareketleri/stok-hareketleri.service';
import { StokLotBakiyeModel, StokSeriBakiyeModel } from '../muhasebe/stok-hareketleri/stok-hareketleri.dto';
import { KantinlerService } from './kantinler.service';
import { KantinModel, KantinOdemeHesapOption, KantinSatisNoktasiModel, KantinUrunModel } from './kantinler.dto';
import { AddKantinSatisOdemeRequest, AddKantinSatisSatirRequest, KANTIN_ODEME_YONTEMLERI, KantinSatisBarkodUrunModel, KantinSatisIadeOzetModel, KantinSatisModel, KantinSatisOdemeModel, KantinSatisSatirModel } from './kantin-satis.dto';
import { KantinSatisService } from './kantin-satis.service';

@Component({
    selector: 'app-kantin-satis-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        DialogModule,
        DividerModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        TextareaModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './kantin-satis.html',
    styles: [`
        :host {
            display: block;
        }

        .satis-shell {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .sales-hero {
            border-radius: 1.5rem;
            padding: 1.5rem;
            background:
                radial-gradient(circle at top right, rgba(59, 130, 246, 0.14), transparent 24%),
                radial-gradient(circle at left bottom, rgba(16, 185, 129, 0.18), transparent 26%),
                linear-gradient(135deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.96));
            border: 1px solid rgba(148, 163, 184, 0.18);
            box-shadow: 0 18px 45px rgba(15, 23, 42, 0.08);
        }

        .sales-hero-grid {
            display: grid;
            grid-template-columns: minmax(0, 1.6fr) minmax(20rem, 0.9fr);
            gap: 1.5rem;
            align-items: end;
        }

        .hero-copy-block {
            display: flex;
            flex-direction: column;
            justify-content: center;
        }

        .hero-control-block {
            align-self: end;
            max-width: 26rem;
            justify-self: end;
            width: 100%;
        }

        .hero-badge {
            display: inline-flex;
            align-items: center;
            gap: 0.5rem;
            padding: 0.45rem 0.8rem;
            border-radius: 999px;
            background: rgba(15, 23, 42, 0.06);
            color: #334155;
            font-size: 0.85rem;
            font-weight: 600;
        }

        .hero-heading {
            margin: 0;
            color: #0f172a;
            font-size: 2rem;
            line-height: 1.1;
        }

        .hero-text {
            margin: 0.75rem 0 0;
            max-width: 52rem;
            color: #475569;
            line-height: 1.6;
        }

        .stat-strip {
            display: grid;
            grid-template-columns: repeat(4, minmax(0, 1fr));
            gap: 1rem;
        }

        .stat-panel {
            border-radius: 1.2rem;
            padding: 1rem 1.1rem;
            background: rgba(255, 255, 255, 0.88);
            border: 1px solid rgba(148, 163, 184, 0.16);
        }

        .stat-panel-label {
            color: #64748b;
            font-size: 0.8rem;
            text-transform: uppercase;
            letter-spacing: 0.06em;
            font-weight: 700;
        }

        .stat-panel-value {
            margin-top: 0.65rem;
            color: #0f172a;
            font-size: 1.6rem;
            font-weight: 700;
        }

        .sales-main-grid {
            display: grid;
            grid-template-columns: minmax(18rem, 0.9fr) minmax(0, 2.2fr);
            gap: 1.25rem;
            align-items: start;
        }

        .sales-side-stack,
        .sales-content-stack {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
            min-width: 0;
        }

        .sales-bottom-grid {
            display: grid;
            grid-template-columns: minmax(0, 1.15fr) minmax(0, 1fr);
            gap: 1.25rem;
            align-items: start;
        }

        .surface-card {
            height: 100%;
            border-radius: 1.4rem;
            background: rgba(255, 255, 255, 0.95);
            border: 1px solid rgba(148, 163, 184, 0.16);
            box-shadow: 0 16px 35px rgba(15, 23, 42, 0.06);
            overflow: hidden;
        }

        .surface-header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 1rem;
            padding: 1.2rem 1.35rem 0;
        }

        .surface-title {
            margin: 0;
            color: #0f172a;
            font-size: 1.3rem;
            font-weight: 700;
        }

        .surface-subtitle {
            margin: 0.35rem 0 0;
            color: #64748b;
            font-size: 0.95rem;
        }

        .surface-body {
            padding: 1.2rem 1.35rem 1.35rem;
        }

        .finder-stack,
        .payment-stack,
        .detail-stack {
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }

        .quick-toolbar {
            display: flex;
            gap: 0.75rem;
            align-items: end;
            flex-wrap: wrap;
        }

        .product-list {
            display: flex;
            flex-direction: column;
            gap: 0.75rem;
            max-height: 24rem;
            overflow: auto;
            padding-right: 0.25rem;
        }

        .product-item {
            display: flex;
            flex-direction: column;
            align-items: flex-start;
            gap: 0.3rem;
            width: 100%;
            text-align: left;
        }

        .product-meta {
            color: #64748b;
            font-size: 0.86rem;
        }

        .summary-grid {
            display: grid;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            gap: 0.75rem;
        }

        .summary-box {
            border-radius: 1rem;
            padding: 0.95rem 1rem;
            background: linear-gradient(180deg, rgba(248, 250, 252, 0.94), rgba(255, 255, 255, 0.98));
            border: 1px solid rgba(226, 232, 240, 0.9);
        }

        .summary-box-label {
            color: #64748b;
            font-size: 0.8rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.06em;
        }

        .summary-box-value {
            margin-top: 0.55rem;
            color: #0f172a;
            font-size: 1.5rem;
            font-weight: 700;
        }

        .payment-form-grid {
            display: grid;
            grid-template-columns: 1.1fr 1.25fr 1fr auto;
            gap: 0.85rem;
            align-items: end;
        }

        .field-stack {
            display: flex;
            flex-direction: column;
            gap: 0.45rem;
        }

        .field-label {
            color: #334155;
            font-size: 0.92rem;
            font-weight: 600;
        }

        .empty-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 0.85rem;
            min-height: 14rem;
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
            font-size: 1.05rem;
            font-weight: 700;
        }

        .history-card {
            min-height: 0;
        }

        .compact-empty {
            min-height: 9rem;
            padding: 1.5rem;
        }

        @media (max-width: 1200px) {
            .sales-hero-grid,
            .sales-main-grid,
            .sales-bottom-grid,
            .payment-form-grid,
            .stat-strip,
            .summary-grid {
                grid-template-columns: 1fr;
            }

            .hero-control-block {
                max-width: none;
                justify-self: stretch;
            }
        }
    `],
    providers: [MessageService]
})
export class KantinSatisPage implements OnInit {
    private readonly satisService = inject(KantinSatisService);
    private readonly kantinlerService = inject(KantinlerService);
    private readonly stokHareketleriService = inject(StokHareketleriService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    barkod = '';
    urunArama = '';

    kantinler: KantinModel[] = [];
    selectedKantinId: number | null = null;
    selectedKantin: KantinModel | null = null;
    satisNoktalari: KantinSatisNoktasiModel[] = [];
    selectedSatisNoktasiId: number | null = null;
    selectedSatisNoktasi: KantinSatisNoktasiModel | null = null;
    urunler: KantinUrunModel[] = [];
    satislar: KantinSatisModel[] = [];
    currentDraft: KantinSatisModel | null = null;
    selectedHistory: KantinSatisModel | null = null;
    showIptalDialog = false;
    iptalSatis: KantinSatisModel | null = null;
    iptalAciklama = '';
    showIadeDialog = false;
    iadeLoading = false;
    iadeAciklama = '';
    iadeOzeti: KantinSatisIadeOzetModel[] = [];
    iadeMiktarlari: Record<number, number | null> = {};
    odemeHesaplari: Record<string, KantinOdemeHesapOption[]> = {};
    yeniOdeme: AddKantinSatisOdemeRequest = { odemeYontemi: KANTIN_ODEME_YONTEMLERI.Nakit, tutar: 0, kasaBankaHesapId: null };
    lotOptions: Record<number, StokLotBakiyeModel[]> = {};
    seriOptions: Record<number, StokSeriBakiyeModel[]> = {};

    readonly odemeYontemiSecenekleri = [
        { label: 'Nakit', value: KANTIN_ODEME_YONTEMLERI.Nakit },
        { label: 'Kredi Kartı', value: KANTIN_ODEME_YONTEMLERI.KrediKarti }
    ];

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.resetState();
        this.loadKantinler();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadKantinler();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    get filteredUrunler(): KantinUrunModel[] {
        const query = this.urunArama.trim().toLocaleLowerCase('tr-TR');
        if (!query) {
            return this.urunler.filter((x) => x.aktifMi);
        }

        return this.urunler
            .filter((x) => x.aktifMi)
            .filter((x) => `${x.stokKodu ?? ''} ${x.urunAdi ?? ''} ${x.barkod ?? ''}`.toLocaleLowerCase('tr-TR').includes(query));
    }

    get genelToplam(): number {
        return this.currentDraft?.toplamTutar ?? 0;
    }

    get araToplam(): number {
        return this.currentDraft?.matrahToplami ?? 0;
    }

    get kdvToplami(): number {
        return this.currentDraft?.kdvToplami ?? 0;
    }

    get canKesinlestir(): boolean {
        return !!this.currentDraft?.id && this.currentDraft.satirlar.length > 0 && this.currentDraft.odemeler.length > 0 && !this.saving;
    }

    get odenenToplam(): number {
        return this.currentDraft?.odemeler.reduce((sum, item) => sum + item.tutar, 0) ?? 0;
    }

    get kalanTutar(): number {
        return Math.max(this.genelToplam - this.odenenToplam, 0);
    }

    get sepetSatirSayisi(): number {
        return this.currentDraft?.satirlar.length ?? 0;
    }

    get aktifUrunSayisi(): number {
        return this.filteredUrunler.length;
    }

    get selectedKantinLabel(): string {
        if (!this.selectedKantin) {
            return 'Kantin seçilmedi';
        }

        return `${this.selectedKantin.kod} - ${this.selectedKantin.ad}`;
    }

    canMuhasebelestir(satis: KantinSatisModel | null | undefined): boolean {
        return !!satis?.id
            && satis.durum === 'Kesinlesti'
            && !satis.muhasebeFisId
            && !this.saving;
    }

    canIptalEt(satis: KantinSatisModel | null | undefined): boolean {
        return !!satis?.id && satis.durum === 'Kesinlesti' && !this.saving;
    }

    canIadeOlustur(satis: KantinSatisModel | null | undefined): boolean {
        return !!satis?.id && satis.durum === 'Kesinlesti' && !this.saving;
    }

    iadeOzetiBySatir(satirId?: number): KantinSatisIadeOzetModel {
        return this.iadeOzeti.find((x) => x.kantinSatisSatirId === satirId)
            ?? { kantinSatisSatirId: satirId ?? 0, satilanMiktar: 0, oncekiIadeMiktari: 0, kalanMiktar: 0 };
    }

    loadKantinler(): void {
        const tesisId = this.currentTesisId;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.kantinlerService.getAll(tesisId)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.kantinler = items.filter((x) => x.aktifMi);
                    if (this.kantinler.length > 0) {
                        this.onKantinChange(this.kantinler[0].id ?? null);
                    }
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    onKantinChange(kantinId: number | null): void {
        this.selectedKantinId = kantinId;
        this.selectedKantin = this.kantinler.find((x) => x.id === kantinId) ?? null;
        this.currentDraft = null;
        this.selectedHistory = null;
        this.urunler = [];
        this.satislar = [];
        this.lotOptions = {};
        this.seriOptions = {};
        this.satisNoktalari = [];
        this.selectedSatisNoktasiId = null;
        this.selectedSatisNoktasi = null;
        this.resetYeniOdeme();

        if (!this.selectedKantin?.id) {
            return;
        }

        this.kantinlerService.getUrunler(this.selectedKantin.id).subscribe({
            next: (items) => {
                this.urunler = items;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });

        this.kantinlerService.getSatisNoktalari(this.selectedKantin.id).subscribe({
            next: (items) => {
                this.satisNoktalari = items.filter((x) => x.aktifMi);
                this.autoSelectSatisNoktasi();
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });

        this.loadSatisHistory();
        this.loadOdemeHesaplari(KANTIN_ODEME_YONTEMLERI.Nakit);
        this.loadOdemeHesaplari(KANTIN_ODEME_YONTEMLERI.KrediKarti);
    }

    onSatisNoktasiChange(noktaId: number | null): void {
        this.selectedSatisNoktasiId = noktaId;
        this.selectedSatisNoktasi = this.satisNoktalari.find((x) => x.id === noktaId) ?? null;
        this.currentDraft = null;
        this.selectedHistory = null;
        this.lotOptions = {};
        this.seriOptions = {};
        this.resetYeniOdeme();
    }

    private autoSelectSatisNoktasi(): void {
        const aktifler = this.satisNoktalari.filter((x) => x.aktifMi);
        if (aktifler.length === 1) {
            this.onSatisNoktasiChange(aktifler[0].id ?? null);
            return;
        }

        const varsayilan = aktifler.find((x) => x.varsayilanMi);
        if (varsayilan) {
            this.onSatisNoktasiChange(varsayilan.id ?? null);
            return;
        }

        this.onSatisNoktasiChange(null);
    }

    yeniSatis(): void {
        this.currentDraft = null;
        this.selectedHistory = null;
        this.barkod = '';
        this.urunArama = '';
        this.lotOptions = {};
        this.seriOptions = {};
    }

    barkodAra(): void {
        if (!this.selectedKantin?.id || !this.barkod.trim()) {
            return;
        }

        this.satisService.getByBarkod(this.selectedKantin.id, this.barkod.trim()).subscribe({
            next: (urun) => {
                this.handleScannedProduct(urun);
                this.barkod = '';
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    urunEkle(urun: KantinUrunModel): void {
        if (!urun.id) {
            return;
        }

        const barkodUrun: KantinSatisBarkodUrunModel = {
            kantinUrunId: urun.id,
            tasinirKartId: urun.tasinirKartId,
            stokKodu: urun.stokKodu ?? '',
            urunAdi: urun.urunAdi ?? '',
            birim: urun.birim ?? '',
            barkod: urun.barkod ?? null,
            satisFiyati: urun.satisFiyati,
            kdvOrani: urun.kdvOrani,
            mevcutStok: urun.mevcutStok,
            takipTipi: urun.takipTipi ?? 'Yok'
        };

        this.handleScannedProduct(barkodUrun);
    }

    satirMiktarDegisti(satir: KantinSatisSatirModel): void {
        if (!this.currentDraft?.id || !satir.id) {
            return;
        }

        const request: AddKantinSatisSatirRequest = {
            kantinUrunId: satir.kantinUrunId,
            miktar: satir.miktar,
            stokLotId: satir.stokLotId ?? null,
            stokSeriId: satir.stokSeriId ?? null
        };

        this.satisService.updateSatir(this.currentDraft.id, satir.id, request).subscribe({
            next: (draft) => this.applyDraft(draft),
            error: (error: unknown) => this.showError(error)
        });
    }

    satirLotDegisti(satir: KantinSatisSatirModel): void {
        this.satirMiktarDegisti(satir);
    }

    satirSeriDegisti(satir: KantinSatisSatirModel): void {
        this.satirMiktarDegisti(satir);
    }

    satirSil(satir: KantinSatisSatirModel): void {
        if (!this.currentDraft?.id || !satir.id) {
            return;
        }

        this.satisService.deleteSatir(this.currentDraft.id, satir.id).subscribe({
            next: () => this.reloadCurrentDraft(),
            error: (error: unknown) => this.showError(error)
        });
    }

    odemeEkle(): void {
        if (!this.currentDraft?.id) {
            this.showError(new Error('Önce satış satırı ekleyiniz.'));
            return;
        }

        this.satisService.addOdeme(this.currentDraft.id, this.yeniOdeme).subscribe({
            next: (draft) => {
                this.applyDraft(draft);
                this.resetYeniOdeme();
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    odemeDegisti(odeme: KantinSatisOdemeModel): void {
        if (!this.currentDraft?.id || !odeme.id) {
            return;
        }

        this.satisService.updateOdeme(this.currentDraft.id, odeme.id, {
            odemeYontemi: odeme.odemeYontemi,
            kasaBankaHesapId: odeme.kasaBankaHesapId ?? null,
            tutar: odeme.tutar
        }).subscribe({
            next: (draft) => this.applyDraft(draft),
            error: (error: unknown) => this.showError(error)
        });
    }

    odemeSil(odeme: KantinSatisOdemeModel): void {
        if (!this.currentDraft?.id || !odeme.id) {
            return;
        }

        this.satisService.deleteOdeme(this.currentDraft.id, odeme.id).subscribe({
            next: () => this.reloadCurrentDraft(),
            error: (error: unknown) => this.showError(error)
        });
    }

    kesinlestir(): void {
        if (!this.currentDraft?.id) {
            return;
        }

        this.saving = true;
        this.satisService.kesinlestir(this.currentDraft.id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    const satisId = result.id;
                    this.showSuccess(`Satış kesinleşti. Satış no: ${satisId}`);
                    this.currentDraft = null;
                    this.selectedHistory = result;
                    this.loadSatisHistory();
                    if (this.selectedKantin?.id) {
                        this.kantinlerService.getUrunler(this.selectedKantin.id).subscribe({
                            next: (items) => {
                                this.urunler = items;
                                this.cdr.detectChanges();
                            }
                        });
                    }
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    historyDetay(satis: KantinSatisModel): void {
        if (!satis.id) {
            return;
        }

        this.satisService.getById(satis.id).subscribe({
            next: (item) => {
                this.selectedHistory = item;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    muhasebelestir(satis: KantinSatisModel | null | undefined): void {
        if (!satis?.id || !this.canMuhasebelestir(satis)) {
            return;
        }

        this.saving = true;
        this.satisService.muhasebeFisiOlustur(satis.id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    this.selectedHistory = result;
                    this.showSuccess(`Muhasebe fişi oluşturuldu: ${result.muhasebeFisNo ?? '#' + result.muhasebeFisId}`);
                    this.loadSatisHistory();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    openIptalDialog(satis: KantinSatisModel | null | undefined): void {
        if (!satis?.id || !this.canIptalEt(satis)) {
            return;
        }

        this.iptalSatis = satis;
        this.iptalAciklama = '';
        this.showIptalDialog = true;
    }

    confirmIptal(): void {
        const satis = this.iptalSatis;
        if (!satis?.id || !this.iptalAciklama.trim()) {
            return;
        }

        this.saving = true;
        this.satisService.iptal(satis.id, { aciklama: this.iptalAciklama.trim() })
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    this.showIptalDialog = false;
                    this.iptalSatis = null;
                    this.iptalAciklama = '';
                    this.selectedHistory = result;
                    this.loadSatisHistory();
                    this.showSuccess(`Satış iptal edildi. Satış no: ${result.id}`);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    openIadeDialog(satis: KantinSatisModel | null | undefined): void {
        if (!satis?.id || !this.canIadeOlustur(satis)) {
            return;
        }

        this.iadeAciklama = '';
        this.iadeMiktarlari = {};
        this.iadeOzeti = [];
        this.showIadeDialog = true;

        this.iadeLoading = true;
        this.satisService.getIadeOzeti(satis.id)
            .pipe(finalize(() => {
                this.iadeLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.iadeOzeti = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    confirmIade(): void {
        const satis = this.selectedHistory;
        if (!satis?.id) {
            return;
        }

        const satirlar = Object.entries(this.iadeMiktarlari)
            .map(([satirId, miktar]) => ({ kantinSatisSatirId: Number(satirId), miktar: miktar ?? 0 }))
            .filter((x) => x.miktar > 0);

        if (satirlar.length === 0) {
            this.showError(new Error('En az bir satır için iade miktarı giriniz.'));
            return;
        }

        this.saving = true;
        this.satisService.createIade({
            kantinSatisId: satis.id,
            aciklama: this.iadeAciklama.trim() || null,
            satirlar
        }).subscribe({
            next: (iade) => {
                if (!iade.id) {
                    this.saving = false;
                    this.showError(new Error('İade oluşturulamadı.'));
                    return;
                }

                this.satisService.finalizeIade(iade.id)
                    .pipe(finalize(() => {
                        this.saving = false;
                        this.cdr.detectChanges();
                    }))
                    .subscribe({
                        next: () => {
                            this.showIadeDialog = false;
                            this.iadeMiktarlari = {};
                            this.iadeOzeti = [];
                            this.loadSatisHistory();
                            this.showSuccess('İade oluşturuldu ve kesinleştirildi.');
                        },
                        error: (error: unknown) => this.showError(error)
                    });
            },
            error: (error: unknown) => {
                this.saving = false;
                this.showError(error);
            }
        });
    }

    onYeniOdemeYontemiChange(): void {
        this.yeniOdeme.kasaBankaHesapId = this.resolveDefaultOdemeHesapId(this.yeniOdeme.odemeYontemi);
    }

    onOdemeYontemiDegisti(odeme: KantinSatisOdemeModel): void {
        odeme.kasaBankaHesapId = this.resolveDefaultOdemeHesapId(odeme.odemeYontemi);
        this.odemeDegisti(odeme);
    }

    private resolveDefaultOdemeHesapId(odemeYontemi: string): number | null {
        if (odemeYontemi === KANTIN_ODEME_YONTEMLERI.Nakit) {
            return this.selectedSatisNoktasi?.varsayilanNakitKasaId ?? null;
        }

        if (odemeYontemi === KANTIN_ODEME_YONTEMLERI.KrediKarti) {
            return this.selectedSatisNoktasi?.varsayilanPosHesapId ?? null;
        }

        return null;
    }

    private resetYeniOdeme(): void {
        this.yeniOdeme = {
            odemeYontemi: KANTIN_ODEME_YONTEMLERI.Nakit,
            tutar: 0,
            kasaBankaHesapId: this.selectedSatisNoktasi?.varsayilanNakitKasaId ?? null
        };
    }

    getOdemeHesapSecenekleri(odemeYontemi: string): Array<{ label: string; value: number }> {
        return (this.odemeHesaplari[odemeYontemi] ?? []).map((x) => ({
            label: `${x.kod} - ${x.ad}`,
            value: x.id
        }));
    }

    isLotTracked(satir: KantinSatisSatirModel): boolean {
        return satir.takipTipi === 'Lot';
    }

    isSeriTracked(satir: KantinSatisSatirModel): boolean {
        return satir.takipTipi === 'Seri';
    }

    private handleScannedProduct(urun: KantinSatisBarkodUrunModel): void {
        this.ensureDraft((draft) => {
            const existing = draft.satirlar.find((x) =>
                x.kantinUrunId === urun.kantinUrunId &&
                (urun.takipTipi === 'Yok' || !urun.takipTipi));

            if (existing?.id && draft.id) {
                this.satisService.updateSatir(draft.id, existing.id, {
                    kantinUrunId: existing.kantinUrunId,
                    miktar: existing.miktar + 1,
                    stokLotId: existing.stokLotId ?? null,
                    stokSeriId: existing.stokSeriId ?? null
                }).subscribe({
                    next: (updated) => this.applyDraft(updated),
                    error: (error: unknown) => this.showError(error)
                });
                return;
            }

            this.satisService.addSatir(draft.id!, {
                kantinUrunId: urun.kantinUrunId,
                miktar: urun.takipTipi === 'Seri' ? 1 : 1
            }).subscribe({
                next: (updated) => this.applyDraft(updated),
                error: (error: unknown) => this.showError(error)
            });
        });
    }

    private ensureDraft(next: (draft: KantinSatisModel) => void): void {
        if (this.currentDraft?.id) {
            next(this.currentDraft);
            return;
        }

        if (!this.selectedKantin?.id || !this.selectedSatisNoktasiId) {
            this.showError(new Error('Satış yapmak için bir satış noktası seçiniz.'));
            return;
        }

        this.satisService.create({
            kantinId: this.selectedKantin.id,
            satisNoktasiId: this.selectedSatisNoktasiId,
            satisTarihi: new Date().toISOString(),
            aciklama: null
        }).subscribe({
            next: (draft) => {
                this.currentDraft = draft;
                next(draft);
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private applyDraft(draft: KantinSatisModel): void {
        this.currentDraft = draft;
        for (const satir of draft.satirlar) {
            this.loadTrackingOptionsForSatir(satir);
        }
        this.cdr.detectChanges();
    }

    private reloadCurrentDraft(): void {
        if (!this.currentDraft?.id) {
            return;
        }

        this.satisService.getById(this.currentDraft.id).subscribe({
            next: (draft) => this.applyDraft(draft),
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadSatisHistory(): void {
        this.satisService.getAll(this.currentTesisId, this.selectedKantinId).subscribe({
            next: (items) => {
                this.satislar = items.filter((x) => x.durum === 'Kesinlesti');
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadOdemeHesaplari(odemeYontemi: string): void {
        if (!this.currentTesisId) {
            return;
        }

        this.kantinlerService.getOdemeHesaplari(this.currentTesisId, odemeYontemi).subscribe({
            next: (items) => {
                this.odemeHesaplari[odemeYontemi] = items;
                this.cdr.detectChanges();
            }
        });
    }

    private loadTrackingOptionsForSatir(satir: KantinSatisSatirModel): void {
        if (!this.selectedKantin?.depoId) {
            return;
        }

        if (this.isLotTracked(satir) && !this.lotOptions[satir.id ?? 0]) {
            this.stokHareketleriService.getLotBakiyeleri(this.selectedKantin.depoId, satir.tasinirKartId).subscribe({
                next: (items) => {
                    if (satir.id) {
                        this.lotOptions[satir.id] = items.filter((x) => x.bakiyeMiktari > 0 || x.stokLotId === satir.stokLotId);
                    }
                    this.cdr.detectChanges();
                }
            });
        }

        if (this.isSeriTracked(satir) && !this.seriOptions[satir.id ?? 0]) {
            this.stokHareketleriService.getSeriBakiyeleri(this.selectedKantin.depoId, satir.tasinirKartId).subscribe({
                next: (items) => {
                    if (satir.id) {
                        this.seriOptions[satir.id] = items;
                    }
                    this.cdr.detectChanges();
                }
            });
        }
    }

    private resetState(): void {
        this.kantinler = [];
        this.selectedKantinId = null;
        this.selectedKantin = null;
        this.satisNoktalari = [];
        this.selectedSatisNoktasiId = null;
        this.selectedSatisNoktasi = null;
        this.urunler = [];
        this.satislar = [];
        this.currentDraft = null;
        this.selectedHistory = null;
        this.odemeHesaplari = {};
        this.lotOptions = {};
        this.seriOptions = {};
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
