import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
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
import { RezervasyonDegisiklikGecmisiDialogComponent } from '../rezervasyon-yonetimi/components/rezervasyon-degisiklik-gecmisi-dialog/rezervasyon-degisiklik-gecmisi-dialog';
import { RezervasyonKonaklayanPlaniDialogComponent } from '../rezervasyon-yonetimi/components/rezervasyon-konaklayan-plani-dialog/rezervasyon-konaklayan-plani-dialog';
import { RezervasyonOdaDegisimiDialogComponent } from '../rezervasyon-yonetimi/components/rezervasyon-oda-degisimi-dialog/rezervasyon-oda-degisimi-dialog';
import { RezervasyonOdemeDialogComponent } from '../rezervasyon-yonetimi/components/rezervasyon-odeme-dialog/rezervasyon-odeme-dialog';
import { OdaRezervasyonTakvimiService } from './oda-rezervasyon-takvimi.service';
import {
    OdaRezervasyonTakvimiDto,
    OdaRezervasyonBlokDto,
    OdaRezervasyonOdaSatiriDto,
    OdaRezervasyonOdaTipiGrupDto,
    OdaRezervasyonTakvimGunDto,
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

interface OdaRezervasyonBlokViewModel extends OdaRezervasyonBlokDto {
    uiClass: string;
    uiGridColumn: string;
    uiLeft: string;
    uiRight: string;
    uiTooltip: string;
}

interface OdaRezervasyonOdaSatiriViewModel extends Omit<OdaRezervasyonOdaSatiriDto, 'bloklar'> {
    bloklar: OdaRezervasyonBlokViewModel[];
}

interface OdaRezervasyonOdaTipiGrupViewModel extends Omit<OdaRezervasyonOdaTipiGrupDto, 'odalar'> {
    odalar: OdaRezervasyonOdaSatiriViewModel[];
}

interface OdaRezervasyonTakvimiViewModel extends Omit<OdaRezervasyonTakvimiDto, 'odaTipleri'> {
    odaTipleri: OdaRezervasyonOdaTipiGrupViewModel[];
}

const TakvimDurumFiltreleri = {
    Onayli: 'Onayli',
    CheckIn: 'CheckIn',
    OdemeEksik: 'OdemeEksik',
    BakimAriza: 'BakimAriza'
} as const;

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
        ToolbarModule,
        RezervasyonDegisiklikGecmisiDialogComponent,
        RezervasyonKonaklayanPlaniDialogComponent,
        RezervasyonOdaDegisimiDialogComponent,
        RezervasyonOdemeDialogComponent
    ],
    providers: [MessageService],
    templateUrl: './oda-rezervasyon-takvimi.html',
    styleUrl: './oda-rezervasyon-takvimi.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
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

    takvim: OdaRezervasyonTakvimiViewModel | null = null;
    yukleniyor = false;
    tesislerYukleniyor = false;
    odaTipleriYukleniyor = false;

    secilenBlok: OdaRezervasyonBlokViewModel | null = null;
    blokDetayVisible = false;
    legendVisible = false;

    degisiklikGecmisiDialogVisible = false;
    degisiklikGecmisiRezervasyonId: number | null = null;
    degisiklikGecmisiReferansNo = '';
    konaklayanPlanDialogVisible = false;
    konaklayanPlanRezervasyonId: number | null = null;
    konaklayanPlanReferansNo = '';
    odaDegisimDialogVisible = false;
    odaDegisimRezervasyonId: number | null = null;
    odaDegisimReferansNo = '';
    odaDegisimRezervasyonDurumu: string | null = null;
    odemeDialogVisible = false;
    odemeRezervasyonId: number | null = null;
    odemeReferansNo = '';
    odemeRezervasyonDurumu: string | null = null;

    private takvimRequestSeq = 0;
    private odaTipiRequestSeq = 0;
    private readonly collapsedOdaTipiIds = new Set<number>();

    readonly gunSayisiSecenekleri: GunSayisiSecenegi[] = [
        { label: '7 Gün', value: 7 },
        { label: '14 Gün', value: 14 },
        { label: '30 Gün', value: 30 }
    ];

    readonly durumSecenekleri: DurumSecenegi[] = [
        { label: 'Tümü', value: null },
        { label: 'Onaylı', value: TakvimDurumFiltreleri.Onayli },
        { label: 'Check-in Yapılmış', value: TakvimDurumFiltreleri.CheckIn },
        { label: 'Ödeme Eksik', value: TakvimDurumFiltreleri.OdemeEksik },
        { label: 'Bakım / Arıza', value: TakvimDurumFiltreleri.BakimAriza }
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

        if (!this.selectedTesisId) {
            this.odaTipleriYukleniyor = false;
            this.takvim = null;
            this.cdr.markForCheck();
            return;
        }

        const tesisId = this.selectedTesisId;
        const requestSeq = ++this.odaTipiRequestSeq;
        this.odaTipleriYukleniyor = true;

        this.rezervasyonService
            .getOdaTipleriByTesis(tesisId)
            .pipe(finalize(() => {
                if (requestSeq === this.odaTipiRequestSeq) {
                    this.odaTipleriYukleniyor = false;
                    this.cdr.markForCheck();
                }
            }))
            .subscribe({
                next: (tipler) => {
                    if (requestSeq === this.odaTipiRequestSeq && tesisId === this.selectedTesisId) {
                        this.odaTipleri = tipler;
                    }
                },
                error: (err: HttpErrorResponse) => {
                    if (requestSeq === this.odaTipiRequestSeq) {
                        this.messageService.add({
                            severity: 'error',
                            summary: 'Hata',
                            detail: tryReadApiMessage(err) ?? 'Oda tipleri alınamadı.'
                        });
                    }
                }
            });

        this.takvimYukle();
    }

    takvimYukle(): void {
        if (!this.selectedTesisId || !this.selectedBaslangicTarihi) return;

        const baslangicStr = toLocalDateString(this.selectedBaslangicTarihi);
        if (!baslangicStr) return;

        const requestSeq = ++this.takvimRequestSeq;
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
            .pipe(finalize(() => {
                if (requestSeq === this.takvimRequestSeq) {
                    this.yukleniyor = false;
                    this.cdr.markForCheck();
                }
            }))
            .subscribe({
                next: (takvim) => {
                    if (requestSeq === this.takvimRequestSeq) {
                        this.takvim = this.toTakvimViewModel(takvim);
                    }
                },
                error: (err: HttpErrorResponse) => {
                    if (requestSeq === this.takvimRequestSeq) {
                        this.messageService.add({
                            severity: 'error',
                            summary: 'Hata',
                            detail: tryReadApiMessage(err) ?? 'Takvim yüklenemedi.'
                        });
                    }
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

    isGrupCollapsed(odaTipiId: number): boolean {
        return this.collapsedOdaTipiIds.has(odaTipiId);
    }

    toggleGrup(odaTipiId: number): void {
        if (this.collapsedOdaTipiIds.has(odaTipiId)) {
            this.collapsedOdaTipiIds.delete(odaTipiId);
        } else {
            this.collapsedOdaTipiIds.add(odaTipiId);
        }
        this.cdr.markForCheck();
    }

    tumGruplariAc(): void {
        this.collapsedOdaTipiIds.clear();
        this.cdr.markForCheck();
    }

    tumGruplariKapat(): void {
        this.takvim?.odaTipleri.forEach(g => this.collapsedOdaTipiIds.add(g.odaTipiId));
        this.cdr.markForCheck();
    }

    private toTakvimViewModel(dto: OdaRezervasyonTakvimiDto): OdaRezervasyonTakvimiViewModel {
        return {
            ...dto,
            odaTipleri: dto.odaTipleri.map(grup => ({
                ...grup,
                odalar: grup.odalar.map(oda => ({
                    ...oda,
                    bloklar: oda.bloklar.map(blok => ({
                        ...blok,
                        uiClass: this.getBlokClass(blok),
                        uiGridColumn: this.getBlokGridColumn(blok),
                        uiLeft: this.getBlokLeft(blok),
                        uiRight: this.getBlokRight(blok),
                        uiTooltip: this.getBlokTooltip(blok)
                    }))
                }))
            }))
        };
    }

    private getBlokClass(blok: OdaRezervasyonBlokDto): string {
        const classes = ['reservation-block', `block-${blok.renkTipi}`];
        if (!blok.solKenaraDevamEdiyor) classes.push('has-checkin-start');
        if (!blok.sagKenaraDevamEdiyor) classes.push('has-checkout-end');
        return classes.join(' ');
    }

    private getBlokGridColumn(blok: OdaRezervasyonBlokDto): string {
        // +1 çünkü room-days-area kendi nested grid'i; oda bilgi sütunu burada yok
        const start = blok.baslangicGunIndex + 1;
        const checkoutEklendi = !blok.sagKenaraDevamEdiyor ? 1 : 0;
        // gunUzunlugu 0 olabilir (sadece checkout kuyruğu): en az 1 sütun kapla
        const end = Math.max(start + 1, start + blok.gunUzunlugu + checkoutEklendi);
        return `${start} / ${end}`;
    }

    private getBlokLeft(blok: OdaRezervasyonBlokDto): string {
        if (blok.solKenaraDevamEdiyor) return '0';
        // Giriş bu takvimde → giriş sütununun sağ yarısından başla
        const checkoutEklendi = !blok.sagKenaraDevamEdiyor ? 1 : 0;
        const dispCols = Math.max(1, blok.gunUzunlugu + checkoutEklendi);
        return `${50 / dispCols}%`;
    }

    private getBlokRight(blok: OdaRezervasyonBlokDto): string {
        if (blok.sagKenaraDevamEdiyor) return '0';
        // Çıkış bu takvimde → çıkış sütununun sol yarısında bitir
        const checkoutEklendi = 1;
        const dispCols = Math.max(1, blok.gunUzunlugu + checkoutEklendi);
        return `${50 / dispCols}%`;
    }

    private getBlokTooltip(blok: OdaRezervasyonBlokDto): string {
        if (blok.blokTipi === 'Rezervasyon') {
            const satirlar: string[] = [blok.baslik];
            if (blok.altBaslik) satirlar.push(`#${blok.altBaslik}`);
            satirlar.push(`Giriş: ${this.formatBlokTarih(blok.baslangicTarihi)}`);
            satirlar.push(`Çıkış: ${this.formatBlokTarih(blok.bitisTarihi)}`);
            satirlar.push(`Geceleme: ${blok.geceSayisi} gece`);
            satirlar.push(`Durum: ${this.durumLabel(blok.durum)}`);
            if (blok.toplamUcret != null) {
                satirlar.push(`Toplam: ${this.formatPara(blok.toplamUcret, blok.paraBirimi)}`);
            }
            if (blok.odemeEksikMi && blok.kalanTutar != null) {
                satirlar.push(`Kalan: ${this.formatPara(blok.kalanTutar, blok.paraBirimi)}`);
            }
            if (blok.uyarilar?.length) satirlar.push(...blok.uyarilar);
            return satirlar.join('\n');
        }
        const satirlar = [blok.baslik];
        if (blok.altBaslik) satirlar.push(blok.altBaslik);
        satirlar.push(`Başlangıç: ${this.formatBlokTarih(blok.baslangicTarihi)}`);
        satirlar.push(`Bitiş: ${this.formatBlokTarih(blok.bitisTarihi)}`);
        return satirlar.join('\n');
    }

    formatBlokTarih(tarih: string): string {
        if (!tarih) return '-';
        const d = new Date(tarih);
        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit', month: '2-digit', year: 'numeric',
            hour: '2-digit', minute: '2-digit', hour12: false
        }).format(d);
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

    blokDetayAc(blok: OdaRezervasyonBlokViewModel): void {
        this.secilenBlok = blok;
        this.blokDetayVisible = true;
    }

    blokDetayKapat(): void {
        this.blokDetayVisible = false;
        this.secilenBlok = null;
    }

    legendAc(): void {
        this.legendVisible = true;
    }

    legendKapat(): void {
        this.legendVisible = false;
    }

    rezervasyonYonetimineGit(rezervasyonId: number | null): void {
        if (!rezervasyonId) return;
        this.router.navigate(['/rezervasyon-yonetimi'], { queryParams: { rezervasyonId } });
        this.blokDetayKapat();
    }

    odemeAc(blok: OdaRezervasyonBlokViewModel): void {
        if (!blok.rezervasyonId) return;
        this.odemeRezervasyonId = blok.rezervasyonId;
        this.odemeReferansNo = blok.altBaslik ?? '';
        this.odemeRezervasyonDurumu = blok.durum ?? null;
        this.odemeDialogVisible = true;
        this.blokDetayKapat();
    }

    degisiklikGecmisiAc(blok: OdaRezervasyonBlokViewModel): void {
        if (!blok.rezervasyonId) return;
        this.degisiklikGecmisiRezervasyonId = blok.rezervasyonId;
        this.degisiklikGecmisiReferansNo = blok.altBaslik ?? '';
        this.degisiklikGecmisiDialogVisible = true;
        this.blokDetayKapat();
    }

    konaklayanPlaniAc(blok: OdaRezervasyonBlokViewModel): void {
        if (!blok.rezervasyonId) return;
        this.konaklayanPlanRezervasyonId = blok.rezervasyonId;
        this.konaklayanPlanReferansNo = blok.altBaslik ?? '';
        this.konaklayanPlanDialogVisible = true;
        this.blokDetayKapat();
    }

    odaDegisimiAc(blok: OdaRezervasyonBlokViewModel): void {
        if (!blok.rezervasyonId) return;
        this.odaDegisimRezervasyonId = blok.rezervasyonId;
        this.odaDegisimReferansNo = blok.altBaslik ?? '';
        this.odaDegisimRezervasyonDurumu = blok.durum ?? null;
        this.odaDegisimDialogVisible = true;
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

    gunDolulukOrani(gun: OdaRezervasyonTakvimGunDto): number {
        const toplam = gun.doluOdaSayisi + gun.bosOdaSayisi;
        if (toplam === 0) return 0;
        return Math.round((gun.doluOdaSayisi / toplam) * 100);
    }

    gunDolulukClass(gun: OdaRezervasyonTakvimGunDto): string {
        const oran = this.gunDolulukOrani(gun);
        if (oran >= 80) return 'occupancy-high';
        if (oran >= 50) return 'occupancy-medium';
        return 'occupancy-low';
    }
}
