import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AppMenuitem } from './app.menuitem';
import { MenuRuntimeService } from '../../core/menu';
import { AppMenuItem } from '../../core/menu/models';
import { LayoutService } from '../service/layout.service';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

interface MenuSearchResult {
    label: string;
    breadcrumb: string;
    icon?: string;
    routerLink?: string[];
    url?: string;
    target?: string;
    routeText: string;
}

@Component({
    selector: 'app-menu',
    standalone: true,
    imports: [CommonModule, AppMenuitem, RouterModule, ButtonModule, InputTextModule],
    styleUrl: './app.menu.scss',
    template: `
        <div class="layout-menu-search" [class.layout-menu-search--open]="searchExpanded()">
            <button
                pButton
                type="button"
                icon="pi pi-search"
                [text]="true"
                [rounded]="true"
                severity="secondary"
                class="layout-menu-search-toggle"
                (click)="openSearch()"
                aria-label="Menüde ara">
            </button>

            @if (searchExpanded()) {
                <input
                    #searchInput
                    pInputText
                    type="text"
                    [value]="searchTerm()"
                    (input)="searchTerm.set($any($event.target).value)"
                    (keydown.escape)="closeSearch()"
                    placeholder="Menüde ara..."
                    autocomplete="off"
                    class="layout-menu-search-input"
                />
                <button
                    pButton
                    type="button"
                    icon="pi pi-times"
                    [text]="true"
                    [rounded]="true"
                    severity="secondary"
                    class="layout-menu-search-close"
                    (click)="closeSearch()"
                    aria-label="Kapat">
                </button>
            }
        </div>

        @if (searchExpanded() && normalizedSearchTerm().length >= 2) {
            <ul class="layout-menu layout-menu-search-results">
                @if (menuSearchResults().length > 0) {
                    @for (result of menuSearchResults(); track result.routeText) {
                        <li class="layout-menu-search-result">
                            <a (click)="navigateToResult(result)" (keydown.enter)="navigateToResult(result)" tabindex="0" role="button">
                                <i [ngClass]="result.icon || 'pi pi-circle'" class="layout-menuitem-icon"></i>
                                <span class="search-result-content">
                                    <span class="search-result-label">{{ result.label }}</span>
                                    @if (result.breadcrumb) {
                                        <small class="search-result-breadcrumb">{{ result.breadcrumb }}</small>
                                    }
                                </span>
                            </a>
                        </li>
                    }
                } @else {
                    <li class="layout-menu-search-empty">Sonuç bulunamadı.</li>
                }
            </ul>
        } @else {
            <ul class="layout-menu">
                @for (item of menuRuntimeService.menuItems(); track item.label) {
                    @if (!item.separator) {
                        <li app-menuitem [item]="item" [root]="true"></li>
                    } @else {
                        <li class="menu-separator"></li>
                    }
                }
            </ul>
        }
    `
})
export class AppMenu {
    readonly menuRuntimeService = inject(MenuRuntimeService);
    private readonly router = inject(Router);
    private readonly layoutService = inject(LayoutService);

    readonly searchTerm = signal<string>('');
    readonly searchExpanded = signal<boolean>(true);

    private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

    readonly normalizedSearchTerm = computed(() => this.normalizeText(this.searchTerm()));

    readonly menuSearchResults = computed((): MenuSearchResult[] => {
        const term = this.normalizedSearchTerm();
        if (term.length < 2) return [];

        const allItems = this.flattenMenuItems(this.menuRuntimeService.menuItems(), []);
        const seen = new Set<string>();
        const results: MenuSearchResult[] = [];

        for (const item of allItems) {
            if (seen.has(item.routeText)) continue;

            const searchable = this.normalizeText(`${item.label} ${item.breadcrumb} ${item.routeText}`);
            if (searchable.includes(term)) {
                seen.add(item.routeText);
                results.push(item);
            }
        }

        return results;
    });

    openSearch(): void {
        if (this.searchExpanded()) {
            this.closeSearch();
            return;
        }
        this.searchExpanded.set(true);
        setTimeout(() => this.searchInput()?.nativeElement.focus(), 50);
    }

    closeSearch(): void {
        this.searchTerm.set('');
        this.searchExpanded.set(false);
    }

    navigateToResult(result: MenuSearchResult): void {
        if (result.routerLink?.length) {
            this.router.navigate(result.routerLink);
        } else if (result.url) {
            if (result.target === '_blank') {
                window.open(result.url, '_blank');
            } else {
                window.location.href = result.url;
            }
        }

        this.closeSearch();
        this.layoutService.layoutState.update((val) => ({
            ...val,
            overlayMenuActive: false,
            mobileMenuActive: false,
            menuHoverActive: false
        }));
    }

    private flattenMenuItems(items: AppMenuItem[], parents: string[]): MenuSearchResult[] {
        const results: MenuSearchResult[] = [];

        for (const item of items) {
            if (item.visible === false || item.separator) continue;

            const label = item.label ?? '';
            const currentPath = [...parents, label].filter(Boolean);
            const isLeaf = !item.items?.length;

            if (isLeaf && (item.routerLink || item.url)) {
                const routeText = item.routerLink
                    ? (Array.isArray(item.routerLink) ? item.routerLink[0] : String(item.routerLink))
                    : (item.url ?? '');

                results.push({
                    label,
                    breadcrumb: parents.join(' > '),
                    icon: item.icon,
                    routerLink: item.routerLink as string[] | undefined,
                    url: item.url,
                    target: item.target,
                    routeText: String(routeText)
                });
            }

            if (item.items?.length) {
                results.push(...this.flattenMenuItems(item.items, currentPath));
            }
        }

        return results;
    }

    private normalizeText(value: string): string {
        return value
            .toLowerCase()
            .replace(/ç/g, 'c')
            .replace(/ğ/g, 'g')
            .replace(/ı/g, 'i')
            .replace(/ö/g, 'o')
            .replace(/ş/g, 's')
            .replace(/ü/g, 'u')
            .replace(/i̇/g, 'i');
    }
}
