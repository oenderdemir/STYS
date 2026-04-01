FROM node:22-alpine AS build
WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm install --no-audit --no-fund

COPY frontend/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime

RUN apk add --no-cache apache2-utils

COPY frontend/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY frontend/nginx/docker-entrypoint.d/10-generate-swagger-auth.sh /docker-entrypoint.d/10-generate-swagger-auth.sh
COPY --from=build /src/frontend/dist/sakai-ng/browser /usr/share/nginx/html

RUN sed -i 's/\r$//' /docker-entrypoint.d/10-generate-swagger-auth.sh \
    && chmod +x /docker-entrypoint.d/10-generate-swagger-auth.sh

EXPOSE 80
