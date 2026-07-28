import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { AuthService } from '../../pages/auth';

@Component({
    selector: 'app-inactivity-warning-dialog',
    standalone: true,
    imports: [CommonModule, ButtonModule, DialogModule],
    template: `
        <p-dialog
            header="Oturumunuz Sonlanmak Uzere"
            [visible]="authService.showInactivityWarning()"
            [modal]="true"
            [closable]="false"
            [closeOnEscape]="false"
            [style]="{ width: '28rem', 'max-width': '95vw' }"
        >
            <div class="flex items-start gap-3">
                <i class="pi pi-exclamation-triangle text-3xl text-orange-500"></i>
                <div>
                    <p class="m-0 mb-2">
                        Uzun sureli hareketsizlik nedeniyle uygulamadan cikis yapilacak.
                    </p>
                    <p class="m-0 font-medium">
                        {{ remainingSecondsLabel() }} icinde otomatik olarak cikis yapilacaktir. Devam etmek icin ek sure ister misiniz?
                    </p>
                </div>
            </div>

            <ng-template #footer>
                <p-button label="Simdi cikis yap" icon="pi pi-sign-out" severity="secondary" text (onClick)="cikisYap()" />
                <p-button label="Evet, ek sure istiyorum" icon="pi pi-refresh" (onClick)="ekSureIste()" />
            </ng-template>
        </p-dialog>
    `
})
export class AppInactivityWarningDialog {
    readonly authService = inject(AuthService);

    remainingSecondsLabel(): string {
        const totalSeconds = this.authService.inactivityWarningSecondsRemaining();
        const minutes = Math.floor(totalSeconds / 60);
        const seconds = totalSeconds % 60;

        if (minutes <= 0) {
            return `${seconds} saniye`;
        }

        return `${minutes} dakika ${seconds.toString().padStart(2, '0')} saniye`;
    }

    ekSureIste(): void {
        this.authService.extendSession();
    }

    cikisYap(): void {
        this.authService.logout({ reason: 'inactivity' });
    }
}
