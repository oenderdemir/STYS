export interface LicenseGenerationContextDto {
    productCode: string;
    licenseFilePath: string;
    environmentName: string;
    instanceId: string;
    customerCode: string;
    deploymentMarker: string;
    fingerprintProfile: string;
    runtimeFingerprintHash: string;
    runtimeMachineName: string;
    runtimeOsDescription: string;
    requiresDeploymentMarker: boolean;
}
