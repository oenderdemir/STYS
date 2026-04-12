import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    GarsonMasaModel,
    GarsonMenuKategoriModel,
    GarsonMenuModel,
    GarsonMenuUrunModel,
    getKalemDurumSeverity,
    getGarsonMasaDurumSeverity,
    getMasaOturumuDurumSeverity,
    MASA_OTURUMU_KALEM_DURUMLARI,
    MasaOturumuKalemiModel,
    MasaOturumuModel
} from './garson-servis.dto';
import { GarsonServisService } from './garson-servis.service';
import { RestoranModel } from './restoran-yonetimi.dto';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

@Component({
    selector: 'app-garson-servis',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        DividerModule,
        InputTextModule,
        TextareaModule,
        ProgressSpinnerModule,
        SelectModule,
        TabsModule,
        TagModule,
        ToastModule
    ],
    templateUrl: './garson-servis.html',
    styleUrl: './garson-servis.scss',
    providers: [MessageService]
})
export class GarsonServisPage implements OnInit {
    private readonly restoranService = inject(RestoranYonetimiService);
    private readonly service = inject(GarsonServisService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    masalarLoading = false;
    oturumLoading = false;
    saving = false;

    restoranlar: RestoranModel[] = [];
    selectedRestoranId: number | null = null;
    masalar: GarsonMasaModel[] = [];
    selectedMasaId: number | null = null;
    oturum: MasaOturumuModel | null = null;
    menu: GarsonMenuModel | null = null;
    selectedKategoriId: number | null = null;
    urunArama = '';
    oturumNotu = '';

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranSiparisYonetimi.Manage');
    }

    get selectedKategori(): GarsonMenuKategoriModel | null {
        if (!this.menu || this.menu.kategoriler.length === 0) {
            return null;
        }

        if (this.selectedKategoriId) {
            const selected = this.menu.kategoriler.find((x) => x.id === this.selectedKategoriId);
            if (selected) {
                return selected;
            }
        }

        return this.menu.kategoriler[0];
    }

    get selectedKategoriUrunleri(): GarsonMenuUrunModel[] {
        const kategori = this.selectedKategori;
        if (!kategori) {
            return [];
        }

        return this.filterUrunler(kategori.urunler);
    }

    readonly kalemDurumlari = [...MASA_OTURUMU_KALEM_DURUMLARI];

    get toplamKalemSayisi(): number {
        if (!this.oturum) {
            return 0;
        }

        return this.oturum.kalemler.reduce((total, kalem) => total + Number(kalem.miktar), 0);
    }

    ngOnInit(): void {
        this.loadInitial();
    }

    onRestoranChange(): void {
        this.selectedMasaId = null;
        this.oturum = null;
        this.oturumNotu = '';
        this.loadMasalar(false);
        this.loadMenu();
    }

    refresh(): void {
        this.loadMasalar(true);
        this.loadMenu();
        if (this.selectedMasaId) {
            this.loadMasaOturumu(this.selectedMasaId, false);
        }
    }

    selectMasa(masa: GarsonMasaModel): void {
        if (masa.durum === 'Kapali' || this.oturumLoading) {
            return;
        }

        this.selectedMasaId = masa.masaId;
        this.loadMasaOturumu(masa.masaId, true);
    }

    urunEkle(urun: GarsonMenuUrunModel): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .addKalem(this.oturum.oturumId, { urunId: urun.id, miktar: 1, notlar: null })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    kalemMiktarDegistir(kalem: MasaOturumuKalemiModel, delta: number): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        const yeniMiktar = Math.max(0, Math.round((kalem.miktar + delta) * 100) / 100);
        this.saving = true;

