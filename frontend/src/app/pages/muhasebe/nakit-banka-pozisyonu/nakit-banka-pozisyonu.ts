import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    BankaHesapPozisyonuModel,
    BankaValorTakvimiModel,
    HESAP_TURU_SECENEKLERI,
    NakitBankaPozisyonuFilterModel,
    NakitBankaPozisyonuModel,
    PagedResultModel,
    VALOR_DURUMU_SECENEKLERI,
    ValorDetayModel,
    VERI_KALITESI_UYARI_LABELLARI
} from './nakit-banka-pozisyonu.dto';
import { NakitBankaPozisyonuService } from './nakit-banka-pozisyonu.service';

/** Europe/Istanbul saat dilimine gore "bugun" - tarayicinin KENDI yerel saat dilimine (farkli bir
 * ulkeden erisilebilir) BAGIMLI OLMADAN, backend'in varsayilan rapor tarihi davranisiyla AYNI
 * sonucu verir. */
export function bugunIstanbul(): Date {
    const parcalar = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' })
        .formatToParts(new Date())
        .reduce<Record<string, string>>((acc, part) => {
            if (part.type !== 'literal') {
                acc[part.type] = part.value;
            }
            return acc;
        }, {});

    return new Date(Number(parcalar['year']), Number(parcalar['month']) - 1, Number(parcalar['day']));
}

const GUN_DETAY_SAYFA_BOYUTU = 25;

interface GunDetayState {
    loading: boolean;
    sayfa: number;
    sonuc: PagedResultModel<ValorDetayModel> | null;
}

@Component({
    selector: 'app-nakit-banka-pozisyonu-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        DatePickerModule,
        DialogModule,
        ProgressSpinnerModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        TooltipModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './nakit-banka-pozisyonu.html',
    providers: [MessageService]
})
export class NakitBankaPozisyonuPage implements OnInit {
    private readonly service = inject(NakitBankaPozisyonuService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly router = inject(Router);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    errorMessage: string | null = null;

    sonuc: NakitBankaPozisyonuModel | null = null;

    readonly bugun = bugunIstanbul();
    raporTarihi: Date = this.bugun;
    hesapTuru: string | null = null;
    paraBirimi: string | null = null;
    valorDurumu: string | null = null;

    readonly hesapTuruSecenekleri = HESAP_TURU_SECENEKLERI;
    readonly valorDurumuSecenekleri = VALOR_DURUMU_SECENEKLERI;
    readonly uyariLabellari = VERI_KALITESI_UYARI_LABELLARI;

    takvimVisible = false;
    takvimLoading = false;
    takvimData: BankaValorTakvimiModel | null = null;
    takvimBankaId = 0;
    takvimBankaAdi = '';
    expandedGunler: Record<string, boolean> = {};
    gunDetaylari: Record<string, GunDetayState> = {};

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.load();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için nakit/banka pozisyonu yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    load(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        const filter = this.buildFilter(tesisId);

        this.loading = true;
        this.errorMessage = null;
        this.service.getPozisyon(filter)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (sonuc) => {
                    this.sonuc = sonuc;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.sonuc = null;
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
    }

    refresh(): void {
        this.load();
    }

    onFiltreDegisti(): void {
        this.load();
    }

    openValorTakvimi(hesap: BankaHesapPozisyonuModel): void {
        this.takvimBankaId = hesap.kasaBankaHesapId;
        this.takvimBankaAdi = `${hesap.bankaAdi} - ${hesap.hesapAdi}`;
        this.takvimVisible = true;
        this.takvimLoading = true;
        this.takvimData = null;
        this.expandedGunler = {};
        this.gunDetaylari = {};

        this.service.getValorTakvimi(hesap.kasaBankaHesapId, this.toIsoDate(this.raporTarihi))
            .pipe(finalize(() => {
                this.takvimLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (takvim) => {
                    this.takvimData = takvim;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    toggleGun(valorTarihi: string): void {
        const yeniDurum = !this.expandedGunler[valorTarihi];
        this.expandedGunler[valorTarihi] = yeniDurum;
        if (yeniDurum && !this.gunDetaylari[valorTarihi]) {
            this.loadGunDetaylari(valorTarihi, 1);
        }
    }

    isGunAcik(valorTarihi: string): boolean {
        return !!this.expandedGunler[valorTarihi];
    }

    gunDetayDurumu(valorTarihi: string): GunDetayState | null {
        return this.gunDetaylari[valorTarihi] ?? null;
    }

    gunSayfaDegistir(valorTarihi: string, sayfa: number): void {
        this.loadGunDetaylari(valorTarihi, sayfa);
    }

    private loadGunDetaylari(valorTarihi: string, sayfa: number): void {
        this.gunDetaylari[valorTarihi] = { loading: true, sayfa, sonuc: this.gunDetaylari[valorTarihi]?.sonuc ?? null };

        this.service.getValorGunDetaylari(this.takvimBankaId, valorTarihi, sayfa, GUN_DETAY_SAYFA_BOYUTU)
            .pipe(finalize(() => this.cdr.detectChanges()))
            .subscribe({
                next: (sonuc) => {
                    this.gunDetaylari[valorTarihi] = { loading: false, sayfa, sonuc };
                },
                error: (error: unknown) => {
                    this.gunDetaylari[valorTarihi] = { loading: false, sayfa, sonuc: null };
                    this.showError(error);
                }
            });
    }

    /** POS Valör Takibi ekrani su an banka/tarih filtresini query param olarak desteklemiyor
     * (bkz. round raporundaki bilinen sinirlamalar) - bu yuzden yalnizca genel ekrana yonlendirir. */
    gitPosValorTakibine(): void {
        this.router.navigate(['/muhasebe/pos-tahsilat-valor']);
    }

    copyIban(iban: string | null | undefined): void {
        if (!iban) {
            return;
        }

        navigator.clipboard.writeText(iban).then(
            () => this.messageService.add({ severity: UiSeverity.Success, summary: 'Kopyalandı', detail: 'IBAN panoya kopyalandı.' }),
            () => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: 'IBAN kopyalanamadı.' })
        );
    }

    formatIban(iban: string | null | undefined): string {
        if (!iban) {
            return '-';
        }
        return iban.replace(/(.{4})/g, '$1 ').trim();
    }

    getTutarClass(tutar: number): string {
        if (tutar > 0) {
            return 'text-green-600';
        }
        if (tutar < 0) {
            return 'text-red-500';
        }
        return '';
    }

    getUyariLabel(uyariTipi: string): string {
        return this.uyariLabellari[uyariTipi] ?? uyariTipi;
    }

    getDurumSeverity(durum: string): 'info' | 'warn' | 'danger' | 'secondary' {
        switch (durum) {
            case 'ValorBekliyor':
                return 'info';
            case 'MutabakatBekliyor':
                return 'warn';
            case 'Hata':
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private buildFilter(tesisId: number): NakitBankaPozisyonuFilterModel {
        return {
            tesisId,
            raporTarihi: this.toIsoDate(this.raporTarihi),
            hesapTuru: this.hesapTuru,
            paraBirimi: this.paraBirimi,
            valorDurumu: this.valorDurumu
        };
    }

    private toIsoDate(date: Date | null): string | null {
        if (!date) {
            return null;
        }
        const y = date.getFullYear();
        const m = (date.getMonth() + 1).toString().padStart(2, '0');
        const d = date.getDate().toString().padStart(2, '0');
        return `${y}-${m}-${d}`;
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.errorMessage = message;
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
