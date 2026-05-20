import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import {
    CreateMuhasebeFisRequestModel,
    CreateMuhasebeFisSatirRequestModel,
    MuhasebeFisModel,
    MuhasebeFisTipleri
} from '../models/muhasebe-fis.model';
import { MuhasebeHesapPlaniModel } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.dto';
import { MuhasebeHesapPlaniService } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.service';
import { MuhasebeFisService } from '../services/muhasebe-fis.service';
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';

interface TesisSecenek {
    label: string;
    value: number;
}

interface HesapSecenek {
    label: string;
    value: number;
    tamKod: string;
    ad: string;
    seviyeNo: number;
    hasChildren: boolean;
}

interface SatirRow {
    siraNo: number;
    muhasebeHesapPlaniId: number | null;
    hesapKodu: string;
    hesapAdi: string;
    borc: number;
    alacak: number;
    paraBirimi: string;
    kur: number;
    aciklama: string | null;
}

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const currentYear = new Date().getFullYear();
    const options: Array<{ label: string; value: number | null }> = [];
    for (let y = currentYear - 3; y <= currentYear + 1; y++) {
        options.push({ label: String(y), value: y });
    }
    return options;
})();

const DONEM_SECENEKLERI: Array<{ label: string; value: number }> = [
    { label: '1. Dönem', value: 1 },
    { label: '2. Dönem', value: 2 },
    { label: '3. Dönem', value: 3 },
    { label: '4. Dönem', value: 4 },
    { label: '5. Dönem', value: 5 },
    { label: '6. Dönem', value: 6 },
    { label: '7. Dönem', value: 7 },
    { label: '8. Dönem', value: 8 },
    { label: '9. Dönem', value: 9 },
    { label: '10. Dönem', value: 10 },
    { label: '11. Dönem', value: 11 },
    { label: '12. Dönem', value: 12 }
];

const FIS_TIPI_SECENEKLERI: Array<{ label: string; value: string }> = [
    { label: 'Mahsup', value: MuhasebeFisTipleri.Mahsup },
    { label: 'Tahsil', value: MuhasebeFisTipleri.Tahsil },
    { label: 'Tediye', value: MuhasebeFisTipleri.Tediye },
    { label: 'Açılış', value: MuhasebeFisTipleri.Acilis },
    { label: 'Kapanış', value: MuhasebeFisTipleri.Kapanis }
];

@Component({
    selector: 'app-muhasebe-fis-olustur',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DecimalPipe,
        ButtonModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        ToastModule,
        TooltipModule
    ],
    templateUrl: './muhasebe-fis-olustur.component.html',
    styleUrls: ['./muhasebe-fis-olustur.component.scss'],
    providers: [MessageService]
})
export class MuhasebeFisOlusturComponent implements OnInit {
    private readonly service = inject(MuhasebeFisService);
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly hesapPlaniService = inject(MuhasebeHesapPlaniService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);

    loading = false;
    saving = false;

    // Form
    tesisId: number | null = null;
    fisTarihi: string = '';
    maliYil: number;
    donem: number;
    fisTipi: string = MuhasebeFisTipleri.Mahsup;
    aciklama: string | null = null;
    kaynakModul: string | null = null;
    kaynakId: number | null = null;

    // Dropdown data
    tesisSecenekleri: TesisSecenek[] = [];
    maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    donemSecenekleri = DONEM_SECENEKLERI;
    fisTipiSecenekleri = FIS_TIPI_SECENEKLERI;

    // Hesap planı tree for lookup
    hesapPlaniTree: MuhasebeHesapPlaniModel[] = [];
    hesapPlaniLoaded = false;

    // Satır table
    satirlar: SatirRow[] = [];

    // Hesap kodu lookup per row
    hesapLookupMap: Map<number, HesapSecenek[]> = new Map();
    hesapLookupLoadingMap: Map<number, boolean> = new Map();
    hesapLookupDialogVisible = false;
    lookupSatirIndex: number | null = null;
    hesapAramaMetni = '';
    filtrelenmisHesaplar: HesapSecenek[] = [];

