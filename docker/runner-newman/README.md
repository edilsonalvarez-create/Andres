# Sprint 13-B — imagen sandbox Newman
# Build: docker build -t qaguardian/runner-newman:local -f docker/runner-newman/Dockerfile docker/runner-newman
#
# Seguridad:
# - USER 1000:1000 (non-root)
# - El API monta SOLO el workspace del TestRun en /workspace
# - Red por defecto: none (Runners:SandboxNetworkMode). bridge solo si se necesitan APIs externas.
# - No recibe ConnectionStrings / JWT / EncryptionKey del proceso API
