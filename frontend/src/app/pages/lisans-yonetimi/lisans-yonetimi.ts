import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule } from 'primeng/fileupload';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';
import { tryReadApiMessage } from '../../core/api';
import { LicenseStatusDto } from './lisans-yonetimi.dto';
import { LisansYonetimiService } from './lisans-yonetimi.service';

@Component({
    selector: 'app-lisans-yonetimi',
    standalone: true,
    imports: [CommonModule, ButtonModule, FileUploadModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './lisans-yonetimi.html',
    styleUrl: './lisans-yonetimi.scss',
    providers: [MessageService]
})
export class LisansYonetimi implements OnInit {
    private readonly service = inject(LisansYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    status: LicenseStatusDto | null = null;
    loading = false;
    uploading = false;

    get statusSeverity(): 'success' | 'danger' {
        return this.status?.isValid ? 'success' : 'danger';
    }

    get statusLabel(): string {
        return this.status?.isValid ? 'Gecerli' : 'Gecersiz';
    }

    get daysRemaining(): number | null {
        if (!this.status?.expiresAtUtc) return null;
        const expires = new Date(this.status.expiresAtUtc);
        const now = new Date();
        const diff = expires.getTime() - now.getTime();
        return Math.ceil(diff / (1000 * 60 * 60 * 24));
    }

    get expirySeverity(): 'success' | 'warn' | 'danger' {
        if (this.daysRemaining === null) return 'danger';
        if (this.daysRemaining <= 0) return 'danger';
        if (this.daysRemaining <= 30) return 'warn';
        return 'success';
    }

    ngOnInit(): void {
        this.loadStatus();
    }

    loadStatus(): void {
        this.loading = true;
        this.service
            .getStatus()
            .pipe(finalize(() => { this.loading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (data) => {
                    this.status = data;
                    this.cdr.detectChanges();
                },
                error: (err: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveError(err) });
                    this.cdr.detectChanges();
                }
            });
    }

    onFileSelect(event: { files: File[] }): void {
        const file = event.files?.[0];
        if (!file) return;

        if (!file.name.endsWith('.json')) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Uyari', detail: 'Sadece .json dosya yuklenebilir.' });
            return;
        }

        this.uploading = true;
        this.service
            .upload(file)
            .pipe(finalize(() => { this.uploading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (data) => {
                    this.status = data;
                    if (data.isValid) {
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Lisans basariyla yuklendi ve dogrulandi.' });
                    } else {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Gecersiz Lisans', detail: data.errors.join(', ') });
                    }
                    this.cdr.detectChanges();
                },
                error: (err: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Yukleme Hatasi', detail: this.resolveError(err) });
                    this.cdr.detectChanges();
                }
            });
    }

    private resolveError(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMsg = tryReadApiMessage(error.error);
            if (apiMsg) return apiMsg;
        }
        if (error instanceof Error && error.message.trim().length > 0) return error.message;
        return 'Beklenmeyen bir hata olustu.';
    }
}