    // Collapsible kaynak
    kaynakCollapsed = true;

    // Computed
    get toplamBorc(): number {
        return this.satirlar.reduce((sum, s) => sum + (s.borc || 0), 0);
    }

    get toplamAlacak(): number {
        return this.satirlar.reduce((sum, s) => sum + (s.alacak || 0), 0);
    }

    get bakiye(): number {
        return this.toplamBorc - this.toplamAlacak;
    }

    get bakiyeDengede(): boolean {
        return Math.abs(this.bakiye) < 0.0001;
    }

    constructor() {
        const today = new Date();
        this.fisTarihi = today.toISOString().split('T')[0];
        this.maliYil = today.getFullYear();
        this.donem = today.getMonth() + 1;
    }

    ngOnInit(): void {
        this.loadTesisler();
        this.loadHesapPlani();
    }

    private loadTesisler(): void {
        this.raporService.getTesisler().pipe(finalize(() => {
            // nothing
        })).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler.map(t => ({
                    label: t.ad,
                    value: t.id
                }));
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private loadHesapPlani(): void {
        this.hesapPlaniService.getTree().pipe(finalize(() => {
            this.hesapPlaniLoaded = true;
        })).subscribe({
            next: (tree) => {
                this.hesapPlaniTree = tree ?? [];
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private flattenTree(nodes: MuhasebeHesapPlaniModel[], prefix: string = ''): HesapSecenek[] {
        const result: HesapSecenek[] = [];
        for (const node of nodes) {
            if (!node.aktifMi) continue;
            result.push({
                label: `${node.tamKod} - ${node.ad}`,
                value: node.id!,
                tamKod: node.tamKod,
                ad: node.ad,
                seviyeNo: node.seviyeNo,
                hasChildren: !!node.hasChildren
            });
            // Also add children from tree if present (tree may be flat or nested depending on backend)
            // Tree is nested so we flatten recursively
            const children = (node as any).children as MuhasebeHesapPlaniModel[] | undefined;
            if (children && children.length > 0) {
                result.push(...this.flattenTree(children, prefix));
            }
        }
        return result;
    }

    private findByKod(partialKod: string): HesapSecenek[] {
        const flat = this.flattenTree(this.hesapPlaniTree);
        const lower = partialKod.toLowerCase().trim();
        if (!lower) return flat;
        return flat.filter(h =>
            h.tamKod.toLowerCase().includes(lower) ||
            h.ad.toLowerCase().includes(lower)
        );
    }

    openHesapLookup(satirIndex: number): void {
        this.lookupSatirIndex = satirIndex;
        this.hesapAramaMetni = '';
        this.filtrelenmisHesaplar = this.flattenTree(this.hesapPlaniTree);
        this.hesapLookupDialogVisible = true;
    }

    filtreleHesaplar(): void {
        this.filtrelenmisHesaplar = this.findByKod(this.hesapAramaMetni);
    }

    secHesap(hesap: HesapSecenek): void {
        if (this.lookupSatirIndex === null) return;
        const satir = this.satirlar[this.lookupSatirIndex];
        if (!satir) return;
        satir.muhasebeHesapPlaniId = hesap.value;
        satir.hesapKodu = hesap.tamKod;
        satir.hesapAdi = hesap.ad;
        this.hesapLookupDialogVisible = false;
        this.lookupSatirIndex = null;
    }

    satirEkle(): void {
        const siraNo = this.satirlar.length + 1;
        this.satirlar.push({
            siraNo,
            muhasebeHesapPlaniId: null,
            hesapKodu: '',
            hesapAdi: '',
            borc: 0,
            alacak: 0,
            paraBirimi: 'TRY',
            kur: 1,
            aciklama: null
        });
    }

    satirSil(index: number): void {
        this.satirlar.splice(index, 1);
        // Re-number siraNo
        this.satirlar.forEach((s, i) => s.siraNo = i + 1);
    }

    onBorcChange(satir: SatirRow): void {
        if (satir.borc > 0) {
            satir.alacak = 0;
        }
    }

    onAlacakChange(satir: SatirRow): void {
        if (satir.alacak > 0) {
            satir.borc = 0;
        }
    }

    private validateForm(): string | null {
        if (!this.tesisId || this.tesisId <= 0) {
            return 'Lütfen bir tesis seçiniz.';
        }
        if (!this.fisTarihi) {
            return 'Lütfen fiş tarihi giriniz.';
        }
        if (!this.fisTipi) {
            return 'Lütfen fiş tipi seçiniz.';
        }
        if (this.satirlar.length < 2) {
            return 'En az 2 fiş satırı eklemelisiniz.';
        }

        for (let i = 0; i < this.satirlar.length; i++) {
            const s = this.satirlar[i];
            if (!s.muhasebeHesapPlaniId || s.muhasebeHesapPlaniId <= 0) {
                return `${i + 1}. satırda geçerli bir muhasebe hesabı seçilmelidir.`;
            }
            if (s.borc < 0 || s.alacak < 0) {
                return `${i + 1}. satırda borç veya alacak negatif olamaz.`;
            }
            if (s.borc > 0 && s.alacak > 0) {
                return `${i + 1}. satırda hem borç hem alacak girilemez.`;
            }
            if (s.borc === 0 && s.alacak === 0) {
                return `${i + 1}. satırda borç veya alacak girilmelidir.`;
            }
        }

        if (!this.bakiyeDengede) {
            return `Toplam borç (${this.toplamBorc.toFixed(2)}) ile toplam alacak (${this.toplamAlacak.toFixed(2)}) eşit olmalıdır. Fark: ${Math.abs(this.bakiye).toFixed(2)}`;
        }

        return null;
    }

    kaydet(): void {
        const error = this.validateForm();
        if (error) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Doğrulama Hatası',
                detail: error,
                life: 5000
            });
            return;
        }

        const satirlar: CreateMuhasebeFisSatirRequestModel[] = this.satirlar.map((s, i) => ({
            muhasebeHesapPlaniId: s.muhasebeHesapPlaniId!,
            siraNo: s.siraNo || i + 1,
            borc: s.borc || 0,
            alacak: s.alacak || 0,
            paraBirimi: s.paraBirimi || 'TRY',
            kur: s.kur || 1,
            cariKartId: null,
            tasinirKartId: null,
            depoId: null,
            kasaBankaHesapId: null,
            aciklama: s.aciklama || null
        }));

        const request: CreateMuhasebeFisRequestModel = {
            tesisId: this.tesisId!,
            maliYil: this.maliYil,
            donem: this.donem,
            fisTarihi: this.fisTarihi,
            fisTipi: this.fisTipi,
            kaynakModul: this.kaynakModul || null,
            kaynakId: this.kaynakId || null,
            aciklama: this.aciklama || null,
            satirlar
        };

        this.saving = true;
        this.service.create(request).pipe(finalize(() => {
            this.saving = false;
        })).subscribe({
            next: (created) => {
                this.messageService.add({
                    severity: UiSeverity.Success,
                    summary: 'Başarılı',
                    detail: `Fiş oluşturuldu: ${created.fisNo}`,
                    life: 4000
                });
                this.router.navigate(['/muhasebe/fisler'], {
                    queryParams: { fisNo: created.fisNo, id: created.id }
                });
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    vazgec(): void {
        this.router.navigate(['/muhasebe/fisler']);
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error) ?? 'Bir hata oluştu.';
        this.messageService.add({
            severity: UiSeverity.Error,
            summary: 'Hata',
            detail: message,
            life: 6000
        });
    }
}