        this.service
            .updateKalem(this.oturum.oturumId, kalem.id, { miktar: yeniMiktar, durum: kalem.durum, notlar: kalem.notlar ?? null })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    kalemNotunuKaydet(kalem: MasaOturumuKalemiModel): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .updateKalem(this.oturum.oturumId, kalem.id, { miktar: kalem.miktar, durum: kalem.durum, notlar: kalem.notlar ?? null })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated, false);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    kalemDurumGuncelle(kalem: MasaOturumuKalemiModel, durum: string): void {
        if (!this.oturum || !this.canManage || this.saving || kalem.durum === durum) {
            return;
        }

        this.saving = true;
        this.service
            .updateKalem(this.oturum.oturumId, kalem.id, { miktar: kalem.miktar, durum, notlar: kalem.notlar ?? null })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated, false);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    kalemSil(kalem: MasaOturumuKalemiModel): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .deleteKalem(this.oturum.oturumId, kalem.id)
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    oturumNotunuKaydet(): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .updateNot(this.oturum.oturumId, { notlar: this.oturumNotu?.trim() || null })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    this.applyOturumUpdate(updated, false);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Kaydedildi', detail: 'Oturum notu guncellendi.' });
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    oturumDurumGuncelle(durum: string): void {
        if (!this.oturum || !this.canManage || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .updateDurum(this.oturum.oturumId, { durum })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updated) => {
                    if (durum === 'Kapatildi' || durum === 'Iptal') {
                        this.oturum = null;
                        this.oturumNotu = '';
                        this.loadMasalar(true);
                    } else {
                        this.applyOturumUpdate(updated);
                    }
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    getMasaDurumSeverityValue(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        return getGarsonMasaDurumSeverity(durum);
    }

    getOturumDurumSeverityValue(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        return getMasaOturumuDurumSeverity(durum);
    }

    getKalemDurumSeverityValue(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        return getKalemDurumSeverity(durum);
    }

    private loadInitial(): void {
        this.loading = true;
        forkJoin({
            restoranlar: this.restoranService.getAll()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ restoranlar }) => {
                    this.restoranlar = restoranlar.filter((x) => x.aktifMi);
                    this.selectedRestoranId = this.restoranlar[0]?.id ?? null;
                    if (this.selectedRestoranId) {
                        this.loadMasalar(false);
                        this.loadMenu();
                    }
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    private loadMasalar(keepSelection: boolean): void {
        if (!this.selectedRestoranId) {
            this.masalar = [];
            return;
        }

        this.masalarLoading = true;
        this.service
            .getMasalar(this.selectedRestoranId)
            .pipe(
                finalize(() => {
                    this.masalarLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.masalar = items;
                    if (keepSelection && this.selectedMasaId) {
                        const exists = this.masalar.some((x) => x.masaId === this.selectedMasaId);
                        if (!exists) {
                            this.selectedMasaId = null;
                            this.oturum = null;
                        }
                    }
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    private loadMenu(): void {
        if (!this.selectedRestoranId) {
            this.menu = null;
            this.selectedKategoriId = null;
            return;
        }

        this.service.getMenu(this.selectedRestoranId).subscribe({
            next: (menu) => {
                this.menu = menu;
                if (!this.selectedKategoriId || !menu.kategoriler.some((x) => x.id === this.selectedKategoriId)) {
                    this.selectedKategoriId = menu.kategoriler[0]?.id ?? null;
                }
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadMasaOturumu(masaId: number, createIfMissing: boolean): void {
        this.oturumLoading = true;
        const request$ = createIfMissing
            ? this.service.startOrGetMasaOturumu(masaId, { paraBirimi: 'TRY' })
            : this.service.getMasaOturumuByMasa(masaId);

        request$
            .pipe(
                finalize(() => {
                    this.oturumLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (oturum) => {
                    this.oturum = oturum;
                    this.oturumNotu = oturum.notlar ?? '';
                    this.loadMasalar(true);
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    private applyOturumUpdate(updated: MasaOturumuModel, reloadMasalar = true): void {
        this.oturum = updated;
        this.oturumNotu = updated.notlar ?? '';
        if (reloadMasalar) {
            this.loadMasalar(true);
        }
    }

    private filterUrunler(urunler: GarsonMenuUrunModel[]): GarsonMenuUrunModel[] {
        const search = this.urunArama.trim().toLocaleLowerCase('tr-TR');
        if (!search) {
            return urunler;
        }

        return urunler.filter((urun) => {
            const ad = urun.ad.toLocaleLowerCase('tr-TR');
            const aciklama = (urun.aciklama ?? '').toLocaleLowerCase('tr-TR');
            return ad.includes(search) || aciklama.includes(search);
        });
    }

    private showError(error: unknown): void {
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
