#!/bin/sh
set -eu

AUTH_FILE="/etc/nginx/.swagger_htpasswd"
USERNAME="${STYS_SWAGGER_AUTH_USERNAME:-swagger}"
PASSWORD="${STYS_SWAGGER_AUTH_PASSWORD:-ChangeMe!Swagger_2026}"

htpasswd -bc "${AUTH_FILE}" "${USERNAME}" "${PASSWORD}" >/dev/null 2>&1
