import { Component } from '@angular/core';

@Component({
    standalone: true,
    selector: 'app-footer',
    template: `
        <div class="layout-footer footer-brand">
            <img src="logo.png" alt="STYS" class="footer-brand-logo" />
            <span>by TOD</span>
        </div>
    `,
    styles: [`
        .footer-brand {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: .5rem;
            flex-wrap: wrap;
        }

        .footer-brand-logo {
            height: 1.75rem;
            width: auto;
            object-fit: contain;
        }
    `]
})
export class AppFooter {}
