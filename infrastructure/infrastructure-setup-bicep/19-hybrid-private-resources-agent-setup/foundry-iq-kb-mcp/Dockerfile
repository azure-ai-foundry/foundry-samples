FROM node:22-slim AS builder

WORKDIR /app

# Copy foundry-iq-mcp-apps source (build from source)
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts --no-optional

COPY tsconfig.json tsconfig.server.json vite.config.ts ./
COPY scripts/ scripts/
COPY src/ src/
COPY public/ public/

RUN npm run build

# ── Production stage ─────────────────────────────────────────
FROM node:22-slim

WORKDIR /app

COPY --from=builder /app/dist ./dist
COPY --from=builder /app/package.json ./

# Install production deps only (no bun, no dev)
RUN npm install --omit=dev --ignore-scripts --no-optional

# Container Apps convention: port 8080
ENV PORT=8080
EXPOSE 8080

# Health-check: hit the /mcp endpoint with an OPTIONS request
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD node -e "fetch('http://localhost:8080/mcp',{method:'OPTIONS'}).then(r=>{if(!r.ok)throw r.status}).catch(()=>process.exit(1))"

CMD ["node", "dist/index.js"]
