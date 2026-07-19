import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
    standalone: true,
    selector: 'app-footer',
    template: `
        <div class="layout-footer footer-brand">
            <img src="logo.png" alt="STYS" class="footer-brand-logo" />
            <span>by TOD</span>
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.Eager,
    styles: [
        `
            .footer-brand {
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 0.5rem;
                flex-wrap: wrap;
            }

            .footer-brand-logo {
                height: 1.75rem;
                width: auto;
                object-fit: contain;
            }
        `
    ]
})
export class AppFooter {}
