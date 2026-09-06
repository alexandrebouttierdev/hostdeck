# Docker

Docker is abstracted behind `ContainerRuntime`.

V1 provides:

- demo runtime with seeded containers
- SSH-backed runtime that can query Docker CLI/API over the existing SSH session

Presentation never talks to Docker directly.
