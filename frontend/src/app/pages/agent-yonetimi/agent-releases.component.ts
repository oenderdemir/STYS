import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { RippleModule } from 'primeng/ripple';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToolbarModule } from 'primeng/toolbar';
import { finalize } from 'rxjs';
import { AgentReleaseDto, AgentReleasePublishForm } from './agent-yonetimi.dto';
import { AgentYonetimiService } from './agent-yonetimi.service';

@Component({
    selector: 'app-agent-releases',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DialogModule,
        InputTextModule,
        RippleModule,
        SelectModule,
        TableModule,
        TagModule,
        TextareaModule,
        ToolbarModule
    ],
    templateUrl: './agent-releases.component.html'
})
export class AgentReleasesComponent implements OnInit {
    private readonly service = inject(AgentYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    /** Only win-x64 is publishable in this phase; the backend rejects anything else. */
    readonly runtimeOptions = [{ label: 'Windows x64', value: 'win-x64' }];

    releases = signal<AgentReleaseDto[]>([]);
    loading = signal(false);
    publishing = signal(false);
    togglingId = signal<number | null>(null);
    dialogVisible = signal(false);
    selectedFile = signal<File | null>(null);
    submitted = signal(false);

    form: AgentReleasePublishForm = this.createEmptyForm();

    ngOnInit(): void {
        this.loadReleases();
    }

    loadReleases(): void {
        this.loading.set(true);
        this.service
            .getReleases()
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: (releases) => this.releases.set(releases),
                error: (err) => this.showError(err)
            });
    }

    openPublishDialog(): void {
        this.form = this.createEmptyForm();
        this.selectedFile.set(null);
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    onFileSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        this.selectedFile.set(input.files?.length ? input.files[0] : null);
    }

    canPublish(): boolean {
        return (
            this.form.version.trim().length > 0 &&
            this.form.contractVersion.trim().length > 0 &&
            this.form.runtimeIdentifier.trim().length > 0 &&
            this.selectedFile() !== null
        );
    }

    publish(): void {
        this.submitted.set(true);

        const file = this.selectedFile();
        if (!this.canPublish() || !file) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Sürüm, contract sürümü ve paket dosyası zorunludur.' });
            return;
        }

        this.publishing.set(true);
        this.service
            .publishRelease(this.form, file)
            .pipe(finalize(() => this.publishing.set(false)))
            .subscribe({
                next: (release) => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Sürüm Yayınlandı',
                        detail: `${release.version} (${release.runtimeIdentifier}) imzalandı ve kaydedildi.`
                    });
                    this.dialogVisible.set(false);
                    this.loadReleases();
                },
                error: (err) => this.showError(err)
            });
    }

    toggleEnabled(release: AgentReleaseDto): void {
        const enabling = !release.enabled;
        const message = enabling
            ? `${release.version} sürümü aktifleştirilsin mi? Uygun agent'lar bu sürüme yükseltilebilir hale gelir.`
            : `${release.version} sürümü pasifleştirilsin mi? Yeni güncelleme hazırlama işlemlerinde seçilmez.`;

        this.confirmationService.confirm({
            message,
            header: enabling ? 'Sürümü Aktifleştir' : 'Sürümü Pasifleştir',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.togglingId.set(release.id);
                this.service
                    .setReleaseEnabled(release.id, enabling)
                    .pipe(finalize(() => this.togglingId.set(null)))
                    .subscribe({
                        next: () => {
                            this.messageService.add({
                                severity: 'success',
                                summary: 'Güncellendi',
                                detail: `${release.version} ${enabling ? 'aktifleştirildi' : 'pasifleştirildi'}.`
                            });
                            this.loadReleases();
                        },
                        error: (err) => this.showError(err)
                    });
            }
        });
    }

    formatSize(bytes: number): string {
        if (!bytes || bytes <= 0) {
            return '-';
        }

        const units = ['B', 'KB', 'MB', 'GB'];
        let value = bytes;
        let unit = 0;
        while (value >= 1024 && unit < units.length - 1) {
            value /= 1024;
            unit++;
        }

        return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
    }

    /** Hashes are long; the leading bytes are enough to eyeball against the build output. */
    shortHash(sha256: string): string {
        return sha256 ? `${sha256.substring(0, 12)}…` : '-';
    }

    private createEmptyForm(): AgentReleasePublishForm {
        return {
            version: '',
            contractVersion: '',
            runtimeIdentifier: 'win-x64',
            releaseNotes: '',
            enabled: true
        };
    }

    private showError(err: unknown): void {
        const detail = err instanceof Error ? err.message : 'Beklenmeyen bir hata oluştu.';
        this.messageService.add({ severity: 'error', summary: 'Hata', detail });
    }
}
