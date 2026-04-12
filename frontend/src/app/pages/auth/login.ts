import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { RippleModule } from 'primeng/ripple';
import { tryReadApiMessage } from '../../core/api';
import { LoginResponseDto } from './dto';
import { AuthService } from './auth.service';
import { AppFloatingConfigurator } from '../../layout/component/app.floatingconfigurator';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [ButtonModule, CheckboxModule, InputTextModule, PasswordModule, FormsModule, RouterModule, RippleModule, AppFloatingConfigurator],
    template: `
        <app-floating-configurator />
        <div class="bg-surface-50 dark:bg-surface-950 flex items-center justify-center min-h-screen min-w-screen overflow-hidden">
            <div class="flex flex-col items-center justify-center">
                <div style="border-radius: 56px; padding: 0.3rem; background: linear-gradient(180deg, var(--primary-color) 10%, rgba(33, 150, 243, 0) 30%)">
                    <div class="w-full bg-surface-0 dark:bg-surface-900 py-20 px-8 sm:px-20" style="border-radius: 53px">
                        <div class="text-center mb-8">
                            <div class="text-surface-900 dark:text-surface-0 text-3xl font-medium mb-4">STYS - Hoşgeldiniz</div>
                            <span class="text-muted-color font-medium">Lütfen Giriş Yapınız</span>
                        </div>

                        <div>
                            <label for="username1" class="block text-surface-900 dark:text-surface-0 text-xl font-medium mb-2">Kullanıcı Adı</label>
                            <input
                                pInputText
                                id="username1"
                                type="text"
                                placeholder="Kullanıcı Adı"
                                class="w-full md:w-120 mb-8"
                                [(ngModel)]="userName"
                                [disabled]="isSubmitting"
                                (keyup.enter)="signIn()"
                            />

                            <label for="password1" class="block text-surface-900 dark:text-surface-0 font-medium text-xl mb-2">Parola</label>
                            <p-password
                                id="password1"
                                [(ngModel)]="password"
                                placeholder="Parola"
                                [toggleMask]="true"
                                styleClass="mb-4"
                                [fluid]="true"
                                [feedback]="false"
                                [disabled]="isSubmitting"
                                (keyup.enter)="signIn()"
                            ></p-password>

                            <!--
                            <div class="flex items-center justify-between mt-2 mb-4 gap-8">
                                <div class="flex items-center">
                                    <p-checkbox [(ngModel)]="checked" id="rememberme1" binary class="mr-2"></p-checkbox>
                                    <label for="rememberme1">Remember me</label>
                                </div>
                            </div>
                            -->

                            @if (errorMessage) {
                                <small class="block mb-4 text-red-500">{{ errorMessage }}</small>
                            }

                            <p-button
                                [label]="isSubmitting ? 'Kontrol Ediliyor...' : 'Giriş Yap'"
                                styleClass="w-full"
                                [disabled]="isSubmitting || !userName || !password"
                                (onClick)="signIn()"
                            ></p-button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `
})
export class Login implements OnInit {
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);
    private readonly cdr = inject(ChangeDetectorRef);

    userName = '';
    password = '';
    checked = false;
    isSubmitting = false;
    errorMessage: string | null = null;

    ngOnInit(): void {
        const reason = this.route.snapshot.queryParamMap.get('reason');
        if (reason === 'expired') {
            this.errorMessage = 'Session expired. Please sign in again.';
            return;
        }

        if (reason === 'inactivity') {
            this.errorMessage = 'Session ended due to inactivity. Please sign in again.';
            return;
        }

        if (reason === 'unauthorized') {
            this.errorMessage = 'Your session is no longer valid. Please sign in again.';
        }
    }

    signIn(): void {
        if (this.isSubmitting || !this.userName || !this.password) {
            return;
        }

        this.errorMessage = null;
        this.isSubmitting = true;

        this.authService
            .login(this.userName.trim(), this.password)
            .pipe(
                finalize(() => {
                    this.isSubmitting = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (response: LoginResponseDto) => {
                    
 
                    this.authService.storeSession(response);
                    void this.router.navigateByUrl(this.getReturnUrl());
                },
                error: (error: unknown) => {
                    this.errorMessage = this.resolveErrorMessage(error);
                    this.cdr.detectChanges();
                }
            });
    }

    private getReturnUrl(): string {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        if (!returnUrl || !returnUrl.startsWith('/')) {
            return '/';
        }

        return returnUrl;
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (error.status === 401) {
                if (apiMessage) {
                    return `${apiMessage}`;
                }

                return 'Kullanıcı adı veya parola yanlış.';
            }

            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Login başarısız. Tekrar deneyin.';
    }
}
