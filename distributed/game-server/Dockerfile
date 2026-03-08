FROM munique/openmu-game:latest

USER root
RUN apt-get update && apt-get install -y wget ca-certificates && rm -rf /var/lib/apt/lists/*

# Install Dapr CLI
RUN wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash

# Download daprd
ARG DAPR_VERSION=1.12.0
RUN wget -q https://github.com/dapr/dapr/releases/download/v${DAPR_VERSION}/daprd_linux_amd64.tar.gz && \
    tar -xzf daprd_linux_amd64.tar.gz && \
    mv daprd /usr/local/bin/ && \
    rm daprd_linux_amd64.tar.gz

COPY ./components /components
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
