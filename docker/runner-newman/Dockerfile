# Runner efímero Newman (Sprint 13-B). Usuario non-root 1000:1000.
FROM node:22-bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && npm install -g newman@6 \
    && groupadd --gid 1000 runner \
    && useradd --uid 1000 --gid 1000 --create-home --shell /bin/bash runner \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

USER runner
WORKDIR /workspace

# El comando lo pasa docker run (newman run …).
ENTRYPOINT []
CMD ["newman", "--version"]
