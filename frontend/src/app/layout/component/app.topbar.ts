import { ChangeDetectorRef, Component, effect, inject, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { Menu, MenuModule } from 'primeng/menu';
import { MultiSelectModule } from 'primeng/multiselect';
import { Popover, PopoverModule } from 'primeng/popover';
import { PasswordModule } from 'primeng/password';
import { Router, RouterModule } from '@angular/router';
import { SelectModule } from 'primeng/select';
import { CommonModule } from '@angular/common';
import { StyleClassModule } from 'primeng/styleclass';
import { ToastModule } from 'primeng/toast';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiErrorItem, tryReadApiMessage } from '../../core/api';
import { AppConfigurator } from './app.configurator';
import { LayoutService } from '@/app/layout/service/layout.service';
import { AuthService } from '../../pages/auth';
import { NotificationService } from '../../core/notifications/notification.service';
import { NotificationSeverityValues, NotificationViewModel } from '../../core/notifications/notification.model';
import { NotificationPreferenceDto } from '../../core/notifications/notification-preference.model';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';

@Component({
    selector: 'app-topbar',
    standalone: true,
    imports: [RouterModule, CommonModule, FormsModule, StyleClassModule, AppConfigurator, MenuModule, PopoverModule, DialogModule, PasswordModule, ButtonModule, ToastModule, CheckboxModule, MultiSelectModule, SelectModule],
    providers: [MessageService],
    styleUrl: './app.topbar.scss',
    template: ` <div class="layout-topbar">
            <p-toast position="bottom-right" [baseZIndex]="20000" />
        <div class="layout-topbar-logo-container">
            <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
                <i class="pi pi-bars"></i>
            </button>
            <a class="layout-topbar-logo" routerLink="/">

               <img src="logo.png" class="logo-img" style="width:100px;" />
                    <!-- <span>STYS</span> -->
            </a>
        </div>

        <div class="layout-topbar-actions">
            @if (currentUserName) {
                <div
                    class="hidden lg:flex"
                    style="align-items: center; gap: 0.45rem; padding: 0.4rem 0.75rem; border: 1px solid var(--surface-border); border-radius: 999px; background: var(--surface-card); margin-right: 0.5rem;"
                    [title]="currentUserName"
                >
                    <i class="pi pi-user" style="font-size: 0.9rem; color: var(--text-color-secondary);"></i>
                    <span style="font-weight: 600; max-width: 14rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">{{ currentUserName }}</span>
                </div>
            }

            <div class="layout-config-menu">
                <button type="button" class="layout-topbar-action" (click)="toggleDarkMode()">
                    <i [ngClass]="{ 'pi ': true, 'pi-moon': layoutService.isDarkTheme(), 'pi-sun': !layoutService.isDarkTheme() }"></i>
                </button>
                <div class="relative">
                    <button
                        class="layout-topbar-action layout-topbar-action-highlight"
                        pStyleClass="@next"
                        enterFromClass="hidden"
                        enterActiveClass="animate-scalein"
                        leaveToClass="hidden"
                        leaveActiveClass="animate-fadeout"
                        [hideOnOutsideClick]="true"
                    >
                        <i class="pi pi-palette"></i>
                    </button>
                    <app-configurator />
                </div>
            </div>

            <button class="layout-topbar-menu-button layout-topbar-action" pStyleClass="@next" enterFromClass="hidden" enterActiveClass="animate-scalein" leaveToClass="hidden" leaveActiveClass="animate-fadeout" [hideOnOutsideClick]="true">
                <i class="pi pi-ellipsis-v"></i>
            </button>

            <div class="layout-topbar-menu hidden lg:block">
                <div class="layout-topbar-menu-content">
                    <button type="button" class="layout-topbar-action">
                        <i class="pi pi-calendar"></i>
                        <span>Calendar</span>
                    </button>
                    <button type="button" class="layout-topbar-action relative" (click)="toggleNotificationPopover($event)">
                        <i
                            class="pi pi-envelope"
                            [ngClass]="{ 'topbar-notification-pulse': hasNewNotificationCue, 'topbar-envelope-unread': unreadCount() > 0 }"
                            [style.color]="unreadCount() > 0 ? '#22c55e' : null"
                        ></i>
                        @if (hasNewNotificationCue) {
                            <span class="topbar-notification-alert">!</span>
                        }
                        @if (unreadCount() > 0) {
                            <span class="topbar-unread-badge">
                                {{ unreadBadgeText() }}
                            </span>
                        }
                        <span>Bildirimler</span>
                    </button>
                    <p-popover #notificationPopover [appendTo]="'body'" [style]="{ width: '26rem', 'max-width': '92vw' }">
                        <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; margin-bottom: 0.75rem;">
                            <div style="display: flex; align-items: center; gap: 0.5rem;">
                                <i class="pi pi-bell"></i>
                                <span style="font-weight: 700;">Bildirimler</span>
                            </div>
                            <button
                                type="button"
                                class="p-button p-button-text p-button-sm"
                                (click)="markAllNotificationsAsRead()"
                                [disabled]="unreadCount() === 0"
                            >
                                Tumunu Okundu Yap
                            </button>
                        </div>

                        @if (notifications().length === 0) {
                            <div style="padding: 1.25rem 0.5rem; color: var(--text-color-secondary); text-align: center;">
                                Yeni bildirim bulunmuyor.
                            </div>
                        } @else {
                            <div style="max-height: 24rem; overflow-y: auto; display: flex; flex-direction: column; gap: 0.5rem;">
                                @for (item of notifications(); track item.id) {
                                    <button
                                        type="button"
                                        (click)="onNotificationClick(item)"
                                        [ngStyle]="notificationCardStyle(item)"
                                    >
                                        <div style="display: flex; align-items: start; justify-content: space-between; gap: 0.5rem;">
                                            <strong style="display: block; font-size: 0.9rem;">{{ item.baslik }}</strong>
                                            @if (!item.isRead) {
                                                <span style="display: inline-flex; width: 0.55rem; height: 0.55rem; border-radius: 999px; background: var(--primary-color); margin-top: 0.25rem;"></span>
                                            }
                                        </div>
                                        <div style="margin-top: 0.3rem; color: var(--text-color-secondary); font-size: 0.84rem; line-height: 1.25;">
                                            {{ item.mesaj }}
                                        </div>
                                        @if (item.kaynakUserAdi) {
                                            <div style="margin-top: 0.3rem; color: var(--text-color-secondary); font-size: 0.76rem;">
                                                Kaynak: {{ item.kaynakUserAdi }}
                                            </div>
                                        }
                                        <div style="margin-top: 0.45rem; color: var(--text-color-secondary); font-size: 0.76rem;">
                                            {{ formatNotificationDate(item.createdAt) }}
                                        </div>
                                    </button>
                                }
                            </div>
                        }
                    </p-popover>
                    <button type="button" class="layout-topbar-action" (click)="onProfileMenuToggle($event, profileMenu)" [title]="currentUserName || 'Profile'">
                        <i class="pi pi-user"></i>
                        <span>{{ currentUserName || 'Profile' }}</span>
                    </button>
                    <p-menu #profileMenu [popup]="true" [model]="profileMenuItems"></p-menu>
                </div>
            </div>
        </div>
    </div>

        <p-dialog
            header="Bildirim Tercihleri"
            [(visible)]="notificationPreferenceDialogVisible"
            [modal]="true"
            [style]="{ width: '36rem', 'max-width': '96vw' }"
            [breakpoints]="{ '960px': '96vw' }"
        >
            <div class="flex flex-col gap-4">
                <div class="flex items-center gap-2">
                    <p-checkbox
                        inputId="notificationActive"
                        [binary]="true"
                        [(ngModel)]="notificationPreferences.bildirimlerAktifMi"
                        [disabled]="isLoadingNotificationPreferences || isSavingNotificationPreferences"
                    />
                    <label for="notificationActive" class="font-medium">Bildirimleri Aktif Tut</label>
                </div>

                <div>
                    <label class="block font-medium mb-2">Minimum Severity</label>
                    <p-select
                        [options]="notificationSeverityOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="notificationPreferences.minimumSeverity"
                        [disabled]="isLoadingNotificationPreferences || isSavingNotificationPreferences || !notificationPreferences.bildirimlerAktifMi"
                        appendTo="body"
                        class="w-full"
                    />
                </div>

                <div>
                    <label class="block font-medium mb-2">Bildirim Tipleri (bos ise tumu)</label>
                    <p-multiselect
                        [options]="notificationPreferences.mevcutTipler"
                        [(ngModel)]="notificationPreferences.izinliTipler"
                        [filter]="true"
                        [showClear]="true"
                        [disabled]="isLoadingNotificationPreferences || isSavingNotificationPreferences || !notificationPreferences.bildirimlerAktifMi"
                        appendTo="body"
                        styleClass="w-full"
                        placeholder="Tum tipler"
                    />
                </div>

                <div>
                    <label class="block font-medium mb-2">Kaynak Kullanici (bos ise tumu)</label>
                    <p-multiselect
                        [options]="notificationPreferences.mevcutKaynaklar"
                        [(ngModel)]="notificationPreferences.izinliKaynaklar"
                        [filter]="true"
                        [showClear]="true"
                        [disabled]="isLoadingNotificationPreferences || isSavingNotificationPreferences || !notificationPreferences.bildirimlerAktifMi"
                        appendTo="body"
                        styleClass="w-full"
                        placeholder="Tum kaynaklar"
                    />
                </div>

                @if (notificationPreferencesError) {
                    <small class="text-red-500">{{ notificationPreferencesError }}</small>
                }
            </div>

            <ng-template #footer>
                <p-button label="Kapat" icon="pi pi-times" severity="secondary" text [disabled]="isSavingNotificationPreferences" (onClick)="notificationPreferenceDialogVisible = false" />
                <p-button
                    [label]="isSavingNotificationPreferences ? 'Kaydediliyor...' : 'Kaydet'"
                    icon="pi pi-check"
                    [disabled]="isLoadingNotificationPreferences || isSavingNotificationPreferences"
                    (onClick)="saveNotificationPreferences()"
                />
            </ng-template>
        </p-dialog>

		    <p-dialog
	        [header]="isForceChangePasswordMode ? 'Zorunlu Sifre Degistirme' : 'Sifre Degistir'"
	        [(visible)]="changePasswordDialogVisible"
        [modal]="true"
        [closable]="!isForceChangePasswordMode"
        [closeOnEscape]="!isForceChangePasswordMode"
        [dismissableMask]="!isForceChangePasswordMode"
        [style]="{ width: '28rem', 'max-width': '95vw' }"
        [breakpoints]="{ '960px': '95vw' }"
    >
        <div class="flex flex-col gap-4">
            <div>
                <label for="currentPassword" class="block font-medium mb-2">Mevcut Sifre</label>
                <p-password id="currentPassword" [(ngModel)]="currentPassword" [feedback]="false" [toggleMask]="true" [fluid]="true" [disabled]="isChangingPassword"></p-password>
            </div>

            <div>
                <label for="newPassword" class="block font-medium mb-2">Yeni Sifre</label>
                <p-password id="newPassword" [(ngModel)]="newPassword" [feedback]="false" [toggleMask]="true" [fluid]="true" [disabled]="isChangingPassword"></p-password>
            </div>

            <div>
                <label for="newPasswordAgain" class="block font-medium mb-2">Yeni Sifre (Tekrar)</label>
                <p-password id="newPasswordAgain" [(ngModel)]="newPasswordAgain" [feedback]="false" [toggleMask]="true" [fluid]="true" [disabled]="isChangingPassword"></p-password>
            </div>

            @if (changePasswordError) {
                <small class="text-red-500">{{ changePasswordError }}</small>
            }
        </div>

		        <ng-template #footer>
                    @if (!isForceChangePasswordMode) {
		                <p-button label="Iptal" icon="pi pi-times" severity="secondary" text [disabled]="isChangingPassword" (onClick)="changePasswordDialogVisible = false" />
                    }
		            <p-button
		                [label]="isChangingPassword ? 'Degistiriliyor...' : 'Degistir'"
		                icon="pi pi-check"
	                [disabled]="isChangingPassword || !currentPassword || !newPassword || !newPasswordAgain"
	                (onClick)="submitChangePassword()"
	            />
	        </ng-template>
	    </p-dialog>

        <p-dialog
            header="Sifre Degistirme Hatasi"
            [(visible)]="changePasswordErrorDialogVisible"
            [modal]="true"
            [style]="{ width: '30rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '95vw' }"
        >
            <div class="flex flex-col gap-3">
                <span>Islem tamamlanamadi. Lutfen hata detaylarini kontrol edin.</span>
                <ul class="m-0 pl-5">
                    @for (detail of changePasswordErrorDetails; track $index) {
                        <li>{{ detail }}</li>
                    }
                </ul>
            </div>

            <ng-template #footer>
                <p-button label="Kapat" icon="pi pi-times" severity="secondary" text (onClick)="changePasswordErrorDialogVisible = false" />
            </ng-template>
        </p-dialog>`
})
export class AppTopbar {
    layoutService = inject(LayoutService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly notificationService = inject(NotificationService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    @ViewChild('profileMenu') private profileMenu?: Menu;
    @ViewChild('notificationPopover') private notificationPopover?: Popover;

    changePasswordDialogVisible = false;
    changePasswordErrorDialogVisible = false;
    isChangingPassword = false;
    currentPassword = '';
    newPassword = '';
    newPasswordAgain = '';
    isForceChangePasswordMode = false;
    changePasswordError: string | null = null;
    changePasswordErrorDetails: string[] = [];
    currentUserName: string | null = null;
    notificationPreferenceDialogVisible = false;
    isLoadingNotificationPreferences = false;
    isSavingNotificationPreferences = false;
    notificationPreferencesError: string | null = null;
    notificationPreferences: NotificationPreferenceDto = {
        bildirimlerAktifMi: true,
        minimumSeverity: NotificationSeverityValues.Info,
        izinliTipler: [],
        izinliKaynaklar: [],
        mevcutTipler: [],
        mevcutKaynaklar: []
    };
    readonly notificationSeverityOptions = [
        { label: 'Bilgi', value: NotificationSeverityValues.Info },
        { label: 'Basarili', value: NotificationSeverityValues.Success },
        { label: 'Uyari', value: NotificationSeverityValues.Warn },
        { label: 'Hata', value: NotificationSeverityValues.Error },
        { label: 'Kritik', value: NotificationSeverityValues.Danger }
    ];
    readonly notifications = this.notificationService.notifications;
    readonly unreadCount = this.notificationService.unreadCount;
    readonly lastRealtimeNotification = this.notificationService.lastRealtimeNotification;
    hasNewNotificationCue = false;
    private lastRealtimeNotificationId: number | null = null;

    readonly profileMenuItems: MenuItem[] = [
        {
            label: 'Bildirim Tercihleri',
            icon: 'pi pi-sliders-h',
            command: () => this.openNotificationPreferencesDialog()
        },
        {
            label: 'Sifre Degistir',
            icon: 'pi pi-key',
            command: () => this.openChangePasswordDialog()
        },
        {
            separator: true
        },
        {
            label: 'Cikis',
            icon: 'pi pi-sign-out',
            command: () => this.handleLogout()
        }
    ];

    constructor() {
        effect(() => {
            this.authService.sessionRevision();
            this.currentUserName = this.authService.getCurrentUserName();

            if (!this.authService.isAuthenticated()) {
                this.currentUserName = null;
                this.hasNewNotificationCue = false;
                this.lastRealtimeNotificationId = null;
                this.closeAllOverlays();
                return;
            }

            if (this.authService.mustChangePassword()) {
                if (!this.changePasswordDialogVisible || !this.isForceChangePasswordMode) {
                    this.openForcedChangePasswordDialog();
                }
                return;
            }

            if (this.isForceChangePasswordMode) {
                this.isForceChangePasswordMode = false;
                this.cdr.detectChanges();
            }
        });

        effect(() => {
            const incoming = this.lastRealtimeNotification();
            if (!incoming || !this.authService.isAuthenticated()) {
                return;
            }

            if (this.lastRealtimeNotificationId === incoming.id) {
                return;
            }

            this.lastRealtimeNotificationId = incoming.id;
            this.hasNewNotificationCue = true;
            this.messageService.add({
                severity: this.mapToastSeverity(incoming.severity),
                summary: incoming.baslik || 'Yeni Bildirim',
                detail: incoming.mesaj,
                life: 4500
            });
        });
    }

    unreadBadgeText(): string {
        const value = this.unreadCount();
        return value > 99 ? '99+' : value.toString();
    }

    toggleNotificationPopover(event: Event): void {
        this.hasNewNotificationCue = false;
        void this.notificationService.reload();
        this.notificationPopover?.toggle(event);
    }

    onNotificationClick(item: NotificationViewModel): void {
        if (!item.isRead) {
            this.notificationService.markAsRead(item.id).subscribe({
                next: () => this.notificationService.applyRead(item.id),
                error: () => {
                    // No-op
                }
            });
        }

        if (item.link && item.link.trim().length > 0) {
            this.profileMenu?.hide();
            this.notificationPopover?.hide();
            void this.router.navigateByUrl(item.link);
        }
    }

    markAllNotificationsAsRead(): void {
        this.notificationService.markAllAsRead().subscribe({
            next: () => this.notificationService.applyReadAll(),
            error: () => {
                // No-op
            }
        });
    }

    openNotificationPreferencesDialog(): void {
        this.profileMenu?.hide();
        this.notificationPreferencesError = null;
        this.notificationPreferenceDialogVisible = true;
        this.loadNotificationPreferences();
    }

    saveNotificationPreferences(): void {
        if (this.isSavingNotificationPreferences) {
            return;
        }

        this.notificationPreferencesError = null;
        this.isSavingNotificationPreferences = true;

        this.notificationService
            .updatePreferences({
                bildirimlerAktifMi: this.notificationPreferences.bildirimlerAktifMi,
                minimumSeverity: this.notificationPreferences.minimumSeverity,
                izinliTipler: [...(this.notificationPreferences.izinliTipler ?? [])],
                izinliKaynaklar: [...(this.notificationPreferences.izinliKaynaklar ?? [])]
            })
            .pipe(
                finalize(() => {
                    this.isSavingNotificationPreferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.notificationPreferences = this.normalizeNotificationPreferences(result);
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: 'Bildirim tercihleri guncellendi.'
                    });
                },
                error: (error: unknown) => {
                    this.notificationPreferencesError = this.resolveErrorMessage(error);
                }
            });
    }

