import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextBarComponent } from '../muhasebe/components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { StokHareketleriService } from '../muhasebe/stok-hareketleri/stok-hareketleri.service';
import { StokLotBakiyeModel, StokSeriBakiyeModel } from '../muhasebe/stok-hareketleri/stok-hareketleri.dto';
import { KantinlerService } from './kantinler.service';
import { KantinModel, KantinOdemeHesapOption, KantinUrunModel } from './kantinler.dto';
import { AddKantinSatisOdemeRequest, AddKantinSatisSatirRequest, KANTIN_ODEME_YONTEMLERI, KantinSatisBarkodUrunModel, KantinSatisModel, KantinSatisOdemeModel, KantinSatisSatirModel } from './kantin-satis.dto';
import { KantinSatisService } from './kantin-satis.service';

@Component({
    selector: 'app-kantin-satis-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        DividerModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './kantin-satis.html',
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
    urunler: KantinUrunModel[] = [];
    satislar: KantinSatisModel[] = [];
    currentDraft: KantinSatisModel | null = null;
    selectedHistory: KantinSatisModel | null = null;
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

    canMuhasebelestir(satis: KantinSatisModel | null | undefined): boolean {
        return !!satis?.id
            && satis.durum === 'Kesinlesti'
            && !satis.muhasebeFisId
            && !this.saving;
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
        this.yeniOdeme = { odemeYontemi: KANTIN_ODEME_YONTEMLERI.Nakit, tutar: 0, kasaBankaHesapId: null };

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

        this.loadSatisHistory();
        this.loadOdemeHesaplari(KANTIN_ODEME_YONTEMLERI.Nakit);
        this.loadOdemeHesaplari(KANTIN_ODEME_YONTEMLERI.KrediKarti);
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
                this.yeniOdeme = { odemeYontemi: KANTIN_ODEME_YONTEMLERI.Nakit, tutar: 0, kasaBankaHesapId: null };
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

    onYeniOdemeYontemiChange(): void {
        if (this.yeniOdeme.odemeYontemi === KANTIN_ODEME_YONTEMLERI.Nakit) {
            this.yeniOdeme.kasaBankaHesapId = null;
        }
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

        if (!this.selectedKantin?.id) {
            return;
        }

        this.satisService.create({
            kantinId: this.selectedKantin.id,
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
