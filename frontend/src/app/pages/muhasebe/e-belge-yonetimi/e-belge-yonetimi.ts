import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { AuthService } from '../../auth';
import { KurumModel } from '../../kurum-yonetimi/kurum.model';
import { KurumService } from '../../kurum-yonetimi/kurum.service';
import {
    blokajNedeniLabel,
    EBelgeEntegrasyonYontemi,
    EBELGE_YONTEM_ACIKLAMALARI,
    EBELGE_YONTEM_LABELS,
    EBelgeYontemReadinessModel,
    KurumEBelgePolitikaRevizyonuModel,
    KurumEBelgePolitikasiModel,
    KurumEBelgeReadinessModel
} from './e-belge-yonetimi.dto';
import { EBelgeYonetimiService } from './e-belge-yonetimi.service';

type EBelgeStatusSeverity = 'success' | 'warn' | 'secondary' | 'danger' | 'info';

interface EBelgeStatusCard {
    baslik: string;
    durum: string;
    severity: EBelgeStatusSeverity;
    aciklama?: string;
}

interface EBelgeYontemSecenegi {
    label: string;
    value: EBelgeEntegrasyonYontemi;
    disabled: boolean;
    disabledNot?: string;
}

interface PolitikaFormDraft {
    entegrasyonYontemi: EBelgeEntegrasyonYontemi;
    aktifMi: boolean;
    hariciSistemKodu: string | null;
    degisiklikNedeni: string;
}

const GLOBAL_PROCESSING_DURUM_LABELS: Readonly<Record<string, string>> = {
    Active: 'Açık',
    Disabled: 'Kapalı (devre dışı)',
    BeforeActivationDate: 'Aktivasyon tarihi henüz gelmedi',
    InvalidDateConfiguration: 'Geçersiz tarih yapılandırması',
    InvalidTimeZoneConfiguration: 'Geçersiz saat dilimi yapılandırması'
};

