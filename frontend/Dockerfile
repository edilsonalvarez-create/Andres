# ── Etapa de compilación ─────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci || npm install
COPY . .
RUN npm run build

# ── Etapa de ejecución (nginx, non-root — Sprint 16-B) ────────────────
# Imagen oficial "unprivileged": corre como UID 101, escucha en 8080
# (los puertos <1024 requieren root). Sin USER/chown manual necesario.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
