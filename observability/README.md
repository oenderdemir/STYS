# STYS Observability Stack

Grafana + Loki + Alloy ile dosya tabanlı log görüntüleme.

STYS backend loglarını Grafana arayüzünden izler.
Uygulama koduna dokunulmaz; loglar dosyaya yazılmaya devam eder.

## Kurulum

### 1. `.env` dosyasını yapılandır

Proje kökündeki `.env.example` dosyasını `.env` olarak kopyalayın ve
`STYS_BACKEND_LOG_HOST_PATH` değerini gerçek log klasörünüze göre ayarlayın.

**Windows:**
```
STYS_BACKEND_LOG_HOST_PATH=C:/Users/cuce/source/repos/STYS/Logs
STYS_BACKEND_LOG_CONTAINER_PATH=/app/Logs
STYS_ALLOY_LOG_CONTAINER_PATH=/var/log/stys/backend
```

> Windows path'lerinde `\` yerine `/` kullanın. Docker Desktop her iki formatı da
> kabul eder ama `/` daha güvenilirdir.

**Linux / Mac:**
```
STYS_BACKEND_LOG_HOST_PATH=./Logs
STYS_BACKEND_LOG_CONTAINER_PATH=/app/Logs
STYS_ALLOY_LOG_CONTAINER_PATH=/var/log/stys/backend
```

`STYS_BACKEND_LOG_HOST_PATH` her iki compose dosyası tarafından ortak kullanılır:
- `docker-compose.yml` → backend container buraya yazar
- `docker-compose.observability.yml` → Alloy buradan okur

### 2. Stack'i başlat

**STYS + Observability (tam stack):**
```bash
docker compose --env-file .env -f docker-compose.yml -f docker-compose.observability.yml up -d
```

**Sadece Observability stack** (loglar zaten yazılıyorsa):
```bash
docker compose --env-file .env -f docker-compose.observability.yml up -d
```

> STYS backend çalışmıyorsa log dosyaları oluşmaz; Alloy beklemeye geçer,
> dosyalar oluşunca otomatik olarak işlemeye başlar.

## Grafana

URL: http://localhost:3000

Varsayılan kullanıcı: `admin` / `admin`

Grafana admin şifresini değiştirmek için `.env` dosyasına ekleyin:
```
STYS_GRAFANA_ADMIN_PASSWORD=güçlü-şifre-buraya
```

## Log Sorgulama (Grafana Explore)

Grafana → Explore → Loki datasource seç

### Tüm backend logları
```logql
{app="stys", component="backend"}
```

### Sadece error log dosyaları
```logql
{app="stys", component="backend", log_type="error"}
```

### JSON alanlarını parse ederek sorgulama

Serilog Compact JSON formatındaki alanlar (`@t`, `@l`, `@m`, `@x`) LogQL ile
sorgu anında parse edilebilir:

```logql
{app="stys", component="backend"} | json
```

```logql
{app="stys", component="backend"} | json | line_format "{{.`@l`}} {{.`@m`}}"
```

```logql
# Sadece Error ve üstü seviyeler
{app="stys", component="backend"} | json | `@l`=~"Error|Fatal"
```

```logql
# TraceId ile ara
{app="stys", component="backend"} | json | TraceId="<trace-id-buraya>"
```

### Metrik sorgular

```logql
# Son 1 saat error sayısı
count_over_time({app="stys", log_type="error"}[1h])
```

## Servisler

| Servis   | Port  | Açıklama                 |
|----------|-------|--------------------------|
| Grafana  | 3000  | Log görüntüleme arayüzü  |
| Loki     | 3100  | Log depolama             |
| Alloy    | 12345 | Alloy UI / metrics       |

## İzlenen Log Dosyaları

Alloy, `STYS_BACKEND_LOG_HOST_PATH` olarak mount edilen klasörü izler:

| Host klasörü (örnek)                    | Loki label              |
|-----------------------------------------|-------------------------|
| `<LOG_PATH>/stys-*.json`                | `log_type="application"` |
| `<LOG_PATH>/errors/stys-error-*.json`   | `log_type="error"`       |

## Güvenlik Notu

Bu yapı local/test ortam içindir.
Production ortamında:
- `STYS_GRAFANA_ADMIN_PASSWORD` mutlaka değiştirilmeli
- Grafana portu (3000) dışarıya açılmamalı
- Loki portu (3100) iç network'te kalmalı
