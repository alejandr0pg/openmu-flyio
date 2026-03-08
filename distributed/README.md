# OpenMU Distributed Deployment on Fly.io

This directory contains the configuration for deploying OpenMU as a distributed system on Fly.io.

## Prerequisites

1.  **Fly CLI**: Installed and authenticated (`fly auth login`).
2.  **PostgreSQL**: An external PostgreSQL database accessible from Fly.io.
3.  **Redis**: A Redis instance (e.g., Upstash on Fly.io) for Dapr state store.
4.  **RabbitMQ**: A RabbitMQ instance (or Redis PubSub) for Dapr pub/sub.

## Setup Steps

1.  **Configure Components**:
    *   Edit `components/login-state.yaml`: Set your Redis host and password.
    *   Edit `components/pubsub.yaml`: Set your RabbitMQ connection string.

2.  **Create Apps**:
    Create the following apps on Fly.io:
    ```bash
    fly apps create openmu-connect
    fly apps create openmu-login
    fly apps create openmu-chat
    fly apps create openmu-friend
    fly apps create openmu-guild
    fly apps create openmu-admin
    fly apps create openmu-game-0
    ```

3.  **Set Secrets**:
    For each app, set the `POSTGRES_CONNECTION_STRING` secret:
    ```bash
    fly secrets set POSTGRES_CONNECTION_STRING="Server=...;Port=5432;..." -a openmu-connect
    fly secrets set POSTGRES_CONNECTION_STRING="Server=...;Port=5432;..." -a openmu-login
    # Repeat for all apps
    ```

4.  **Deploy**:
    Run the deployment script:
    ```bash
    chmod +x deploy_all.sh
    ./deploy_all.sh
    ```

## Notes

*   **Dapr**: Each service runs with a Dapr sidecar (`daprd`) in the same container.
*   **GameServer Scaling**: To add more GameServers, create a new app (e.g., `openmu-game-1`), copy the `game-server` folder or reuse it, and deploy with `GS_ID=1`.
*   **Networking**: Services communicate via Dapr (port 3500) and internal Fly.io networking.