    notificationBorderColor(severity: string): string {
        const normalized = (severity ?? '').trim().toLowerCase();
        switch (normalized) {
            case NotificationSeverityValues.Success:
                return 'var(--green-500)';
            case NotificationSeverityValues.Warn:
                return 'var(--orange-500)';
            case NotificationSeverityValues.Error:
            case NotificationSeverityValues.Danger:
                return 'var(--red-500)';
            default:
                return 'var(--blue-500)';
        }
    }

    private mapToastSeverity(severity: string): 'success' | 'info' | 'warn' | 'error' {
        const normalized = (severity ?? '').trim().toLowerCase();
        switch (normalized) {
            case NotificationSeverityValues.Success:
                return NotificationSeverityValues.Success;
            case NotificationSeverityValues.Warn:
            case 'warning':
                return NotificationSeverityValues.Warn;
            case NotificationSeverityValues.Error:
            case NotificationSeverityValues.Danger:
                return NotificationSeverityValues.Error;
            default:
                return NotificationSeverityValues.Info;
        }
    }

    private loadNotificationPreferences(): void {
        this.isLoadingNotificationPreferences = true;
        this.notificationService
            .getPreferences()
            .pipe(
                finalize(() => {
                    this.isLoadingNotificationPreferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.notificationPreferences = this.normalizeNotificationPreferences(result);
                },
                error: (error: unknown) => {
                    this.notificationPreferencesError = this.resolveErrorMessage(error);
                }
            });
    }

