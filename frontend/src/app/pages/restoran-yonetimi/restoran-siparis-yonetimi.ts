import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    AktifRezervasyonAramaModel,
    CreateKrediKartiOdemeRequest,
    CreateNakitOdemeRequest,
    CreateOdayaEkleOdemeRequest,
    CreateRestoranSiparisKalemiRequest,
    CreateRestoranSiparisRequest,
    getOdemeDurumSeverity,
    getSiparisDurumSeverity,
    PARA_BIRIMI_SECENEKLERI,
    RESTORAN_ODEME_TIPLERI,
    RESTORAN_SIPARIS_DURUMLARI,
    RestoranMasaModel,
    RestoranMenuUrunModel,
    RestoranModel,
    RestoranSiparisModel,
    RestoranSiparisOdemeOzetiModel,
    UpdateRestoranSiparisDurumRequest
} from './restoran-yonetimi.dto';
import { RestoranMasaYonetimiService } from './restoran-masa-yonetimi.service';
import { RestoranMenuYonetimiService } from './restoran-menu-yonetimi.service';
import { RestoranSiparisYonetimiService } from './restoran-siparis-yonetimi.service';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

interface SiparisKalemFormModel {
    restoranMenuUrunId: number | null;
    miktar: number;
    notlar: string | null;
}

