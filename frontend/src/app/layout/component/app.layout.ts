import { Component, computed, effect, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { AppTopbar } from './app.topbar';
import { AppSidebar } from './app.sidebar';
import { AppFooter } from './app.footer';
import { AppBreadcrumb } from './app.breadcrumb';
import { LayoutService } from '@/app/layout/service/layout.service';
import { AuthService } from '../../pages/auth';
import { filter } from 'rxjs';

@Component({
    selector: 'app-layout',
    standalone: true,
    imports: [CommonModule, AppTopbar, AppSidebar, RouterModule, AppFooter, AppBreadcrumb],
    template: `<div class="layout-wrapper" [ngClass]="containerClass()">
        @if (isAuthenticated()) {
            <app-topbar></app-topbar>
            <app-sidebar></app-sidebar>
            <div class="layout-main-container">
                <div class="layout-main">
                    <app-breadcrumb></app-breadcrumb>
                    <router-outlet></router-outlet>
                </div>
                <app-footer></app-footer>
            </div>
        } @else {
            <div class="layout-main-container">
                <div class="layout-main">
                    <router-outlet></router-outlet>
                </div>
            </div>
        }
        <div class="layout-mask"></div>
    </div> `
})
export class AppLayout {
    layoutService = inject(LayoutService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);

    constructor() {
        effect(() => {
            const state = this.layoutService.layoutState();
            this.syncBodyScrollState(state.mobileMenuActive);
        });

        this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
            this.syncBodyScrollState(this.layoutService.layoutState().mobileMenuActive);
        });
    }

    containerClass = computed(() => {
        const config = this.layoutService.layoutConfig();
        const state = this.layoutService.layoutState();
        return {
            'layout-overlay': config.menuMode === 'overlay',
            'layout-static': config.menuMode === 'static',
            'layout-static-inactive': state.staticMenuDesktopInactive && config.menuMode === 'static',
            'layout-overlay-active': state.overlayMenuActive,
            'layout-mobile-active': state.mobileMenuActive
        };
    });

    readonly isAuthenticated = computed(() => {
        this.authService.sessionRevision();
        return this.authService.isAuthenticated();
    });

    private syncBodyScrollState(mobileMenuActive: boolean): void {
        if (mobileMenuActive) {
            document.body.classList.add('blocked-scroll');
            return;
        }

        const hasOverlayMask = !!document.querySelector('.p-overlay-mask.p-overlay-mask-enter');
        if (!hasOverlayMask) {
            document.body.classList.remove('blocked-scroll');
            document.body.classList.remove('p-overflow-hidden');
        }
    }
}
