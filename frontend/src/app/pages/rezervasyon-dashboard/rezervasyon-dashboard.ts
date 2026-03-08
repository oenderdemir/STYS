import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { RezervasyonDashboardDto, RezervasyonTesisDto } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './rezervasyon-dashboard.html',
    providers: [MessageService]
})
export class RezervasyonDashboard implements OnInit {
    private readonly service = inject(RezervasyonYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    selectedTesisId: number | null = null;
    selectedTarih = this.todayInput();
    dashboard: RezervasyonDashboardDto | null = null;

    loadingReferences = false;
    loadingDashboard = false;

    private readonly durumTaslak = 'Taslak';
    private readonly durumOnayli = 'Onayli';
    private readonly durumCheckInTamamlandi = 'CheckInTamamlandi';
    private readonly durumCheckOutTamamlandi = 'CheckOutTamamlandi';
    private readonly durumIptal = 'Iptal';

    get canView(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.View');
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        if (this.selectedTesisId && this.selectedTesisId > 0) {
            this.loadDashboard();
            return;
        }

        this.loadReferences();
    }

    onTesisChange(): void {
        this.loadDashboard();
    }

    onTarihChange(): void {
        this.loadDashboard();
    }

    formatDateTime(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return date.toLocaleString('tr-TR');
    }

    getRezervasyonDurumLabel(durum: string): string {
        switch (durum) {
            case this.durumTaslak:
                return 'Taslak';
            case this.durumOnayli:
                return 'Onaylı';
            case this.durumCheckInTamamlandi:
                return 'Giriş Yapıldı';
            case this.durumCheckOutTamamlandi:
                return 'Çıkış Yapıldı';
            case this.durumIptal:
                return 'İptal';
            default:
                return durum;
        }
    }

    getRezervasyonDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (durum) {
            case this.durumTaslak:
                return 'secondary';
            case this.durumOnayli:
                return 'info';
            case this.durumCheckInTamamlandi:
                return 'warn';
            case this.durumCheckOutTamamlandi:
                return 'success';
            case this.durumIptal:
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private loadReferences(): void {
        this.loadingReferences = true;
        this.service
            .getTesisler()
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    if (this.selectedTesisId && !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId && this.tesisler.length > 0) {
                        this.selectedTesisId = this.tesisler[0].id;
                    }

                    this.loadDashboard();
                },
                error: (error: unknown) => {
                    this.tesisler = [];
                    this.selectedTesisId = null;
                    this.dashboard = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadDashboard(): void {
        if (!this.canView) {
            this.dashboard = null;
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.dashboard = null;
            return;
        }

        this.loadingDashboard = true;
        this.service
            .getGunlukDashboard(this.selectedTesisId, this.selectedTarih)
            .pipe(
                finalize(() => {
                    this.loadingDashboard = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (dashboard) => {
                    this.dashboard = dashboard;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.dashboard = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private todayInput(): string {
        const date = new Date();
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
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
