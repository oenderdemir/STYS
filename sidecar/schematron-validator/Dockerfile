# GİB UBL-TR Schematron dogrulama sidecar'i. Yalniz internal network uzerinden erisilebilir
# olmalidir (docker-compose.yml'da "expose" kullanilir, "ports" KULLANILMAZ).
#
# Build asamasi: Saxon-HE ve xmlresolver jar'lari, sabit surum + checksum ile Maven Central'dan
# indirilir ve DOGRULANIR (floating "latest" veya dogrulanmamis kaynak KULLANILMAZ). Runtime'da
# HICBIR indirme yapilmaz.

FROM eclipse-temurin:17-jdk-jammy AS build

ARG SAXON_VERSION=13.0
ARG SAXON_SHA1=da65e52c768d36eb37e427d8feb6487aabd588fa
ARG XMLRESOLVER_VERSION=6.0.23
ARG XMLRESOLVER_SHA1=ad4e965f8662c7c8ca4fe8ab8aaef09a49f25447

WORKDIR /build
RUN mkdir -p lib \
    && curl -sSL -o lib/Saxon-HE-${SAXON_VERSION}.jar \
       "https://repo1.maven.org/maven2/net/sf/saxon/Saxon-HE/${SAXON_VERSION}/Saxon-HE-${SAXON_VERSION}.jar" \
    && echo "${SAXON_SHA1}  lib/Saxon-HE-${SAXON_VERSION}.jar" | sha1sum -c - \
    && curl -sSL -o lib/xmlresolver-${XMLRESOLVER_VERSION}.jar \
       "https://repo1.maven.org/maven2/org/xmlresolver/xmlresolver/${XMLRESOLVER_VERSION}/xmlresolver-${XMLRESOLVER_VERSION}.jar" \
    && echo "${XMLRESOLVER_SHA1}  lib/xmlresolver-${XMLRESOLVER_VERSION}.jar" | sha1sum -c -

COPY src src
RUN mkdir -p out/classes \
    && javac -cp "lib/Saxon-HE-${SAXON_VERSION}.jar:lib/xmlresolver-${XMLRESOLVER_VERSION}.jar" \
       -d out/classes $(find src -name "*.java")

FROM eclipse-temurin:17-jre-jammy

ARG SAXON_VERSION=13.0
ARG XMLRESOLVER_VERSION=6.0.23

# curl yalnız docker-compose healthcheck için (internal, dış ağ erişimi container network
# policy'siyle zaten kapalı - bkz. docker-compose.yml, stys-internal ağı).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 schematron \
    && useradd --uid 10001 --gid schematron --no-create-home --shell /usr/sbin/nologin schematron

WORKDIR /app
COPY --from=build /build/lib /app/lib
COPY --from=build /build/out/classes /app/classes
COPY rules /app/rules
COPY skeleton /app/skeleton
COPY manifest.json /app/manifest.json

# Kural seti + kod salt-okunurdur; runtime'da hicbir yazma gerekmez (calisma dizini olarak
# yalniz JVM'in kendi gecici alani gerekebilir, bu container calistirilirken --read-only +
# ayri bir tmpfs /tmp ile saglanmalidir - bkz. docker-compose.yml).
RUN chown -R schematron:schematron /app
USER schematron

EXPOSE 8081

ENTRYPOINT ["java", \
  "-Dschematron.rootDir=/app", \
  "-Dschematron.port=8081", \
  "-Djava.security.egd=file:/dev/./urandom", \
  "-cp", "/app/classes:/app/lib/Saxon-HE-13.0.jar:/app/lib/xmlresolver-6.0.23.jar", \
  "stys.ebelge.schematron.SidecarMain"]
