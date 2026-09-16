FROM node:20-alpine AS deps
WORKDIR /app
COPY client-facing-app/package.json client-facing-app/package-lock.json* ./
RUN npm ci

FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY client-facing-app/ .
ARG NEXT_PUBLIC_GATEWAY_URL
ARG NEXT_PUBLIC_APP_URL
ARG NEXT_PUBLIC_SITE_URL
ENV NEXT_PUBLIC_GATEWAY_URL=$NEXT_PUBLIC_GATEWAY_URL
ENV NEXT_PUBLIC_APP_URL=$NEXT_PUBLIC_APP_URL
ENV NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production

COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static

EXPOSE 80

ENV PORT=80
ENV HOSTNAME="0.0.0.0"

CMD ["node", "server.js"]
