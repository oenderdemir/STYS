import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { InputTextModule } from 'primeng/inputtext';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { KullaniciYonetimiService } from '../kullanici-yonetimi/kullanici-yonetimi.service';
import { UserResponseDto } from '../kullanici-yonetimi/dto';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { KurumModel } from './kurum.model';
import { KurumService } from './kurum.service';
import { CreateKurumRequest, UpdateKurumRequest } from './kurum.request';
import { KurumKullaniciService } from './kurum-kullanici.service';
import { AssignUserKurumRequest, UpdateUserKurumRequest } from './user-kurum.request';
import { UserKurumModel } from './user-kurum.model';

interface KurumFormState extends UpdateKurumRequest {
    id?: number | null;
}

interface AssignmentFormState extends UpdateUserKurumRequest {
    id?: string | null;
    userId: string | null;
}

interface UserOption {
    label: string;
    value: string;
}

@Component({
    selector: 'app-kurum-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DialogModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        InputTextModule
    ],
    templateUrl: './kurum-yonetimi.html',
    styleUrl: './kurum-yonetimi.scss',
    providers: [MessageService, ConfirmationService]
})
export class KurumYonetimi implements OnInit {
    private readonly kurumService = inject(KurumService);
    private readonly kurumKullaniciService = inject(KurumKullaniciService);
    private readonly kullaniciYonetimiService = inject(KullaniciYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    kurumlar: KurumModel[] = [];
    selectedKurum: KurumFormState = this.createEmptyKurum();
    selectedKurumIsNew = true;
    selectedKurumUsers: UserKurumModel[] = [];
    userOptions: UserOption[] = [];
    users: UserResponseDto[] = [];
    loading = false;
    kurumSaving = false;
    kurumUserLoading = false;
    kurumUserSaving = false;
    assignmentDialogVisible = false;
    assignmentDialogMode: 'create' | 'edit' = 'create';
    activeTabIndex = 0;
    activeTabValue = '0';
    assignmentDraft: AssignmentFormState = this.createEmptyAssignment();

    readonly canViewKurumManagement = this.authService.hasPermission('UserManagement.Manage') || this.authService.isSuperAdminUser();

    get canManageSelectedKurum(): boolean {
        if (this.authService.isSuperAdminUser()) {
            return true;
        }

        const activeKurumId = this.authService.getAktifKurumId();
        if (!this.selectedKurum.id || activeKurumId === null) {
            return false;
        }

        return activeKurumId === this.selectedKurum.id && this.authService.isKurumAdminFor(this.selectedKurum.id);
    }

    get canManageKurumList(): boolean {
        return this.canViewKurumManagement;
    }

    get currentKurumAdmins(): UserKurumModel[] {
        return this.selectedKurumUsers.filter((item) => item.isKurumAdmin);
    }

    ngOnInit(): void {
        if (!this.canViewKurumManagement) {
            return;
        }

        this.loadPageData(this.authService.getAktifKurumId(), false);
    }

    refresh(): void {
        this.loadPageData(this.selectedKurumIsNew ? this.authService.getAktifKurumId() : this.selectedKurum.id ?? this.authService.getAktifKurumId(), this.selectedKurumIsNew);
    }

    onTabChange(value: string | number | undefined): void {
        this.activeTabValue = value === null || value === undefined ? '0' : value.toString();
    }

    openNewKurum(): void {
        if (!this.canManageKurumList) {
            return;
        }

        this.selectedKurum = this.createEmptyKurum();
        this.selectedKurumIsNew = true;
        this.selectedKurumUsers = [];
        this.assignmentDialogVisible = false;
        this.activeTabIndex = 0;
        this.activeTabValue = '0';
    }

    selectKurum(kurum: KurumModel): void {
        this.selectedKurum = this.cloneKurum(kurum);
        this.selectedKurumIsNew = false;
        this.activeTabIndex = 0;
        this.activeTabValue = '0';
        this.loadKurumUsers();
    }

    isSelectedKurum(kurum: KurumModel): boolean {
        return this.selectedKurum.id !== null && this.selectedKurum.id !== undefined && this.selectedKurum.id === kurum.id;
    }

    saveKurum(): void {
        if (!this.canManageKurumList || this.kurumSaving) {
            return;
        }

        const payload = this.normalizeKurumRequest(this.selectedKurum);
        if (!payload.kod) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kurum kodu zorunludur.' });
            return;
        }

        if (!payload.ad) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kurum adi zorunludur.' });
            return;
        }

