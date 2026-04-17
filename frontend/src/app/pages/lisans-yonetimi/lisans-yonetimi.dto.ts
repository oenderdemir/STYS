export interface LicenseStatusDto {
    isValid: boolean;
    licenseId: string | null;
    productCode: string | null;
    customerCode: string | null;
    customerName: string | null;
    environmentName: string | null;
    instanceId: string | null;
    issuedAtUtc: string | null;
    expiresAtUtc: string | null;
    enabledModules: string[];
    errors: string[];
}
