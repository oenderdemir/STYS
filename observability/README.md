# STYS Observability Stack

Grafana + Loki + Alloy ile dosya tabanlı log görüntüleme.

STYS backend loglarını (`./Data/logs/backend`) Grafana arayüzünden izler.
Uygulama koduna dokunulmaz; loglar dosyaya yazılmaya devam eder.

## Başlatma

### STYS + Observability (tam stack)

```bash
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d
```

### Sadece Observability stack

```bash
docker compose -f docker-compose.observability.yml up -d
```

> Loglar `./Data/logs/backend` klasöründen okunur.
> STYS backend çalışmıyorsa log dosyaları oluşmaz, Alloy beklemeye geçer.

## Grafana

URL: http://localhost:3000

Varsayılan kullanıcı: `admin` / `admin`

Şifreyi environment variable ile geçersiz kılmak için:

```bash
STYS_GRAFANA_ADMIN_PASSWORD=güçlü-şifre docker compose -f docker-compose.observability.yml up -d
```

## Log Sorgulama (Grafana Explore)

Grafana → Explore → Loki seç

### Tüm backend logları

```logql
{app="stys", component="backend"}
```

### Sadece error log dosyaları

```logql
{app="stys", component="backend", log_type="error"}
```

### JSON alanlarını parse ederek sorgulama

Serilog Compact JSON formatı (`@t`, `@l`, `@m`, `@x`) LogQL ile parse edilebilir:

```logql
{app="stys", component="backend"} | json
```

```logql
{app="stys", component="backend"} | json | line_format "{{.`@l`}} {{.`@m`}}"
```

```logql
# Seviyeye göre filtrele
{app="stys", component="backend"} | json | `@l`="Error"
```

```logql
# TraceId ile ara
{app="stys", component="backend"} | json | TraceId="<trace-id>"
```

### Son 1 saat hata sayısı (metric sorgu)

```logql
count_over_time({app="stys", log_type="error"}[1h])
```

## Servisler

| Servis   | Port  | Açıklama                    |
|----------|-------|-----------------------------|
| Grafana  | 3000  | Log görüntüleme arayüzü     |
| Loki     | 3100  | Log depolama                |
| Alloy    | 12345 | Alloy UI / metrics          |

## Log Dosyaları

Alloy şu dosyaları izler:

- `./Data/logs/backend/stys-*.json` → `log_type="application"`
- `./Data/logs/backend/errors/stys-error-*.json` → `log_type="error"`

## Güvenlik Notu

Bu yapı local/test ortam içindir.
Production ortamında:
- `STYS_GRAFANA_ADMIN_PASSWORD` mutlaka değiştirilmeli
- Grafana portu (3000) dışarıya açılmamalı
- Loki portu (3100) iç network'te kalmalı
