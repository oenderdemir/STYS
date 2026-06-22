import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { getApiBaseUrl } from '../config';
import { BackendVersionInfo, VersionInfo } from './version.model';

@Injectable({ providedIn: 'root' })
export class VersionService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getFrontendVersion(): Observable<VersionInfo> {
        const assetsBase = this.apiBaseUrl.replace(/\/api$/, '');
        return this.http.get<VersionInfo>(`${assetsBase}/assets/version.json`).pipe(
            catchError(() => of<VersionInfo>({
                application: 'STYS Frontend',
                version: 'unknown',
                imageTag: 'unknown',
                gitSha: 'unknown',
                buildTime: 'unknown'
            }))
        );
    }

    getBackendVersion(): Observable<BackendVersionInfo> {
        return this.http.get<BackendVersionInfo>(`${this.apiBaseUrl}/ui/version`).pipe(
            catchError(() => of<BackendVersionInfo>({
                application: 'STYS Backend',
                version: 'unknown',
                imageTag: 'unknown',
                gitSha: 'unknown',
                buildTime: 'unknown',
                environment: 'unknown'
            }))
        );
    }
}
