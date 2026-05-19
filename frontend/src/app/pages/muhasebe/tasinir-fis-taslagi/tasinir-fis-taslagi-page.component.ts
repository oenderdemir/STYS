import { Component, inject } from '@angular/core';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TasinirMuhasebeFisTaslagiDialogComponent } from './tasinir-muhasebe-fis-taslagi-dialog.component';

@Component({
    selector: 'app-tasinir-fis-taslagi-page',
    standalone: true,
    imports: [
        ButtonModule,
        ToastModule
    ],
    templateUrl: './tasinir-fis-taslagi-page.component.html',
    providers: [DialogService, MessageService]
})
export class TasinirFisTaslagiPageComponent {
    private readonly dialogService = inject(DialogService);
    private dialogRef: DynamicDialogRef | null = null;

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
