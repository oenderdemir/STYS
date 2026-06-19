/**
 * Merkezi tarih/saat yardımcı fonksiyonları.
 *
 * Kullanım kuralları:
 * - p-datepicker modeli: Date | null
 * - input[type="date"] modeli: yyyy-MM-dd string
 * - input[type="datetime-local"] modeli: yyyy-MM-ddTHH:mm string
 * - API'ye gönderim: toLocalDateString / toLocalDateTimeString (UTC kayması olmaz)
 * - API'den gelen string: parseApiDate / parseApiDateTime ile Date'e dönüştür
 * - Tablo/ekran gösterimi: formatDateForDisplay / formatDateTimeForDisplay
 */

/**
 * API'den gelen date string'ini veya mevcut Date nesnesini Date | null'a çevirir.
 * Sadece tarih alanları için (yyyy-MM-dd). Lokal saatle yorumlar, UTC kayması olmaz.
 */
export function parseApiDate(value: string | Date | null | undefined): Date | null {
    if (!value) return null;

    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? null : value;
    }

    const normalized = value.trim();
    if (!normalized) return null;

    // Sadece tarih (yyyy-MM-dd) → lokal tarih olarak yorumla
    if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
        const [y, m, d] = normalized.split('-').map(Number);
        const date = new Date(y, m - 1, d);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    // Tarih+saat varsa parseApiDateTime'a yönlendir
    return parseApiDateTime(normalized);
}

/**
 * API'den gelen datetime string'ini veya mevcut Date nesnesini Date | null'a çevirir.
 * ISO 8601, "yyyy-MM-dd" ve "yyyy-MM-ddTHH:mm:ss" formatlarını destekler.
 */
export function parseApiDateTime(value: string | Date | null | undefined): Date | null {
    if (!value) return null;

    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? null : value;
    }

    const normalized = value.trim();
    if (!normalized) return null;

    // Sadece tarih → lokal gece yarısı olarak yorumla
    if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
        const [y, m, d] = normalized.split('-').map(Number);
        const date = new Date(y, m - 1, d);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    // Tarih+saat (UTC suffix'siz) → lokal saat olarak yorumla
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$/.test(normalized)) {
        const date = new Date(normalized);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    // Diğer formatlar (Z suffix veya offset varsa): tarayıcı varsayılanına bırak
    const date = new Date(normalized);
    return Number.isNaN(date.getTime()) ? null : date;
}

/**
 * Date nesnesini API'ye gönderilecek yyyy-MM-dd formatında stringe çevirir.
 * toISOString() yerine kullan — UTC kayması olmaz.
 */
export function toLocalDateString(value: Date | null | undefined): string | null {
    if (!value) return null;

    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
}

/**
 * Date nesnesini API'ye gönderilecek yyyy-MM-ddTHH:mm:ss formatında stringe çevirir.
 * toISOString() yerine kullan — UTC kayması olmaz.
 */
export function toLocalDateTimeString(value: Date | null | undefined): string | null {
    if (!value) return null;

    const pad = (n: number) => n.toString().padStart(2, '0');
    const y = value.getFullYear();
    const mo = pad(value.getMonth() + 1);
    const d = pad(value.getDate());
    const h = pad(value.getHours());
    const mi = pad(value.getMinutes());
    const s = pad(value.getSeconds());
    return `${y}-${mo}-${d}T${h}:${mi}:${s}`;
}

/**
 * Date nesnesini input[type="datetime-local"] için yyyy-MM-ddTHH:mm formatında stringe çevirir.
 */
export function toDateTimeLocalInput(value: Date | null | undefined): string {
    if (!value) return '';

    const pad = (n: number) => n.toString().padStart(2, '0');
    const y = value.getFullYear();
    const mo = pad(value.getMonth() + 1);
    const d = pad(value.getDate());
    const h = pad(value.getHours());
    const mi = pad(value.getMinutes());
    return `${y}-${mo}-${d}T${h}:${mi}`;
}

/**
 * input[type="datetime-local"] string değerini (yyyy-MM-ddTHH:mm) API için yyyy-MM-ddTHH:mm:ss'e çevirir.
 */
export function dateTimeLocalInputToApiString(value: string | null | undefined): string | null {
    if (!value) return null;
    const trimmed = value.trim();
    if (!trimmed) return null;
    // Saniye yoksa ekle
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(trimmed)) {
        return `${trimmed}:00`;
    }
    return trimmed;
}

/**
 * Belirli saat/dakikayla bir Date oluşturur. dayOffset ile gün kaydırılabilir.
 */
export function createDateWithTime(hour: number, minute = 0, dayOffset = 0): Date {
    const date = new Date();
    date.setDate(date.getDate() + dayOffset);
    date.setHours(hour, minute, 0, 0);
    return date;
}

/**
 * Bugünün başlangıcını (00:00:00) döndürür.
 */
export function todayStart(): Date {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
}

/**
 * Tarihi Türkçe "dd.MM.yyyy" formatında gösterir. Değer yoksa "-" döner.
 */
export function formatDateForDisplay(value: string | Date | null | undefined): string {
    const date = parseApiDateTime(value);
    if (!date) return '-';

    return new Intl.DateTimeFormat('tr-TR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    }).format(date);
}

/**
 * Tarih+saati Türkçe "dd.MM.yyyy HH:mm" formatında gösterir. Değer yoksa "-" döner.
 */
export function formatDateTimeForDisplay(value: string | Date | null | undefined): string {
    const date = parseApiDateTime(value);
    if (!date) return '-';

    return new Intl.DateTimeFormat('tr-TR', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
    }).format(date);
}
