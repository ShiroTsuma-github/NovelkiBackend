FROM node:24-alpine AS build
WORKDIR /app

ARG VITE_API_BASE_URL=http://localhost:5232/api/v1
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY Web/package*.json ./
RUN npm ci

COPY Web/ .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
