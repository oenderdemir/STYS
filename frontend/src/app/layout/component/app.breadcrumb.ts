import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { BreadcrumbModule } from 'primeng/breadcrumb';

@Component({
    selector: 'app-breadcrumb',
    standalone: true,
    imports: [CommonModule, RouterModule, BreadcrumbModule],
    template: `
        @if (items.length > 0) {
            <div class="app-breadcrumb-shell">
                <p-breadcrumb [model]="items"></p-breadcrumb>
            </div>
        }
    `,
    styles: [`
        .app-breadcrumb-shell {
            margin-bottom: 1rem;
            padding: 0.5rem 0 0.25rem;
        }

        :host ::ng-deep .app-breadcrumb-shell .p-breadcrumb {
            border: 0;
            background: transparent;
            padding: 0;
        }

        :host ::ng-deep .app-breadcrumb-shell .p-breadcrumb-list {
            flex-wrap: wrap;
            gap: 0.2rem;
        }

        :host ::ng-deep .app-breadcrumb-shell .p-menuitem-text {
            font-size: 0.85rem;
        }
    `]
})
export class AppBreadcrumb implements OnInit {
    private readonly router = inject(Router);
    private readonly activatedRoute = inject(ActivatedRoute);

    items: MenuItem[] = [];

    ngOnInit(): void {
        this.rebuild();
        this.router.events
            .pipe(filter((event) => event instanceof NavigationEnd))
            .subscribe(() => this.rebuild());
    }

    private rebuild(): void {
        const labels = this.collectBreadcrumbLabels(this.activatedRoute.root);
        this.items = labels.map((label) => ({
            label
        }));
    }

    private collectBreadcrumbLabels(route: ActivatedRoute): string[] {
        const labels: string[] = [];
        let current: ActivatedRoute | null = route;

        while (current) {
            const data = current.snapshot.data;
            const breadcrumb = data['breadcrumb'] as string | string[] | undefined;

            if (Array.isArray(breadcrumb)) {
                for (const label of breadcrumb) {
                    if (label && !labels.includes(label)) {
                        labels.push(label);
                    }
                }
            } else if (typeof breadcrumb === 'string' && breadcrumb.trim().length > 0) {
                labels.push(breadcrumb);
            }

            current = current.firstChild;
        }

        return labels;
    }
}
