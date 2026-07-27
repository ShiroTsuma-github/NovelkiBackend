#!/bin/sh
set -eu

certificate_directory=/etc/nginx/certs
certificate_path="$certificate_directory/tls.crt"
private_key_path="$certificate_directory/tls.key"

if [ -s "$certificate_path" ] && [ -s "$private_key_path" ]; then
    exit 0
fi

mkdir -p "$certificate_directory"
openssl req \
    -x509 \
    -nodes \
    -newkey rsa:2048 \
    -sha256 \
    -days 3650 \
    -keyout "$private_key_path" \
    -out "$certificate_path" \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
chmod 600 "$private_key_path"
