export const UiSeverity = {
    Success: 'success',
    Info: 'info',
    Warn: 'warn',
    Error: 'error',
    Danger: 'danger',
    Secondary: 'secondary',
    Contrast: 'contrast'
} as const;

export type UiSeverityValue = (typeof UiSeverity)[keyof typeof UiSeverity];
