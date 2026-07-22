# Runner efímero Playwright / Visual / Selenium IDE (Sprint 13-C). Non-root.
FROM mcr.microsoft.com/playwright:v1.50.1-jammy

USER root
RUN npm install -g @playwright/test selenium-side-runner \
    && groupadd --gid 1000 runner 2>/dev/null || true \
    && id -u 1000 >/dev/null 2>&1 || useradd --uid 1000 --gid 1000 --create-home --shell /bin/bash runner \
    && mkdir -p /workspace && chown -R 1000:1000 /workspace

USER 1000:1000
WORKDIR /workspace
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

ENTRYPOINT []
CMD ["npx", "playwright", "--version"]