    private normalizeNotificationPreferences(value: NotificationPreferenceDto): NotificationPreferenceDto {
        const mevcutTipler = [...new Set((value.mevcutTipler ?? []).map((x) => x?.trim()).filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'tr'));
        const mevcutKaynaklar = [...new Set((value.mevcutKaynaklar ?? []).map((x) => x?.trim()).filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'tr'));
        const izinliTipler = [...new Set((value.izinliTipler ?? []).map((x) => x?.trim()).filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'tr'));
        const izinliKaynaklar = [...new Set((value.izinliKaynaklar ?? []).map((x) => x?.trim()).filter((x): x is string => !!x))].sort((a, b) => a.localeCompare(b, 'tr'));

        return {
            bildirimlerAktifMi: value.bildirimlerAktifMi ?? true,
            minimumSeverity: value.minimumSeverity ?? NotificationSeverityValues.Info,
            mevcutTipler,
            mevcutKaynaklar,
            izinliTipler,
            izinliKaynaklar
        };
    }

    notificationCardStyle(item: NotificationViewModel): Record<string, string> {
        return {
            textAlign: 'left',
            border: '1px solid var(--surface-border)',
            borderLeft: '4px solid',
            borderLeftColor: this.notificationBorderColor(item.severity),
            background: item.isRead ? 'var(--surface-ground)' : 'var(--surface-card)',
            borderRadius: '0.65rem',
            padding: '0.65rem 0.75rem',
            cursor: 'pointer',
            width: '100%'
        };
    }

