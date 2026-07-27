FROM node:24-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=/api/v1
ARG VITE_PUBLIC_ORIGIN=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_PUBLIC_ORIGIN=$VITE_PUBLIC_ORIGIN

COPY Web/package*.json ./
RUN npm ci

COPY Web/ .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
RUN apk add --no-cache openssl
COPY Web/nginx.conf /etc/nginx/templates/default.conf.template
COPY Web/docker-entrypoint.d/15-generate-dev-certificate.sh /docker-entrypoint.d/15-generate-dev-certificate.sh
RUN chmod +x /docker-entrypoint.d/15-generate-dev-certificate.sh
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80 443
