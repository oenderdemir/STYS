import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import {
    ErisimTeshisIstekDto,
    ErisimTeshisIslemSonucDto,
    ErisimTeshisKullaniciGrupDto,
    ErisimTeshisMenuSeviyeDto,
    ErisimTeshisModulDto,
    ErisimTeshisReferansDto,
    ErisimTeshisSonucDto,
    ErisimTeshisTesisDto
} from './erisim-teshis.dto';
import { ErisimTeshisService } from './erisim-teshis.service';

@Component({
    selector: 'app-erisim-teshis',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './erisim-teshis.html',
    styleUrl: './erisim-teshis.scss',
    providers: [MessageService]
})
export class ErisimTeshis implements OnInit {
    private readonly service = inject(ErisimTeshisService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    referanslar: ErisimTeshisReferansDto = { kullanicilar: [], tesisler: [], moduller: [] };
    selectedKullaniciId: string | null = null;
    selectedModulAnahtari: string | null = null;
    selectedTesisId: number | null = null;
    sonuc: ErisimTeshisSonucDto | null = null;
    loading = false;
    analyzing = false;

    readonly canView = computed(() => this.authService.hasPermission('ErisimTeshisYonetimi.View'));

    get blockedOperations(): ErisimTeshisIslemSonucDto[] {
        return this.sonuc?.islemler.filter((x) => !x.sonuc) ?? [];
    }

    get warningOperations(): ErisimTeshisIslemSonucDto[] {
        return this.sonuc?.islemler.filter((x) => x.durum === 'Uyari') ?? [];
    }

    get successfulOperations(): ErisimTeshisIslemSonucDto[] {
        return this.sonuc?.islemler.filter((x) => x.durum === 'Basarili') ?? [];
    }

    get selectedModul(): ErisimTeshisModulDto | null {
        if (!this.selectedModulAnahtari) {
            return null;
        }

        return this.referanslar.moduller.find((x) => x.anahtar === this.selectedModulAnahtari) ?? null;
    }

    get selectedTesisRequired(): boolean {
        return !!this.selectedModul?.tesisSecimiGerekli;
    }

    get menuPathSegments(): string[] {
        const menuPath = this.sonuc?.menuGorunumu.menuYolu?.trim();
        return menuPath ? menuPath.split('>').map((x) => x.trim()).filter((x) => x.length > 0) : [];
    }

    get menuChain(): ErisimTeshisMenuSeviyeDto[] {
        return this.sonuc?.menuGorunumu.menuZinciri ?? [];
    }

    ngOnInit(): void {
        this.loadReferanslar();
    }

    refresh(): void {
        this.loadReferanslar();
    }

    onModulChange(): void {
        if (!this.selectedTesisRequired) {
            this.selectedTesisId = null;
        }
        this.sonuc = null;
    }

    analyze(): void {
        if (!this.canView()) {
            return;
        }

        if (!this.selectedKullaniciId || !this.selectedModulAnahtari) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kullanici ve modul seciniz.' });
            return;
        }

        if (this.selectedTesisRequired && !this.selectedTesisId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Bu modul icin tesis secimi zorunludur.' });
            return;
        }

        const payload: ErisimTeshisIstekDto = {
            kullaniciId: this.selectedKullaniciId,
            modulAnahtari: this.selectedModulAnahtari,
            tesisId: this.selectedTesisRequired ? this.selectedTesisId : null
        };

        this.analyzing = true;
        this.service
            .teshisEt(payload)
            .pipe(
                finalize(() => {
                    this.analyzing = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.sonuc = result;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.sonuc = null;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (durum) {
            case 'Basarili':
                return UiSeverity.Success;
            case 'Uyari':
                return UiSeverity.Warn;
            case 'Engelli':
                return UiSeverity.Danger;
            default:
                return UiSeverity.Secondary;
        }
    }

    getSummaryCardClass(type: 'success' | 'warn' | 'danger' | 'info'): string {
        return `summary-card ${type}`;
    }

    getDecisionCardClass(row: ErisimTeshisIslemSonucDto): string {
        if (row.durum === 'Engelli') {
            return 'decision-card blocked';
        }

        if (row.durum === 'Uyari') {
            return 'decision-card warning';
        }

        return 'decision-card success';
    }

    getEngelSeverity(engelKodu: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (engelKodu) {
            case 'YetkiEksik':
                return UiSeverity.Danger;
            case 'ScopeDisi':
                return UiSeverity.Warn;
            case 'TesisSecilmedi':
                return UiSeverity.Info;
            default:
                return UiSeverity.Secondary;
        }
    }

    getEngelEtiketi(engelKodu: string): string {
        switch (engelKodu) {
            case 'YetkiEksik':
                return 'Yetki Eksik';
            case 'ScopeDisi':
                return 'Scope Disi';
            case 'TesisSecilmedi':
                return 'Tesis Secilmedi';
            default:
                return 'Sorun Yok';
        }
    }

    async copySummary(): Promise<void> {
        if (!this.sonuc || typeof navigator === 'undefined' || !navigator.clipboard) {
            return;
        }

        const lines = [
            `Kullanici: ${this.sonuc.kullanici.kullaniciAdi}`,
            `Modul: ${this.sonuc.modul.ad}`,
            `Tesis: ${this.sonuc.seciliTesis?.ad ?? '-'}`,
            `Genel Durum: ${this.sonuc.genelDurum}`,
            `Destek Notu: ${this.sonuc.destekNotu}`,
            `Menu Yolu: ${this.sonuc.menuGorunumu.menuYolu}`,
            `Menu Route: ${this.sonuc.menuGorunumu.route}`,
            `Sidebarda Gorunur: ${this.sonuc.menuGorunumu.sidebardaGorunur ? 'Evet' : 'Hayir'}`,
            `Ozet: ${this.sonuc.ozet}`,
            '',
            'Islem Sonuclari:'
        ];

        if (this.sonuc.menuGorunumu.gerekliMenuYetkileri.length > 0) {
            lines.push('', 'Menu Yetkileri:');
            for (const permission of this.sonuc.menuGorunumu.gerekliMenuYetkileri) {
                lines.push(`- ${permission}`);
            }
        }

        for (const item of this.sonuc.islemler) {
            lines.push(`- ${item.islemAdi}: ${item.durum} | ${item.aciklama}`);
        }

        if (this.sonuc.onerilenAksiyonlar.length > 0) {
            lines.push('', 'Onerilen Aksiyonlar:');
            for (const aksiyon of this.sonuc.onerilenAksiyonlar) {
                lines.push(`- ${aksiyon}`);
            }
        }

        await navigator.clipboard.writeText(lines.join('\n'));
        this.messageService.add({ severity: UiSeverity.Success, summary: 'Kopyalandi', detail: 'Teshis ozeti panoya kopyalandi.' });
    }

    trackPermission(_: number, permission: string): string {
        return permission;
    }

    trackGroup(_: number, group: ErisimTeshisKullaniciGrupDto): string {
        return group.grupAdi;
    }

    private loadReferanslar(): void {
        if (!this.canView()) {
            return;
        }

        this.loading = true;
        this.service
            .getReferanslar()
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (data) => {
                    this.referanslar = {
                        kullanicilar: [...data.kullanicilar].sort((a, b) => (a.kullaniciAdi ?? '').localeCompare(b.kullaniciAdi ?? '')),
                        tesisler: [...data.tesisler].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? '')),
                        moduller: [...data.moduller].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? ''))
                    };

                    if (!this.selectedKullaniciId && this.referanslar.kullanicilar.length > 0) {
                        this.selectedKullaniciId = this.referanslar.kullanicilar[0].id;
                    }

                    if (!this.selectedModulAnahtari && this.referanslar.moduller.length > 0) {
                        this.selectedModulAnahtari = this.referanslar.moduller[0].anahtar;
                    }

                    this.onModulChange();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
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
