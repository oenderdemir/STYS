import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
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
    BeyanEdilenOdemeEslesmeModel,
    CariHareketDokumModel,
    caprazAramaDaralticiAlanVarMi,
    DURUM_SECENEKLERI,
    ERISIM_KISITI_LABELLARI,
    GUVEN_SEVIYESI_LABELLARI,
    KOPUKLUK_LABELLARI,
    ODEME_ADAY_KAYNAKLARI,
    ODEME_CELISKI_LABELLARI,
    ODEME_YONTEMI_SECENEKLERI,
    OdemeAdayiModel,
    OdemeAramaFilterModel,
    OdemeAramaSatiriModel,
    OdemeCaprazAramaFilterModel,
    OdemeDetayModel,
    UYARI_LABELLARI
} from './odeme-izleme.dto';
import { OdemeIzlemeService } from './odeme-izleme.service';

@Component({
    selector: 'app-odeme-izleme-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
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
    templateUrl: './odeme-izleme.html',
    providers: [MessageService]
})
export class OdemeIzlemePage implements OnInit {
    private readonly service = inject(OdemeIzlemeService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    errorMessage: string | null = null;

    satirlar: OdemeAramaSatiriModel[] = [];
    toplamKayit = 0;
    sayfa = 1;
    sayfaBoyutu = 20;

    belgeNo: string | null = null;
    cariAramaMetni: string | null = null;
    tarihBaslangic: Date | null = null;
    tarihBitis: Date | null = null;
    tutarMin: number | null = null;
    tutarMax: number | null = null;
    odemeYontemi: string | null = null;
    durum: string | null = null;
    sadeceFissizOlanlar = false;
    muhasebeFisNo: string | null = null;
    rezervasyonReferansNo: string | null = null;
    iban: string | null = null;
    olusturanKullanici: string | null = null;
    maliYil: number | null = null;
    donem: number | null = null;

    // ── Capraz-kaynak arastirma ──
    caprazVisible = false;
    caprazLoading = false;
    caprazAdaylar: OdemeAdayiModel[] = [];
    caprazToplam = 0;
    caprazSayfa = 1;
    caprazSayfaBoyutu = 20;
    caprazKopuklukTipi: string | null = null;
    caprazBelgeNo: string | null = null;
    caprazMuhasebeFisNo: string | null = null;
    caprazRezervasyonReferansNo: string | null = null;
    caprazTutarMin: number | null = null;
    caprazTutarMax: number | null = null;
    caprazTarihBaslangic: Date | null = null;
    caprazTarihBitis: Date | null = null;
    // ── Beklenen (karsilastirma icin) alanlar - SONUCU DARALTMAZ, yalnizca celiski/eslesme uretir ──
    caprazBeklenenCariKartId: number | null = null;
    caprazBeklenenBankaHesapId: number | null = null;
    caprazBeklenenKasaHesapId: number | null = null;
    caprazBeklenenMuhasebeHesapPlaniId: number | null = null;
    caprazBeklenenMaliYil: number | null = null;
    caprazBeklenenDonem: number | null = null;
    /** Backend'in "en az bir daraltıcı alan zorunlu" kuralını göndermeden ÖNCE kullanıcıya gösterir. */
    caprazDaralticiAlanUyarisi = false;
    readonly kopuklukLabellari = KOPUKLUK_LABELLARI;
    readonly adayKaynaklari = ODEME_ADAY_KAYNAKLARI;
    readonly erisimKisitiLabellari = ERISIM_KISITI_LABELLARI;
    readonly celiskiLabellari = ODEME_CELISKI_LABELLARI;

    readonly odemeYontemiSecenekleri = ODEME_YONTEMI_SECENEKLERI;
    readonly durumSecenekleri = DURUM_SECENEKLERI;
    readonly uyariLabellari = UYARI_LABELLARI;
    readonly guvenSeviyesiLabellari = GUVEN_SEVIYESI_LABELLARI;

    detayVisible = false;
    detayLoading = false;
    detay: OdemeDetayModel | null = null;

    dokumVisible = false;
    dokumLoading = false;
    dokum: CariHareketDokumModel | null = null;

    karsilastirVisible = false;
    karsilastirLoading = false;
    karsilastirSonuclari: BeyanEdilenOdemeEslesmeModel[] = [];
    beyanTarih: Date = new Date();
    beyanTutar: number | null = null;
    beyanParaBirimi = 'TRY';
    beyanBelgeNoTahmini: string | null = null;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }
        this.currentTesisId = tesisId;
        if (tesisId) {
            this.load();
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

        this.loading = true;
        this.errorMessage = null;
        this.service.ara(this.sayfa, this.sayfaBoyutu, this.buildFilter(tesisId))
            .pipe(finalize(() => { this.loading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (sonuc) => {
                    this.satirlar = sonuc.items;
                    this.toplamKayit = sonuc.totalCount;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.satirlar = [];
                    this.toplamKayit = 0;
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
    }

    onFiltreDegisti(): void {
        this.sayfa = 1;
        this.load();
    }

    onSayfaDegisti(event: { first?: number | null; rows?: number | null }): void {
        const rows = event.rows ?? this.sayfaBoyutu;
        const yeniSayfa = Math.floor((event.first ?? 0) / rows) + 1;
        // PrimeNG p-table [lazy]=true, kendi ic durumu (first/rows) her CD tekrarinda AYNI
        // kaldiginda bile bazi durumlarda onLazyLoad'u tekrar tetikleyebiliyor - sayfa/boyut
        // GERCEKTEN degismediyse gereksiz/donguye girme riski tasiyan bir yeniden yuklemeyi
        // ATLA (kullanicidan gelen GERCEK bir sayfa degisikligi degilse sorgu tekrarlanmaz).
        if (yeniSayfa === this.sayfa && rows === this.sayfaBoyutu) {
            return;
        }
        this.sayfa = yeniSayfa;
        this.sayfaBoyutu = rows;
        this.load();
    }

    openDetay(satir: OdemeAramaSatiriModel): void {
        this.detayVisible = true;
        this.detayLoading = true;
        this.detay = null;
        this.service.getDetay(satir.id)
            .pipe(finalize(() => { this.detayLoading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (detay) => { this.detay = detay; this.cdr.detectChanges(); },
                error: (error: unknown) => this.showError(error)
            });
    }

    openCariHareketDokumu(cariKartId: number): void {
        this.dokumVisible = true;
        this.dokumLoading = true;
        this.dokum = null;
        this.service.getCariHareketDokumu({ cariKartId })
            .pipe(finalize(() => { this.dokumLoading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (dokum) => { this.dokum = dokum; this.cdr.detectChanges(); },
                error: (error: unknown) => this.showError(error)
            });
    }

    openKarsilastir(): void {
        this.karsilastirVisible = true;
        this.karsilastirSonuclari = [];
    }

    openCaprazArama(): void {
        this.caprazVisible = true;
        this.caprazSayfa = 1;
        // Ana ekrandaki tarih/tutar/fis-no filtreleri varsa baslangic degeri olarak devral.
        this.caprazTarihBaslangic = this.tarihBaslangic;
        this.caprazTarihBitis = this.tarihBitis;
        this.caprazTutarMin = this.tutarMin;
        this.caprazTutarMax = this.tutarMax;
        this.caprazMuhasebeFisNo = this.muhasebeFisNo;
        this.caprazBelgeNo = this.belgeNo;
        this.onCaprazFiltreDegisti();
    }

    onCaprazFiltreDegisti(): void {
        this.caprazSayfa = 1;
        this.loadCaprazArama();
    }

    caprazSayfaDegisti(event: { first?: number | null; rows?: number | null }): void {
        const rows = event.rows ?? this.caprazSayfaBoyutu;
        const yeniSayfa = Math.floor((event.first ?? 0) / rows) + 1;
        if (yeniSayfa === this.caprazSayfa && rows === this.caprazSayfaBoyutu) {
            return;
        }
        this.caprazSayfa = yeniSayfa;
        this.caprazSayfaBoyutu = rows;
        this.loadCaprazArama();
    }

    private buildCaprazFilter(tesisId: number): OdemeCaprazAramaFilterModel {
        return {
            tesisId,
            tarihBaslangic: this.toIsoDate(this.caprazTarihBaslangic),
            tarihBitis: this.toIsoDate(this.caprazTarihBitis),
            tutarMin: this.caprazTutarMin,
            tutarMax: this.caprazTutarMax,
            belgeNo: this.caprazBelgeNo,
            muhasebeFisNo: this.caprazMuhasebeFisNo,
            rezervasyonReferansNo: this.caprazRezervasyonReferansNo,
            kopuklukTipi: this.caprazKopuklukTipi,
            sadeceKopukOlanlar: true,
            beklenenCariKartId: this.caprazBeklenenCariKartId,
            beklenenBankaHesapId: this.caprazBeklenenBankaHesapId,
            beklenenKasaHesapId: this.caprazBeklenenKasaHesapId,
            beklenenMuhasebeHesapPlaniId: this.caprazBeklenenMuhasebeHesapPlaniId,
            beklenenMaliYil: this.caprazBeklenenMaliYil,
            beklenenDonem: this.caprazBeklenenDonem
        };
    }

    getCeliskiLabel(kod: string): string {
        return this.celiskiLabellari[kod] ?? kod;
    }

    loadCaprazArama(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        const filter = this.buildCaprazFilter(tesisId);
        // Backend'e gondermeden ONCE ayni kurali istemcide de uygula - kullaniciya net bir mesaj
        // goster ve gereksiz bir 400 istegi yapma.
        this.caprazDaralticiAlanUyarisi = !caprazAramaDaralticiAlanVarMi(filter);
        if (this.caprazDaralticiAlanUyarisi) {
            this.caprazAdaylar = [];
            this.caprazToplam = 0;
            return;
        }

        this.caprazLoading = true;
        this.service
            .caprazAra(this.caprazSayfa, this.caprazSayfaBoyutu, filter)
            .pipe(finalize(() => { this.caprazLoading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (sonuc) => {
                    this.caprazAdaylar = sonuc.items;
                    this.caprazToplam = sonuc.totalCount;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.caprazAdaylar = [];
                    this.caprazToplam = 0;
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
    }

    getKopuklukLabel(kod: string): string {
        return this.kopuklukLabellari[kod] ?? kod;
    }

    getKaynakLabel(kaynak: string): string {
        return this.adayKaynaklari[kaynak] ?? kaynak;
    }

    calistirKarsilastirma(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId || this.beyanTutar == null) {
            return;
        }

        this.karsilastirLoading = true;
        this.service.karsilastir({
            tesisId,
            tarih: this.toIsoDate(this.beyanTarih)!,
            tutar: this.beyanTutar,
            paraBirimi: this.beyanParaBirimi,
            belgeNoTahmini: this.beyanBelgeNoTahmini
        })
            .pipe(finalize(() => { this.karsilastirLoading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (sonuclar) => { this.karsilastirSonuclari = sonuclar; this.cdr.detectChanges(); },
                error: (error: unknown) => this.showError(error)
            });
    }

    getUyariLabel(uyariTipi: string): string {
        return this.uyariLabellari[uyariTipi] ?? uyariTipi;
    }

    getGuvenSeviyesiLabel(guvenSeviyesi: string): string {
        return this.guvenSeviyesiLabellari[guvenSeviyesi] ?? guvenSeviyesi;
    }

    getGuvenSeviyesiSeverity(guvenSeviyesi: string): 'danger' | 'warn' | 'info' | 'secondary' {
        switch (guvenSeviyesi) {
            case 'Kesin':
                return 'danger';
            case 'YuksekOlasilik':
                return 'warn';
            case 'IncelenmesiGereken':
                return 'info';
            default:
                return 'secondary';
        }
    }

    getTutarClass(tutar: number): string {
        if (tutar > 0) return 'text-green-600';
        if (tutar < 0) return 'text-red-500';
        return '';
    }

    private buildFilter(tesisId: number): OdemeAramaFilterModel {
        return {
            tesisId,
            belgeNo: this.belgeNo,
            cariAramaMetni: this.cariAramaMetni,
            tarihBaslangic: this.toIsoDate(this.tarihBaslangic),
            tarihBitis: this.toIsoDate(this.tarihBitis),
            tutarMin: this.tutarMin,
            tutarMax: this.tutarMax,
            odemeYontemi: this.odemeYontemi,
            durum: this.durum,
            sadeceFissizOlanlar: this.sadeceFissizOlanlar || null,
            muhasebeFisNo: this.muhasebeFisNo,
            rezervasyonReferansNo: this.rezervasyonReferansNo,
            iban: this.iban,
            olusturanKullanici: this.olusturanKullanici,
            maliYil: this.maliYil,
            donem: this.donem
        };
    }

    private toIsoDate(date: Date | null): string | null {
        if (!date) return null;
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
