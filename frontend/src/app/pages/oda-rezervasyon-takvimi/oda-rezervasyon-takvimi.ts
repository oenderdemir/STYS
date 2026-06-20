import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { toLocalDateString } from '../../core/utils/date-time.util';
import { RezervasyonYonetimiService } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto, RezervasyonOdaTipiDto } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { OdaRezervasyonTakvimiService } from './oda-rezervasyon-takvimi.service';
import {
    OdaRezervasyonTakvimiDto,
    OdaRezervasyonBlokDto,
    OdaRezervasyonTakvimOzetDto
} from './oda-rezervasyon-takvimi.dto';

interface GunSayisiSecenegi {
    label: string;
    value: number;
}

interface DurumSecenegi {
    label: string;
    value: string | null;
}

@Component({
    selector: 'app-oda-rezervasyon-takvimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DatePickerModule,
        DialogModule,
        SelectModule,
        SkeletonModule,
        TagModule,
        ToastModule,
        TooltipModule,
        ToolbarModule
    ],
    providers: [MessageService],
    templateUrl: './oda-rezervasyon-takvimi.html',
    styleUrl: './oda-rezervasyon-takvimi.scss'
})
export class OdaRezervasyonTakvimi implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly takvimiService = inject(OdaRezervasyonTakvimiService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    odaTipleri: RezervasyonOdaTipiDto[] = [];

    selectedTesisId: number | null = null;
    selectedBaslangicTarihi: Date | null = new Date();
    selectedGunSayisi: number = 14;
    selectedOdaTipiId: number | null = null;
    selectedDurum: string | null = null;

    takvim: OdaRezervasyonTakvimiDto | null = null;
    yukleniyor = false;
    tesislerYukleniyor = false;

    secilenBlok: OdaRezervasyonBlokDto | null = null;
    blokDetayVisible = false;

    readonly gunSayisiSecenekleri: GunSayisiSecenegi[] = [
        { label: '7 Gün', value: 7 },
        { label: '14 Gün', value: 14 },
        { label: '30 Gün', value: 30 }
    ];

    readonly durumSecenekleri: DurumSecenegi[] = [
        { label: 'Tümü', value: null },
        { label: 'Onaylı', value: 'Onayli' },
        { label: 'Check-in Yapılmış', value: 'CheckIn' },
        { label: 'Ödeme Eksik', value: 'OdemeEksik' },
        { label: 'Bakım / Arıza', value: 'BakimAriza' }
    ];

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    private tesisleriYukle(): void {
        this.tesislerYukleniyor = true;
        this.rezervasyonService
            .getTesisler()
            .pipe(finalize(() => { this.tesislerYukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = tesisler;
                    if (tesisler.length > 0 && !this.selectedTesisId) {
                        this.selectedTesisId = tesisler[0].id;
                        this.onTesisChange();
                    }
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Tesis listesi alınamadı.'
                    });
                }
            });
    }

    onTesisChange(): void {
        this.odaTipleri = [];
        this.selectedOdaTipiId = null;
        if (!this.selectedTesisId) return;

        this.rezervasyonService.getOdaTipleriByTesis(this.selectedTesisId).subscribe({
            next: (tipler) => {
                this.odaTipleri = tipler;
                this.cdr.markForCheck();
            }
        });

        this.takvimYukle();
    }

    takvimYukle(): void {
        if (!this.selectedTesisId || !this.selectedBaslangicTarihi) return;

        const baslangicStr = toLocalDateString(this.selectedBaslangicTarihi);
        if (!baslangicStr) return;

        this.yukleniyor = true;
        this.takvim = null;

        this.takvimiService
            .getTakvim(
                this.selectedTesisId,
                baslangicStr,
                this.selectedGunSayisi,
                this.selectedOdaTipiId,
                this.selectedDurum
            )
            .pipe(finalize(() => { this.yukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (takvim) => {
                    this.takvim = takvim;
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Takvim yüklenemedi.'
                    });
                }
            });
    }

    bugunGit(): void {
        this.selectedBaslangicTarihi = new Date();
        this.takvimYukle();
    }

    haftaGeri(): void {
        if (!this.selectedBaslangicTarihi) return;
        const d = new Date(this.selectedBaslangicTarihi);
        d.setDate(d.getDate() - 7);
        this.selectedBaslangicTarihi = d;
        this.takvimYukle();
    }

    haftaIleri(): void {
        if (!this.selectedBaslangicTarihi) return;
        const d = new Date(this.selectedBaslangicTarihi);
        d.setDate(d.getDate() + 7);
        this.selectedBaslangicTarihi = d;
        this.takvimYukle();
    }

    getBlokClass(blok: OdaRezervasyonBlokDto): string {
        const base = 'reservation-block';
        const renk = blok.renkTipi;
        return `${base} block-${renk}`;
    }

    getBlokGridColumn(blok: OdaRezervasyonBlokDto): string {
        const start = blok.baslangicGunIndex + 2;
        const end = start + Math.max(1, blok.gunUzunlugu);
        return `${start} / ${end}`;
    }

    getBlokTooltip(blok: OdaRezervasyonBlokDto): string {
        if (blok.blokTipi === 'Rezervasyon') {
            const satirlar = [blok.baslik];
            if (blok.altBaslik) satirlar.push(`#${blok.altBaslik}`);
            if (blok.durum) satirlar.push(this.durumLabel(blok.durum));
            if (blok.uyarilar?.length) satirlar.push(...blok.uyarilar);
            return satirlar.join('\n');
        }
        return [blok.baslik, blok.altBaslik].filter(Boolean).join('\n');
    }

    durumLabel(durum: string): string {
        const map: Record<string, string> = {
            Taslak: 'Taslak',
            Onayli: 'Onaylı',
            CheckInTamamlandi: 'Check-in Yapıldı',
            CheckOutTamamlandi: 'Check-out Yapıldı',
            Bakim: 'Bakım',
            Ariza: 'Arıza'
        };
        return map[durum] ?? durum;
    }

    temizlikLabel(durum: string | null): string {
        if (!durum) return '';
        const map: Record<string, string> = {
            Hazir: 'Hazır',
            Kirli: 'Kirli',
            Temizleniyor: 'Temizleniyor'
        };
        return map[durum] ?? durum;
    }

    temizlikSeverity(durum: string | null): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        if (!durum) return 'secondary';
        const map: Record<string, 'success' | 'warn' | 'danger' | 'info' | 'secondary'> = {
            Hazir: 'success',
            Kirli: 'danger',
            Temizleniyor: 'warn'
        };
        return map[durum] ?? 'secondary';
    }

    blokDetayAc(blok: OdaRezervasyonBlokDto): void {
        this.secilenBlok = blok;
        this.blokDetayVisible = true;
    }

    blokDetayKapat(): void {
        this.blokDetayVisible = false;
        this.secilenBlok = null;
    }

    rezervasyonDetayinaGit(rezervasyonId: number | null): void {
        if (!rezervasyonId) return;
        this.router.navigate(['/rezervasyon-yonetimi'], { queryParams: { rezervasyonId } });
        this.blokDetayKapat();
    }

    formatPara(tutar: number | null | undefined, paraBirimi: string | null | undefined): string {
        if (tutar == null) return '-';
        return new Intl.NumberFormat('tr-TR', {
            style: 'currency',
            currency: paraBirimi ?? 'TRY',
            minimumFractionDigits: 2
        }).format(tutar);
    }

    get ozet(): OdaRezervasyonTakvimOzetDto | null {
        return this.takvim?.ozet ?? null;
    }

    get gridTemplateColumns(): string {
        const gunSayisi = this.takvim?.gunSayisi ?? 14;
        return `180px repeat(${gunSayisi}, minmax(110px, 1fr))`;
    }
}
