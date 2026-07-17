# Runner efímero JMeter (Sprint 13-C). Non-root UID 1000.
FROM eclipse-temurin:17-jre-jammy

ARG JMETER_VERSION=5.6.3
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates unzip \
    && curl -fsSL "https://archive.apache.org/dist/jmeter/binaries/apache-jmeter-${JMETER_VERSION}.tgz" \
         -o /tmp/jmeter.tgz \
    && mkdir -p /opt/jmeter \
    && tar -xzf /tmp/jmeter.tgz -C /opt/jmeter --strip-components=1 \
    && rm /tmp/jmeter.tgz \
    && groupadd --gid 1000 runner \
    && useradd --uid 1000 --gid 1000 --create-home --shell /bin/bash runner \
    && mkdir -p /workspace && chown -R runner:runner /workspace /opt/jmeter \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

ENV PATH="/opt/jmeter/bin:${PATH}"
USER runner
WORKDIR /workspace

ENTRYPOINT []
CMD ["jmeter", "--version"]