    formatNotificationDate(value: Date): string {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
            return '';
        }

        return value.toLocaleString('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    toggleDarkMode() {
        this.layoutService.layoutConfig.update((state) => ({
            ...state,
            darkTheme: !state.darkTheme
        }));
    }

    private openChangePasswordDialog(): void {
        this.profileMenu?.hide();
        this.isForceChangePasswordMode = false;
        this.currentPassword = '';
        this.newPassword = '';
        this.newPasswordAgain = '';
        this.changePasswordError = null;
        this.changePasswordErrorDialogVisible = false;
        this.changePasswordErrorDetails = [];
        this.changePasswordDialogVisible = true;
        this.cdr.detectChanges();
    }

    private handleLogout(): void {
        this.closeAllOverlays();
        this.authService.logout({ preserveReturnUrl: false });
    }

    onProfileMenuToggle(event: Event, menu: Menu): void {
        menu.toggle(event);
    }

    submitChangePassword(): void {
        if (this.isChangingPassword) {
            return;
        }

        if (this.newPassword !== this.newPasswordAgain) {
            this.changePasswordError = 'Yeni sifreler ayni degil.';
            this.cdr.detectChanges();
            return;
        }

        this.changePasswordError = null;
        this.isChangingPassword = true;

        this.authService
            .changePassword(this.currentPassword, this.newPassword, this.newPasswordAgain)
            .pipe(
                finalize(() => {
                    this.isChangingPassword = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.isForceChangePasswordMode = false;
                    this.changePasswordDialogVisible = false;
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: 'Sifre degistirildi. Lutfen tekrar giris yapin.'
                    });
                    this.authService.logout({ preserveReturnUrl: false });
                },
                error: (error: unknown) => {
                    this.openChangePasswordErrorDialog(error);
                    this.cdr.detectChanges();
                }
            });
    }

    private openChangePasswordErrorDialog(error: unknown): void {
        this.changePasswordError = null;
        this.changePasswordErrorDetails = this.resolveErrorDetails(error);
        this.changePasswordErrorDialogVisible = true;
    }

    private openForcedChangePasswordDialog(): void {
        this.profileMenu?.hide();
        this.currentPassword = '';
        this.newPassword = '';
        this.newPasswordAgain = '';
        this.changePasswordError = null;
        this.changePasswordErrorDialogVisible = false;
        this.changePasswordErrorDetails = [];
        this.isForceChangePasswordMode = true;
        this.changePasswordDialogVisible = true;
        this.cdr.detectChanges();
    }

    private closeAllOverlays(): void {
        this.profileMenu?.hide();
        this.notificationPopover?.hide();
        this.changePasswordDialogVisible = false;
        this.changePasswordErrorDialogVisible = false;
        this.changePasswordErrorDetails = [];
        this.isForceChangePasswordMode = false;
        this.cdr.detectChanges();
    }

    private resolveErrorDetails(error: unknown): string[] {
        if (error instanceof HttpErrorResponse) {
            const responseDetails = this.tryReadApiDetails(error.error);
            if (responseDetails.length > 0) {
                return responseDetails;
            }

            const statusMessage = tryReadApiMessage(error.error);
            if (statusMessage) {
                return [statusMessage];
            }
        }

        const directDetails = this.tryReadApiDetails(error);
        if (directDetails.length > 0) {
            return directDetails;
        }

        const fallbackMessage = this.resolveErrorMessage(error);
        return [fallbackMessage];
    }

    private tryReadApiDetails(payload: unknown): string[] {
        if (!isRecord(payload)) {
            return [];
        }

        const details: string[] = [];
        const message = payload['message'];
        if (typeof message === 'string' && message.trim().length > 0) {
            details.push(message.trim());
        }

        const errors = payload['errors'];
        if (Array.isArray(errors)) {
            for (const errorItem of errors) {
                if (!isRecord(errorItem)) {
                    continue;
                }

                const typedItem = errorItem as Partial<ApiErrorItem>;
                if (typeof typedItem.detail === 'string' && typedItem.detail.trim().length > 0) {
                    details.push(typedItem.detail.trim());
                }
            }
        }

        return [...new Set(details)];
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        const apiMessage = tryReadApiMessage(error);
        if (apiMessage) {
            return apiMessage;
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Sifre degistirme islemi basarisiz oldu.';
    }
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
}