@Component({
    selector: 'app-e-belge-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        CheckboxModule,
        ConfirmDialogModule,
        DatePickerModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        TextareaModule,
        ToastModule
    ],
    templateUrl: './e-belge-yonetimi.html',
    styleUrl: './e-belge-yonetimi.scss',
    providers: [MessageService, ConfirmationService]
})
export class EBelgeYonetimi implements OnInit {
    private readonly service = inject(EBelgeYonetimiService);
    private readonly kurumService = inject(KurumService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    readonly EBelgeEntegrasyonYontemi = EBelgeEntegrasyonYontemi;
    readonly yontemLabels = EBELGE_YONTEM_LABELS;
    readonly yontemAciklamalari = EBELGE_YONTEM_ACIKLAMALARI;
    readonly blokajNedeniLabel = blokajNedeniLabel;

    kurumId: number | null = null;
    kurumAdi = '';

    /** SuperAdmin'in aktif/tek bir kurumu YOKSA gösterilen, bu SAYFAYA ÖZGÜ minimal kurum seçici - yeni bir global tenant selector İCAT EDİLMEZ (bkz. görev md.5). */
    superAdminKurumSecenekleri: { label: string; value: number }[] = [];
    superAdminKurumSecimiGerekli = false;
    erisimYok = false;

    /** Faz 2B.11.1 görev md.10 - `GetMyKurumlar`'dan (`benim-kurumlarim`) yüklenen, kullanıcının ERİŞEBİLDİĞİ kurumlar - yalnız güvenli görüntüleme adı/selector kaynağıdır, `UserManagement.View` GEREKTİRMEZ. */
    private kurumlarim: KurumModel[] = [];

    politika: KurumEBelgePolitikasiModel | null = null;
    readiness: KurumEBelgeReadinessModel | null = null;
    revizyonlar: KurumEBelgePolitikaRevizyonuModel[] = [];

    policyLoading = false;
    readinessLoading = false;
    revisionLoading = false;
    saving = false;

    formDraft: PolitikaFormDraft = this.createEmptyDraft();
    aktivasyonTarihiDate: Date | null = null;
    private currentRowVersion = '';

    ngOnInit(): void {
        this.resolveKurumContext();
    }

    /**
     * Faz 2B.11.1 görev md.12 - backend sözleşmesiyle (`[Permission(View, SuperAdminPermission)]`
     * + `EnsureCanAccessKurumAsync`) BİREBİR eşleşir: SuperAdmin domain-spesifik `.View` iznine
     * SAHİP OLMAK ZORUNDA DEĞİLDİR - ÖNCEKİ sürüm SuperAdmin'i de `hasPermission` kontrolüne tabi
     * tutuyordu (yanlış AND semantiği), bu turda düzeltildi.
     */
    get canView(): boolean {
        if (!this.kurumId) {
            return false;
        }

        if (this.authService.isSuperAdminUser()) {
            return true;
        }

        return this.authService.hasPermission('MuhasebeSatisBelgeleriYonetimi.View') && this.authService.getKurumIds().includes(this.kurumId);
    }

    /**
     * Faz 2B.11.1 görev md.12 - backend sözleşmesiyle (`[Permission(Manage, SuperAdminPermission)]`
     * + `EnsureCanManageKurumAsync`: SuperAdmin OR (IsKurumAdmin AND aktif kurum eşleşiyor)) BİREBİR
     * eşleşir. ÖNCEKİ sürüm, SuperAdmin kontrolünden ÖNCE `hasPermission('...Manage')` şartını
     * ZORUNLU kılıyordu - SuperAdmin domain-spesifik Manage iznine SAHİP OLMASA bile erişebilmelidir.
     */
    get canManage(): boolean {
        if (!this.kurumId) {
            return false;
        }

        if (this.authService.isSuperAdminUser()) {
            return true;
        }

        return (
            this.authService.hasPermission('MuhasebeSatisBelgeleriYonetimi.Manage') &&
            this.authService.getAktifKurumId() === this.kurumId &&
            this.authService.isKurumAdminFor(this.kurumId)
        );
    }

    /** Görev md.27 - "Kurum Yönetimi" sayfasına yönlendiren buton, YALNIZ kullanıcının o sayfayı görebildiği durumda gösterilir. */
    get canNavigateToKurumYonetimi(): boolean {
        return this.authService.hasPermission('UserManagement.Manage') || this.authService.isSuperAdminUser();
    }

    get isAnySectionLoading(): boolean {
        return this.policyLoading || this.readinessLoading || this.revisionLoading;
    }

    refreshAll(): void {
        if (!this.kurumId) {
            return;
        }

        this.loadPolitika();
        this.loadReadiness();
        this.loadRevizyonlar();
    }

    onSuperAdminKurumSecildi(kurumId: number | null): void {
        if (kurumId === null || kurumId === undefined) {
            return;
        }

        const eslesen = this.kurumlarim.find((k) => k.id === kurumId);
        if (!eslesen) {
            return;
        }

        this.selectKurum(eslesen);
    }

    navigateToKurumYonetimi(): void {
        if (!this.kurumId) {
            return;
        }

        this.router.navigate(['/kurum-yonetimi'], { queryParams: { kurumId: this.kurumId } });
    }

    // ──────────────────────────── Hazırlık durumu kartları (görev md.10) ────────────────────────────

    get statusCards(): EBelgeStatusCard[] {
        const r = this.readiness;
        if (!r) {
            return [];
        }

        const cards: EBelgeStatusCard[] = [];

        cards.push({
            baslik: 'Genel İşlem Kapısı',
            durum: GLOBAL_PROCESSING_DURUM_LABELS[r.globalProcessingDurumu] ?? r.globalProcessingDurumu,
            severity: r.globalProcessingAktifMi ? 'success' : (r.globalProcessingDurumu === 'Disabled' ? 'secondary' : 'warn')
        });

        cards.push({
            baslik: 'Kurum Politikası',
            durum: !r.politikaYapilandirilmisMi ? 'Yapılandırılmadı' : (r.politikaAktifMi ? 'Aktif' : 'Pasif'),
            severity: !r.politikaYapilandirilmisMi ? 'warn' : (r.politikaAktifMi ? 'success' : 'danger')
        });

        cards.push({
            baslik: 'Entegrasyon Yöntemi',
            durum: this.yontemLabels[r.entegrasyonYontemi],
            severity: !r.politikaYapilandirilmisMi ? 'secondary' : (r.yontemOperasyonelMi ? 'success' : 'danger'),
            aciklama: r.yontemOperasyonelMi ? undefined : 'Henüz desteklenmiyor'
        });

        const yerelUblGerekli = r.yerelSnapshotGerekliMi || r.yerelUnsignedUblGerekliMi;

        cards.push({
            baslik: 'Satıcı Bilgileri',
            durum: !yerelUblGerekli ? 'Uygulanamaz' : (r.saticiAnaVerileriHazirMi ? 'Hazır' : 'Eksik'),
            severity: !yerelUblGerekli ? 'secondary' : (r.saticiAnaVerileriHazirMi ? 'success' : 'danger')
        });

        // Faz 2B.11.1 görev md.16 - dört durum, TAMAMEN backend alanlarından türetilir; frontend
        // `EBelgeUbl.Enabled`'ı KENDİSİ TAHMİN ETMEZ. Yerel UBL bu yöntemde N/A ise (Kullanilmayacak/
        // HariciMuhasebeSistemi) diğer üç durumdan HİÇBİRİ değerlendirilmez.
        const ublKartDurumu: { durum: string; severity: EBelgeStatusSeverity } = !r.ublFeatureUygulanabilirMi
            ? { durum: 'Uygulanamaz', severity: 'secondary' }
            : !r.ublFeatureAktifMi
              ? { durum: 'Kapalı', severity: 'danger' }
              : !r.islemeHazirMi
                ? { durum: 'Bekliyor', severity: 'warn' }
                : { durum: 'Hazır', severity: 'success' };

        cards.push({
            baslik: 'UBL Üretimi',
            durum: ublKartDurumu.durum,
            severity: ublKartDurumu.severity
        });

        cards.push({
            baslik: 'İmzalama',
            durum: !r.signingGateUygulanabilirMi ? 'Uygulanamaz' : (r.signingSuAnMumkunMu ? 'Hazır' : 'Kapalı'),
            severity: !r.signingGateUygulanabilirMi ? 'secondary' : (r.signingSuAnMumkunMu ? 'success' : 'warn')
        });

        cards.push({
            baslik: 'Otomatik Gönderim',
            durum: !r.otomatikGonderimGerekliMi ? 'Uygulanamaz' : 'Bekliyor',
            severity: !r.otomatikGonderimGerekliMi ? 'secondary' : 'warn'
        });

        return cards;
    }

    get eksikSaticiAlanVarMi(): boolean {
        return (this.readiness?.eksikSaticiAlanlari.length ?? 0) > 0;
    }

    // ──────────────────────────── Politika formu (görev md.12-17) ────────────────────────────

    get yontemSecenekleri(): EBelgeYontemSecenegi[] {
        const yontemler = this.readiness?.yontemler ?? [];
        if (yontemler.length === 0) {
            return [];
        }

        return yontemler.map((y: EBelgeYontemReadinessModel) => ({
            label: this.yontemLabels[y.yontem],
            value: y.yontem,
            disabled: !y.operasyonelMi,
            disabledNot: y.operasyonelMi ? undefined : 'Henüz desteklenmiyor'
        }));
    }

    /**
     * Faz 2B.11.1 görev md.8 - seçili yöntemin backend'den gelen TAM capability plan'ı. Component
     * içinde İKİNCİ bir capability matrix (yöntem enum'una göre if/switch) OLUŞTURULMAZ - operasyonel/
     * disabled, snapshot, unsigned UBL, imza, otomatik gönderim gibi TÜM capability değerleri BURADAN
     * okunur (bkz. `yontemSecenekleri`'nin ZATEN `y.operasyonelMi`'yi aynı prensiple kullanması).
     */
    get secilenYontemCapability(): EBelgeYontemReadinessModel | null {
        return this.readiness?.yontemler.find((y) => y.yontem === this.formDraft.entegrasyonYontemi) ?? null;
    }

    get hariciSistemKoduGerekliMi(): boolean {
        return this.secilenYontemCapability?.hariciSistemKoduGerekliMi ?? false;
    }

    get formYontemAciklamasi(): string {
        return this.yontemAciklamalari[this.formDraft.entegrasyonYontemi];
    }

    yontemLabel(yontem: EBelgeEntegrasyonYontemi): string {
        return this.yontemLabels[yontem];
    }

    get minAktivasyonTarihi(): Date | null {
        return this.parseDateOnly(this.readiness?.globalMinimumAktivasyonYerelTarihi ?? null);
    }

    get minAktivasyonTarihiMetni(): string | null {
        const parsed = this.minAktivasyonTarihi;
        return parsed ? this.formatTrDate(parsed) : null;
    }

    save(): void {
        if (!this.canManage || !this.kurumId || this.saving) {
            return;
        }

        const degisiklikNedeni = this.formDraft.degisiklikNedeni.trim();
        if (!degisiklikNedeni) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Değişiklik nedeni zorunludur.' });
            return;
        }

        if (this.hariciSistemKoduGerekliMi && !this.formDraft.hariciSistemKodu?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Harici sistem kodu zorunludur.' });
            return;
        }

        const secilenYontem = this.yontemSecenekleri.find((y) => y.value === this.formDraft.entegrasyonYontemi);
        if (this.formDraft.aktifMi && secilenYontem?.disabled) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Desteklenmiyor', detail: 'Seçilen entegrasyon yöntemi henüz desteklenmiyor; aktif politika olarak kaydedilemez.' });
            return;
        }

