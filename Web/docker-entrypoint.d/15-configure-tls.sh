#!/bin/sh
set -eu

certificate_directory=/etc/nginx/certs
certificate_path="$certificate_directory/tls.crt"
private_key_path="$certificate_directory/tls.key"
acme_webroot=/var/www/certbot
public_domain="${PUBLIC_DOMAIN:?PUBLIC_DOMAIN must be configured}"

mkdir -p "$certificate_directory" "$acme_webroot"

if [ "${ACME_ENABLED:-false}" = "true" ]; then
    live_directory="/etc/letsencrypt/live/$public_domain"
    set -- certbot certonly \
        --standalone \
        --non-interactive \
        --agree-tos \
        --keep-until-expiring \
        --domain "$public_domain"
    if [ -n "${ACME_EMAIL:-}" ]; then
        set -- "$@" --email "$ACME_EMAIL"
    else
        set -- "$@" --register-unsafely-without-email
    fi
    "$@"

    ln -sf "$live_directory/fullchain.pem" "$certificate_path"
    ln -sf "$live_directory/privkey.pem" "$private_key_path"

    (
        while :; do
            sleep 12h
            certbot renew \
                --webroot \
                --webroot-path "$acme_webroot" \
                --quiet \
                --deploy-hook "nginx -s reload"
        done
    ) &
    exit 0
fi

if [ -s "$certificate_path" ] && [ -s "$private_key_path" ]; then
    exit 0
fi

openssl req \
    -x509 \
    -nodes \
    -newkey rsa:2048 \
    -sha256 \
    -days 30 \
    -keyout "$private_key_path" \
    -out "$certificate_path" \
    -subj "/CN=$public_domain" \
    -addext "subjectAltName=DNS:$public_domain,IP:127.0.0.1"
chmod 600 "$private_key_path"
