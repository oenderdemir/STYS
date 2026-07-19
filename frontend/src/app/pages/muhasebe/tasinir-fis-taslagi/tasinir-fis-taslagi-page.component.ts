import { Component, OnInit, effect, inject, ChangeDetectionStrategy } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { TasinirMuhasebeFisTaslagiDialogComponent } from './tasinir-muhasebe-fis-taslagi-dialog.component';

@Component({
    selector: 'app-tasinir-fis-taslagi-page',
    standalone: true,
    imports: [ButtonModule, ToastModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './tasinir-fis-taslagi-page.component.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    providers: [DialogService, MessageService]
})
export class TasinirFisTaslagiPageComponent implements OnInit {
    private readonly dialogService = inject(DialogService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private dialogRef: DynamicDialogRef | null = null;
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId && this.dialogRef) {
            this.dialogRef.close();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için açık taşınır fiş taslağı kapatıldı.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
            },
            error: () => {
                // Hata mesajı seçim dialogunda da gösterilebilir; burada sessiz kalıyoruz.
            }
        });
    }

    openDialog(): void {
        if (this.dialogRef) {
            return;
        }

        this.dialogRef = this.dialogService.open(TasinirMuhasebeFisTaslagiDialogComponent, {
            header: 'Taşınır İşleminden Muhasebe Fişi Oluştur',
            width: '900px',
            modal: true,
            closable: true,
            dismissableMask: false
        });

        this.dialogRef?.onClose.subscribe(() => {
            this.dialogRef = null;
        });
    }
}