        if (this.formDraft.aktifMi && !this.aktivasyonTarihiDate) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Aktivasyon tarihi zorunludur.' });
            return;
        }

        this.confirmAndSave();
    }

    private confirmAndSave(): void {
        const oncekiAktif = this.politika?.aktifMi === true;
        const yeniAktif = this.formDraft.aktifMi === true;
        const yontemDegisiyor = !!this.politika && this.politika.entegrasyonYontemi !== this.formDraft.entegrasyonYontemi;

        if (oncekiAktif && !yeniAktif) {
            this.confirmationService.confirm({
                header: 'E-Belge Politikasını Pasifleştir',
                message:
                    'E-belge işlemleri durdurulacaktır. Bekleyen e-belge işleri yeni işlem başlatmayacak ve politika tekrar aktifleştirilene kadar bekleyecektir. Devam etmek istiyor musunuz?',
                icon: 'pi pi-exclamation-triangle',
                acceptButtonStyleClass: 'p-button-danger',
                rejectButtonStyleClass: 'p-button-secondary',
                acceptLabel: 'Pasifleştir',
                rejectLabel: 'Vazgeç',
                accept: () => this.doSave()
            });
            return;
        }

        if (!oncekiAktif && yeniAktif) {
            const yontemAdi = this.yontemLabels[this.formDraft.entegrasyonYontemi];
            const tarihMetni = this.aktivasyonTarihiDate ? this.formatTrDate(this.aktivasyonTarihiDate) : '-';
            this.confirmationService.confirm({
                header: 'E-Belge Politikasını Aktifleştir',
                message: `Bu kurum için e-belge politikasını aktifleştirmek üzeresiniz. Entegrasyon yöntemi: ${yontemAdi}. Aktivasyon tarihi: ${tarihMetni}. Devam etmek istiyor musunuz?`,
                icon: 'pi pi-check-circle',
                acceptButtonStyleClass: 'p-button-success',
                rejectButtonStyleClass: 'p-button-secondary',
                acceptLabel: 'Aktifleştir',
                rejectLabel: 'Vazgeç',
                accept: () => this.doSave()
            });
            return;
        }

        if (yontemDegisiyor && oncekiAktif) {
            const eskiAdi = this.politika ? this.yontemLabels[this.politika.entegrasyonYontemi] : '-';
            const yeniAdi = this.yontemLabels[this.formDraft.entegrasyonYontemi];
            this.confirmationService.confirm({
                header: 'Entegrasyon Yöntemini Değiştir',
                message: `Aktif politikanın entegrasyon yöntemi "${eskiAdi}" iken "${yeniAdi}" olarak değiştirilecek. Devam etmek istiyor musunuz?`,
                icon: 'pi pi-exclamation-triangle',
                acceptButtonStyleClass: 'p-button-warning',
                rejectButtonStyleClass: 'p-button-secondary',
                acceptLabel: 'Değiştir',
                rejectLabel: 'Vazgeç',
                accept: () => this.doSave()
            });
            return;
        }

        this.doSave();
    }

    private doSave(): void {
        if (!this.kurumId || this.saving) {
            return;
        }

        this.saving = true;
        this.service
            .updatePolitika(this.kurumId, {
                entegrasyonYontemi: this.formDraft.entegrasyonYontemi,
                aktifMi: this.formDraft.aktifMi,
                aktivasyonYerelTarihi: this.formatDateOnly(this.aktivasyonTarihiDate),
                hariciSistemKodu: this.formDraft.hariciSistemKodu?.trim() || null,
                degisiklikNedeni: this.formDraft.degisiklikNedeni.trim(),
                rowVersion: this.currentRowVersion
            })
            .pipe(finalize(() => (this.saving = false)))
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'E-belge politikası güncellendi.' });
                    this.refreshAll();
                },
                error: (error: unknown) => this.handleSaveError(error)
            });
    }

    private handleSaveError(error: unknown): void {
        if (error instanceof HttpErrorResponse && error.status === 409) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Güncel Değil',
                detail: 'Politika siz ekranı açtıktan sonra başka bir kullanıcı tarafından değiştirilmiş. Güncel bilgiler yeniden yüklendi; değişikliklerinizi kontrol ederek tekrar deneyin.',
                life: 10000
            });
            this.refreshAll();
            return;
        }

        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
    }

    // ──────────────────────────── Yükleme ────────────────────────────

    /**
     * Faz 2B.11.1 görev md.10/md.11 - kurum bağlamı ARTIK `KurumService.getAll()`/`getById()`
     * (ikisi de backend'de `UserManagement.View` GEREKTİRİR) kullanmaz - salt-okunur bir e-belge
     * Viewer'ın bu ekranı kullanmak için UserManagement izni GEREKTİRMEMESİ için TEK, güvenli
     * `GetMyKurumlar` (`GET .../benim-kurumlarim`, yalnız kimlik doğrulama gerektirir) endpoint'i
     * kullanılır - bu, tenant scope'una göre ZATEN erişilebilir kurumları döner.
     */
    private resolveKurumContext(): void {
        const aktifKurumId = this.authService.getAktifKurumId();
        if (aktifKurumId !== null) {
            this.loadAktifKurumBaglami(aktifKurumId);
            return;
        }

        if (this.authService.isSuperAdminUser()) {
            this.superAdminKurumSecimiGerekli = true;
            this.kurumService.getMyKurumlar().subscribe({
                next: (kurumlar) => {
                    this.kurumlarim = kurumlar;
                    this.superAdminKurumSecenekleri = [...kurumlar]
                        .sort((left, right) => left.ad.localeCompare(right.ad, 'tr'))
                        .map((k) => ({ label: k.ad, value: k.id }));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
            });
            return;
        }

        // Normal kullanıcının aktif bir kurumu YOKSA fail-closed: salt-okunur erişim hatası
        // gösterilir, sayfaya özgü BAŞKA bir kurum seçici SUNULMAZ (görev md.11).
        this.erisimYok = true;
    }

    /** Görev md.11 - aktif kurum HER ZAMAN otoriterdir; yalnız kullanıcının ZATEN erişebildiği kurumlar listesinden (`getMyKurumlar`) güvenli görüntüleme adı ALINIR - aktif kurum bu listede YOKSA fail-closed erişim hatası gösterilir. */
    private loadAktifKurumBaglami(kurumId: number): void {
        this.kurumService.getMyKurumlar().subscribe({
            next: (kurumlar) => {
                this.kurumlarim = kurumlar;
                const eslesen = kurumlar.find((k) => k.id === kurumId);
                if (!eslesen) {
                    this.erisimYok = true;
                    this.cdr.detectChanges();
                    return;
                }

                this.selectKurum(eslesen);
            },
            error: (error: unknown) => {
                this.erisimYok = true;
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    private selectKurum(kurum: KurumModel): void {
        this.kurumId = kurum.id;
        this.kurumAdi = kurum.ad;
        this.erisimYok = false;
        this.refreshAll();
    }

    private loadPolitika(): void {
        if (!this.kurumId) {
            return;
        }

        this.policyLoading = true;
        this.service
            .getPolitika(this.kurumId)
            .pipe(finalize(() => {
                this.policyLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (politika) => {
                    this.politika = politika;
                    this.currentRowVersion = politika?.rowVersion ?? '';
                    this.formDraft = politika
                        ? {
                              entegrasyonYontemi: politika.entegrasyonYontemi,
                              aktifMi: politika.aktifMi,
                              hariciSistemKodu: politika.hariciSistemKodu ?? null,
                              degisiklikNedeni: ''
                          }
                        : this.createEmptyDraft();
                    this.aktivasyonTarihiDate = this.parseDateOnly(politika?.aktivasyonYerelTarihi ?? null);
                },
                error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
            });
    }

    private loadReadiness(): void {
        if (!this.kurumId) {
            return;
        }

        this.readinessLoading = true;
        this.service
            .getReadiness(this.kurumId)
            .pipe(finalize(() => {
                this.readinessLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (readiness) => {
                    this.readiness = readiness;
                },
                error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
            });
    }

    private loadRevizyonlar(): void {
        if (!this.kurumId) {
            return;
        }

        this.revisionLoading = true;
        this.service
            .getRevizyonlar(this.kurumId)
            .pipe(finalize(() => {
                this.revisionLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (revizyonlar) => {
                    this.revizyonlar = [...revizyonlar].sort(
                        (left, right) => new Date(right.degisiklikZamaniUtc).getTime() - new Date(left.degisiklikZamaniUtc).getTime()
                    );
                },
                error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
            });
    }

    private createEmptyDraft(): PolitikaFormDraft {
        return {
            entegrasyonYontemi: EBelgeEntegrasyonYontemi.Kullanilmayacak,
            aktifMi: false,
            hariciSistemKodu: null,
            degisiklikNedeni: ''
        };
    }

    // ──────────────────────────── Tarih yardımcıları (görev md.12 - timezone off-by-one KORUNMASI) ────────────────────────────

    /** Yalnız "yyyy-MM-dd" TAKVİM değerini okur - UTC/timezone dönüşümü YAPILMAZ (bir gün ileri/geri kaymasını önler). */
    private parseDateOnly(value: string | null | undefined): Date | null {
        if (!value) {
            return null;
        }

        const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (!match) {
            return null;
        }

        const [, year, month, day] = match;
        return new Date(Number(year), Number(month) - 1, Number(day));
    }

    private formatDateOnly(value: Date | null): string | null {
        if (!value || isNaN(value.getTime())) {
            return null;
        }

        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    formatTrDate(value: Date | string | null | undefined): string {
        const parsed = typeof value === 'string' ? this.parseDateOnly(value) : value;
        if (!parsed || isNaN(parsed.getTime())) {
            return '-';
        }

        const day = String(parsed.getDate()).padStart(2, '0');
        const month = String(parsed.getMonth() + 1).padStart(2, '0');
        return `${day}.${month}.${parsed.getFullYear()}`;
    }

    formatTrDateTime(utcIso: string | null | undefined): string {
        if (!utcIso) {
            return '-';
        }

        const parsed = new Date(utcIso.endsWith('Z') ? utcIso : `${utcIso}Z`);
        if (isNaN(parsed.getTime())) {
            return '-';
        }

        return parsed.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const message = tryReadApiMessage(error.error ?? error);
            if (message) {
                return message;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message.trim();
        }

        return 'Beklenmeyen bir hata oluştu.';
    }
}
