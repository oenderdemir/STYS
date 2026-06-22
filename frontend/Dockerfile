FROM node:22-alpine AS build

ARG APP_VERSION=unknown
ARG IMAGE_TAG=unknown
ARG GIT_SHA=unknown
ARG BUILD_TIME=unknown
ARG APP_ENV=prod

WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./
RUN npm install --no-audit --no-fund

COPY frontend/ ./
RUN npm run build -- --base-href /stys/ --deploy-url /stys/

RUN cp /src/frontend/dist/sakai-ng/browser/env.${APP_ENV}.js /src/frontend/dist/sakai-ng/browser/env.js

RUN mkdir -p /src/frontend/dist/sakai-ng/browser/assets \
    && printf '{"application":"STYS Frontend","version":"%s","imageTag":"%s","gitSha":"%s","buildTime":"%s"}\n' \
        "$APP_VERSION" "$IMAGE_TAG" "$GIT_SHA" "$BUILD_TIME" \
        > /src/frontend/dist/sakai-ng/browser/assets/version.json

FROM nginx:1.27-alpine AS runtime

RUN apk add --no-cache apache2-utils

COPY frontend/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY frontend/nginx/docker-entrypoint.d/10-generate-swagger-auth.sh /docker-entrypoint.d/10-generate-swagger-auth.sh
COPY --from=build /src/frontend/dist/sakai-ng/browser /usr/share/nginx/html

RUN sed -i 's/\r$//' /docker-entrypoint.d/10-generate-swagger-auth.sh \
    && chmod +x /docker-entrypoint.d/10-generate-swagger-auth.sh

EXPOSE 80