@Component({
    selector: 'app-restoran-siparis-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        ToastModule,
        ToolbarModule
    ],
    templateUrl: './restoran-siparis-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RestoranSiparisYonetimi implements OnInit {
    private readonly restoranService = inject(RestoranYonetimiService);
    private readonly masaService = inject(RestoranMasaYonetimiService);
    private readonly menuService = inject(RestoranMenuYonetimiService);
    private readonly service = inject(RestoranSiparisYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    paymentSaving = false;

    restoranlar: RestoranModel[] = [];
    masalar: RestoranMasaModel[] = [];
    urunler: RestoranMenuUrunModel[] = [];
    siparisler: RestoranSiparisModel[] = [];

    selectedRestoranId: number | null = null;
    selectedMasaId: number | null = null;

    createDialogVisible = false;
    createModel: CreateRestoranSiparisRequest = this.createEmptySiparisModel();
    createKalemler: SiparisKalemFormModel[] = [this.createEmptyKalem()];

    detailDialogVisible = false;
    selectedSiparis: RestoranSiparisModel | null = null;
    odemeOzeti: RestoranSiparisOdemeOzetiModel | null = null;

    selectedDurum: string | null = null;

    odemeTipi = 'Nakit';
    odemeTutari = 0;
    odemeAciklama: string | null = null;

    rezervasyonDialogVisible = false;
    rezervasyonSearchTerm = '';
    rezervasyonlar: AktifRezervasyonAramaModel[] = [];
    selectedRezervasyonId: number | null = null;

    readonly paraBirimiSecenekleri = [...PARA_BIRIMI_SECENEKLERI];
    readonly odemeTipleri = [...RESTORAN_ODEME_TIPLERI];
    readonly durumSecenekleri = [...RESTORAN_SIPARIS_DURUMLARI];

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranSiparisYonetimi.Manage');
    }

    get canManageOdeme(): boolean {
        return this.authService.hasPermission('RestoranOdemeYonetimi.Manage');
    }

    get availableMasalar(): RestoranMasaModel[] {
        return this.masalar.filter((x) => x.aktifMi);
    }

    get createToplamTutar(): number {
        return this.createKalemler.reduce((total, kalem) => {
            const urun = this.urunler.find((x) => x.id === kalem.restoranMenuUrunId);
            if (!urun || kalem.miktar <= 0) {
                return total;
            }

            return total + urun.fiyat * kalem.miktar;
        }, 0);
    }

    get canTakePayment(): boolean {
        if (!this.selectedSiparis) {
            return false;
        }

        if (!this.canManageOdeme) {
            return false;
        }

        if (this.selectedSiparis.siparisDurumu === 'Tamamlandi' || this.selectedSiparis.siparisDurumu === 'Iptal') {
            return false;
        }

        return (this.odemeOzeti?.kalanTutar ?? 0) > 0;
    }

    ngOnInit(): void {
        this.loadInitial();
    }

    onRestoranChange(): void {
        this.selectedMasaId = null;
        this.loadMasalar();
        this.loadMenuUrunleri();
        this.loadSiparisler();
    }

    onMasaFilterChange(): void {
        this.loadSiparisler();
    }

    refresh(): void {
        this.loadSiparisler();
    }

    openCreateDialog(): void {
        if (!this.canManage || !this.selectedRestoranId) {
            return;
        }

        this.createModel = this.createEmptySiparisModel();
        this.createModel.restoranId = this.selectedRestoranId;
        this.createKalemler = [this.createEmptyKalem()];
        this.createDialogVisible = true;
    }

    addKalemLine(): void {
        this.createKalemler = [...this.createKalemler, this.createEmptyKalem()];
    }

    removeKalemLine(index: number): void {
        if (this.createKalemler.length <= 1) {
            return;
        }

        this.createKalemler = this.createKalemler.filter((_, i) => i !== index);
    }

    saveSiparis(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (!this.createModel.restoranId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Restoran secimi zorunludur.' });
            return;
        }

        const kalemler: CreateRestoranSiparisKalemiRequest[] = this.createKalemler
            .filter((x) => !!x.restoranMenuUrunId && x.miktar > 0)
            .map((x) => ({
                restoranMenuUrunId: x.restoranMenuUrunId!,
                miktar: x.miktar,
                notlar: x.notlar?.trim() || null
            }));

        if (kalemler.length === 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'En az bir siparis kalemi girilmelidir.' });
            return;
        }

        const payload: CreateRestoranSiparisRequest = {
            restoranId: this.createModel.restoranId,
            restoranMasaId: this.createModel.restoranMasaId || null,
            paraBirimi: this.createModel.paraBirimi,
            notlar: this.createModel.notlar?.trim() || null,
            kalemler
        };

        this.saving = true;
        this.service.create(payload)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.createDialogVisible = false;
                    this.loadSiparisler();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Siparis olusturuldu.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    openDetail(item: RestoranSiparisModel): void {
        this.selectedSiparis = item;
        this.selectedDurum = item.siparisDurumu;
        this.odemeTipi = 'Nakit';
        this.odemeTutari = Math.max(0, item.kalanTutar ?? 0);
        this.odemeAciklama = null;
        this.detailDialogVisible = true;
        this.loadOdemeOzeti(item.id!);
    }

    guncelleDurum(): void {
        if (!this.canManage || !this.selectedSiparis?.id || !this.selectedDurum) {
            return;
        }

        const payload: UpdateRestoranSiparisDurumRequest = { siparisDurumu: this.selectedDurum };
        this.paymentSaving = true;
        this.service.updateDurum(this.selectedSiparis.id, payload)
            .pipe(finalize(() => {
                this.paymentSaving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (updated) => {
                    this.selectedSiparis = updated;
                    this.loadSiparisler();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Siparis durumu guncellendi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    odemeAl(): void {
        if (!this.canManageOdeme || !this.canTakePayment || !this.selectedSiparis?.id || this.odemeTutari <= 0 || !this.odemeOzeti) {
            return;
        }

        if (this.odemeTutari > this.odemeOzeti.kalanTutar) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Tutar', detail: 'Odeme tutari kalan tutari asamaz.' });
            return;
        }

        this.paymentSaving = true;
        const completed = () => {
            this.loadOdemeOzeti(this.selectedSiparis!.id!);
            this.loadSiparisler();
            this.odemeTutari = 0;
            this.odemeAciklama = null;
            this.paymentSaving = false;
            this.cdr.detectChanges();
        };

        if (this.odemeTipi === 'KrediKarti') {
            const payload: CreateKrediKartiOdemeRequest = { tutar: this.odemeTutari, aciklama: this.odemeAciklama };
            this.service.krediKartiOdemeAl(this.selectedSiparis.id, payload).subscribe({
                next: () => {
                    completed();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kredi karti odemesi alindi.' });
                },
                error: (error: unknown) => {
                    this.paymentSaving = false;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
            return;
        }

        const payload: CreateNakitOdemeRequest = { tutar: this.odemeTutari, aciklama: this.odemeAciklama };
        this.service.nakitOdemeAl(this.selectedSiparis.id, payload).subscribe({
            next: () => {
                completed();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Nakit odeme alindi.' });
            },
            error: (error: unknown) => {
                this.paymentSaving = false;
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    openOdayaEkleDialog(): void {
        if (!this.canManageOdeme || !this.selectedSiparis || !this.selectedRestoranId) {
            return;
        }

        const restoran = this.restoranlar.find((x) => x.id === this.selectedRestoranId);
        if (!restoran?.tesisId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyari', detail: 'Secili restoranin tesis bilgisi bulunamadi.' });
            return;
        }

        this.selectedRezervasyonId = null;
        this.rezervasyonSearchTerm = '';
        this.rezervasyonlar = [];
        this.odemeTutari = Math.max(0, this.odemeOzeti?.kalanTutar ?? 0);
        this.rezervasyonDialogVisible = true;
        this.searchRezervasyonlar();
    }

    searchRezervasyonlar(): void {
        if (!this.selectedRestoranId) {
            return;
        }

        const restoran = this.restoranlar.find((x) => x.id === this.selectedRestoranId);
        if (!restoran?.tesisId) {
            return;
        }

        this.service.uygunRezervasyonAra(restoran.tesisId, this.rezervasyonSearchTerm).subscribe({
            next: (items) => {
                this.rezervasyonlar = items;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    confirmOdayaEkle(): void {
        if (!this.canManageOdeme || !this.selectedSiparis?.id || !this.selectedRezervasyonId || !this.odemeOzeti) {
            return;
        }

        if (this.odemeTutari <= 0 || this.odemeTutari > this.odemeOzeti.kalanTutar) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Tutar', detail: 'Odaya ekleme tutari kalan tutari asamaz.' });
            return;
        }

        this.paymentSaving = true;
        const payload: CreateOdayaEkleOdemeRequest = {
            rezervasyonId: this.selectedRezervasyonId,
            tutar: this.odemeTutari,
            aciklama: this.odemeAciklama
        };

        this.service.odayaEkle(this.selectedSiparis.id, payload)
            .pipe(finalize(() => {
                this.paymentSaving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.rezervasyonDialogVisible = false;
                    this.loadOdemeOzeti(this.selectedSiparis!.id!);
                    this.loadSiparisler();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Siparis borcu odaya eklendi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    getSiparisDurumSeverityValue(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        return getSiparisDurumSeverity(durum);
    }

    getOdemeDurumSeverityValue(durum: string): 'success' | 'warn' | 'secondary' {
        return getOdemeDurumSeverity(durum);
    }

    getMasaAdi(masaId?: number | null): string {
        if (!masaId) {
            return '-';
        }

        return this.masalar.find((x) => x.id === masaId)?.masaNo ?? '-';
    }

    getUrunFiyat(kalem: SiparisKalemFormModel): number {
        const urun = this.urunler.find((x) => x.id === kalem.restoranMenuUrunId);
        if (!urun) {
            return 0;
        }

        return urun.fiyat * (kalem.miktar || 0);
    }

    private loadInitial(): void {
        this.loading = true;
        forkJoin({
            restoranlar: this.restoranService.getAll()
        })
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ restoranlar }) => {
                    this.restoranlar = restoranlar;
                    if (this.restoranlar.length > 0) {
                        this.selectedRestoranId = this.restoranlar[0].id ?? null;
                        this.onRestoranChange();
                    }
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadMasalar(): void {
        if (!this.selectedRestoranId) {
            this.masalar = [];
            return;
        }

        this.masaService.getByRestoranId(this.selectedRestoranId).subscribe({
            next: (items) => {
                this.masalar = items;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    private loadMenuUrunleri(): void {
        if (!this.selectedRestoranId) {
            this.urunler = [];
            return;
        }

        this.menuService.getMenuByRestoranId(this.selectedRestoranId).subscribe({
            next: (result) => {
                const allProducts: RestoranMenuUrunModel[] = [];
                for (const kategoriId of Object.keys(result.urunMap)) {
                    allProducts.push(...(result.urunMap[Number(kategoriId)] ?? []));
                }

                this.urunler = allProducts.filter((x) => x.aktifMi);
                this.cdr.detectChanges();
            },
            error: () => {
                this.urunler = [];
            }
        });
    }

    private loadSiparisler(): void {
        if (!this.selectedRestoranId) {
            this.siparisler = [];
            return;
        }

        this.loading = true;

        const request$ = this.selectedMasaId
            ? this.service.getAcikByMasaId(this.selectedMasaId)
            : this.service.getByRestoranId(this.selectedRestoranId);

        request$
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.siparisler = this.selectedMasaId
                        ? items.filter((x) => x.restoranMasaId === this.selectedMasaId)
                        : items;
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadOdemeOzeti(siparisId: number): void {
        this.service.getOdemeOzeti(siparisId).subscribe({
            next: (result) => {
                this.odemeOzeti = result;
                this.odemeTutari = Math.max(0, result.kalanTutar);
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    private createEmptySiparisModel(): CreateRestoranSiparisRequest {
        return {
            restoranId: 0,
            restoranMasaId: null,
            paraBirimi: 'TRY',
            notlar: null,
            kalemler: []
        };
    }

    private createEmptyKalem(): SiparisKalemFormModel {
        return {
            restoranMenuUrunId: null,
            miktar: 1,
            notlar: null
        };
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}