        this.kurumSaving = true;
        const request$: Observable<KurumModel> = this.selectedKurumIsNew || !this.selectedKurum.id
            ? this.kurumService.create(payload)
            : this.kurumService.update(this.selectedKurum.id, payload);

        request$
            .pipe(
                finalize(() => {
                    this.kurumSaving = false;
                })
            )
            .subscribe({
                next: (saved) => {
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: this.selectedKurumIsNew ? 'Kurum olusturuldu.' : 'Kurum guncellendi.'
                    });
                    this.selectedKurumIsNew = false;
                    const preferredKurumId = saved.id ?? this.selectedKurum.id ?? this.authService.getAktifKurumId() ?? null;
                    this.loadPageData(preferredKurumId, false);
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    deleteKurum(kurum: KurumModel | null = null): void {
        const target = kurum ?? (this.selectedKurum.id ? this.selectedKurum as KurumModel : null);
        if (!this.canManageKurumList || !target?.id) {
            return;
        }

        const kurumId = target.id;
        const wasSelected = this.selectedKurum.id === kurumId;
        this.confirmationService.confirm({
            message: `"${target.ad}" kurumunu silmek istiyor musunuz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.kurumService.delete(kurumId).subscribe({
                    next: () => {
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kurum silindi.' });
                        if (wasSelected) {
                            this.openNewKurum();
                        }

                        const preferredKurumId = wasSelected ? (this.authService.getAktifKurumId() ?? null) : (this.selectedKurum.id ?? null);
                        this.loadPageData(preferredKurumId, wasSelected);
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    }
                });
            }
        });
    }

    openAssignDialog(): void {
        if (!this.canManageSelectedKurum || !this.selectedKurum.id) {
            return;
        }

        this.assignmentDialogMode = 'create';
        this.assignmentDraft = this.createEmptyAssignment();
        this.assignmentDialogVisible = true;
    }

    openEditAssignment(item: UserKurumModel): void {
        if (!this.canManageSelectedKurum || !item.id) {
            return;
        }

        this.assignmentDialogMode = 'edit';
        this.assignmentDraft = {
            id: item.id,
            userId: item.userId,
            varsayilanMi: item.varsayilanMi,
            aktifMi: item.aktifMi,
            isKurumAdmin: item.isKurumAdmin
        };
        this.assignmentDialogVisible = true;
    }

    saveAssignment(): void {
        if (!this.canManageSelectedKurum || this.kurumUserSaving || !this.selectedKurum.id) {
            return;
        }

        const request = this.normalizeAssignmentDraft();
        if (this.assignmentDialogMode === 'create') {
            if (!request.userId) {
                this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kullanici secimi zorunludur.' });
                return;
            }

            const assignPayload: AssignUserKurumRequest = {
                userId: request.userId,
                kurumId: this.selectedKurum.id,
                varsayilanMi: request.varsayilanMi,
                aktifMi: request.aktifMi,
                isKurumAdmin: request.isKurumAdmin
            };

            this.kurumUserSaving = true;
            this.kurumKullaniciService.assign(assignPayload)
                .pipe(finalize(() => (this.kurumUserSaving = false)))
                .subscribe({
                    next: () => {
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kullanici kurum atamasi yapildi.' });
                        this.assignmentDialogVisible = false;
                        this.loadKurumUsers();
                    },
                    error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
                });
            return;
        }

        if (!request.id) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Atama kaydi bulunamadi.' });
            return;
        }

        const updatePayload: UpdateUserKurumRequest = {
            varsayilanMi: request.varsayilanMi,
            aktifMi: request.aktifMi,
            isKurumAdmin: request.isKurumAdmin
        };

        this.kurumUserSaving = true;
        this.kurumKullaniciService.update(request.id, updatePayload)
            .pipe(finalize(() => (this.kurumUserSaving = false)))
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kullanici kurum atamasi guncellendi.' });
                    this.assignmentDialogVisible = false;
                    this.loadKurumUsers();
                },
                error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
            });
    }

    deleteAssignment(item: UserKurumModel): void {
        if (!this.canManageSelectedKurum || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Bu kullanici-kurum atamasini silmek istiyor musunuz?',
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.kurumKullaniciService.delete(item.id!).subscribe({
                    next: () => {
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Atama silindi.' });
                        this.loadKurumUsers();
                    },
                    error: (error: unknown) => this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) })
                });
            }
        });
    }

    getKurumName(kurumId: number): string {
        const kurum = this.kurumlar.find((item) => item.id === kurumId);
        return kurum?.ad?.trim().length ? kurum.ad.trim() : `Kurum #${kurumId}`;
    }

    getUserLabel(userId: string): string {
        const user = this.users.find((item) => item.id === userId);
        if (!user) {
            return userId;
        }

        const nameParts = [user.firstName, user.lastName].map((part) => part?.trim()).filter((part): part is string => !!part && part.length > 0);
        const fullName = nameParts.join(' ').trim();
        if (fullName.length > 0) {
            return `${user.userName}${fullName ? ` (${fullName})` : ''}`;
        }

        return user.userName;
    }

    getAssignmentCountForTab(): number {
        return this.selectedKurumUsers.length;
    }

    private loadPageData(preferredKurumId: number | null, keepNewDraft: boolean): void {
        this.loading = true;
        forkJoin({
            kurumlar: this.kurumService.getAll(),
            users: this.kullaniciYonetimiService.getUsers()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                })
            )
            .subscribe({
                next: ({ kurumlar, users }) => {
                    this.kurumlar = [...(kurumlar ?? [])].sort((left, right) => left.ad.localeCompare(right.ad, 'tr'));
                    this.users = [...(users ?? [])].sort((left, right) => (left.userName ?? '').localeCompare(right.userName ?? '', 'tr'));
                    this.userOptions = this.users.map((user) => ({
                        value: user.id ?? '',
                        label: this.buildUserOptionLabel(user)
                    }));

                    if (keepNewDraft && this.selectedKurumIsNew) {
                        this.selectedKurumUsers = [];
                        return;
                    }

                    const matched = this.resolvePreferredKurum(preferredKurumId);
                    if (matched) {
                        this.selectedKurum = this.cloneKurum(matched);
                        this.selectedKurumIsNew = false;
                        this.loadKurumUsers();
                        return;
                    }

                    if (this.kurumlar.length > 0) {
                        this.selectKurum(this.kurumlar[0]);
                        return;
                    }

                    this.openNewKurum();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    loadKurumUsers(): void {
        if (!this.selectedKurum.id) {
            this.selectedKurumUsers = [];
            return;
        }

        this.kurumUserLoading = true;
        this.kurumKullaniciService.getByKurum(this.selectedKurum.id)
            .pipe(
                finalize(() => {
                    this.kurumUserLoading = false;
                })
            )
            .subscribe({
                next: (items) => {
                    this.selectedKurumUsers = [...(items ?? [])].sort((left, right) => {
                        if (left.isKurumAdmin !== right.isKurumAdmin) {
                            return left.isKurumAdmin ? -1 : 1;
                        }

                        if (left.varsayilanMi !== right.varsayilanMi) {
                            return left.varsayilanMi ? -1 : 1;
                        }

                        return this.getUserLabel(left.userId).localeCompare(this.getUserLabel(right.userId), 'tr');
                    });
                },
                error: (error: unknown) => {
                    this.selectedKurumUsers = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private resolvePreferredKurum(preferredKurumId: number | null): KurumModel | null {
        if (preferredKurumId !== null && preferredKurumId !== undefined) {
            const matchedPreferred = this.kurumlar.find((item) => item.id === preferredKurumId);
            if (matchedPreferred) {
                return matchedPreferred;
            }
        }

        const activeKurumId = this.authService.getAktifKurumId();
        if (activeKurumId !== null && activeKurumId !== undefined) {
            const matchedActive = this.kurumlar.find((item) => item.id === activeKurumId);
            if (matchedActive) {
                return matchedActive;
            }
        }

        return null;
    }

    private buildUserOptionLabel(user: UserResponseDto): string {
        const fullName = [user.firstName, user.lastName]
            .map((part) => part?.trim())
            .filter((part): part is string => !!part && part.length > 0)
            .join(' ')
            .trim();

        if (fullName.length > 0) {
            return `${user.userName}${fullName ? ` - ${fullName}` : ''}`;
        }

        return user.userName;
    }

    private cloneKurum(kurum: KurumModel): KurumFormState {
        return {
            id: kurum.id,
            kod: kurum.kod ?? '',
            ad: kurum.ad ?? '',
            vergiNo: kurum.vergiNo ?? null,
            telefon: kurum.telefon ?? null,
            eposta: kurum.eposta ?? null,
            aktifMi: kurum.aktifMi
        };
    }

    private createEmptyKurum(): KurumFormState {
        return {
            id: null,
            kod: '',
            ad: '',
            vergiNo: null,
            telefon: null,
            eposta: null,
            aktifMi: true
        };
    }

    private normalizeKurumRequest(form: KurumFormState): CreateKurumRequest {
        return {
            kod: (form.kod ?? '').trim(),
            ad: (form.ad ?? '').trim(),
            vergiNo: this.normalizeOptionalText(form.vergiNo),
            telefon: this.normalizeOptionalText(form.telefon),
            eposta: this.normalizeOptionalText(form.eposta),
            aktifMi: form.aktifMi === true
        };
    }

    private createEmptyAssignment(): AssignmentFormState {
        return {
            id: null,
            userId: null,
            varsayilanMi: false,
            aktifMi: true,
            isKurumAdmin: false
        };
    }

    private normalizeAssignmentDraft(): AssignmentFormState {
        return {
            id: this.assignmentDraft.id ?? null,
            userId: this.assignmentDraft.userId?.trim() ?? null,
            varsayilanMi: this.assignmentDraft.varsayilanMi === true,
            aktifMi: this.assignmentDraft.aktifMi === true,
            isKurumAdmin: this.assignmentDraft.isKurumAdmin === true
        };
    }

    private normalizeOptionalText(value: string | null | undefined): string | null {
        if (!value || value.trim().length === 0) {
            return null;
        }

        return value.trim();
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const response = error.error;
            const apiMessage = tryReadApiMessage(response);
            if (apiMessage) {
                return apiMessage;
            }

            if (typeof response === 'string' && response.trim().length > 0) {
                return response.trim();
            }

            if (response && typeof response === 'object') {
                const raw = response as Record<string, unknown>;
                const directMessage = this.firstString(raw['message'], raw['detail'], raw['title']);
                if (directMessage) {
                    return directMessage;
                }

                const errors = raw['errors'];
                if (Array.isArray(errors)) {
                    const item = errors.map((entry) => (typeof entry === 'string' ? entry.trim() : '')).find((entry) => entry.length > 0);
                    if (item) {
                        return item;
                    }
                } else if (errors && typeof errors === 'object') {
                    const nested = Object.values(errors as Record<string, unknown>)
                        .flatMap((value) => (Array.isArray(value) ? value : [value]))
                        .map((value) => (typeof value === 'string' ? value.trim() : ''))
                        .find((entry) => entry.length > 0);
                    if (nested) {
                        return nested;
                    }
                }
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message.trim();
        }

        return 'Beklenmeyen bir hata olustu.';
    }

    private firstString(...values: unknown[]): string | null {
        for (const value of values) {
            if (typeof value === 'string' && value.trim().length > 0) {
                return value.trim();
            }
        }

        return null;
    }
}
