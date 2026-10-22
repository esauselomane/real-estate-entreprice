FROM node:22-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=8080
COPY --chown=node:node property-public-site/package.json property-public-site/server.mjs property-public-site/seo.mjs property-public-site/chat.mjs ./
COPY --chown=node:node property-public-site/public ./public
USER node
EXPOSE 8080
CMD ["node", "server.mjs"]
