export interface VersionInfo {
    application: string;
    version: string;
    imageTag: string;
    gitSha: string;
    buildTime: string;
}

export interface BackendVersionInfo extends VersionInfo {
    environment: string;
}
