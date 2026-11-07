# Nginx sin privilegios (usuario nginx, uid 101) como reverse proxy de las replicas
FROM nginxinc/nginx-unprivileged:1.28-alpine

# Valores por defecto para docker compose local; en Railway se sobrescriben con variables
ENV PORT=8080 \
    DNS_RESOLVER=127.0.0.11 \
    BACKEND_1_HOST=backend-1 \
    BACKEND_2_HOST=backend-2

COPY default.conf.template /etc/nginx/templates/default.conf.template

EXPOSE 8080
