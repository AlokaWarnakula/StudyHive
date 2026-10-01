# StudyHive web client: Vite build served by unprivileged nginx. Build context: web/
#   docker build --build-arg VITE_API_BASE_URL=https://api.example.com -t studyhive-web web
# VITE_API_BASE_URL is compiled into the bundle at build time (it is a public URL, not a secret).
# The container listens on $PORT (default 8080).

FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_API_BASE_URL
RUN test -n "$VITE_API_BASE_URL" || { echo "Build arg VITE_API_BASE_URL is required" >&2; exit 1; }
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
# The image's entrypoint renders /etc/nginx/templates/*.template into conf.d with envsubst.
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
ENV PORT=8080
EXPOSE 8080
