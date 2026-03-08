#!/bin/bash

echo "Deploying OpenMU Distributed System to Fly.io..."

# Function to deploy a service
deploy_service() {
    service=$1
    app_name=$2
    echo "Deploying $service..."
    cd $service
    
    # Check if fly.toml exists, if not, launch (this shouldn't happen with my setup)
    if ! fly status --app $app_name > /dev/null 2>&1; then
        echo "App $app_name does not exist. Please create it first with 'fly apps create $app_name'"
        # fly launch --name $app_name --copy-config --no-deploy
    fi
    
    fly deploy --config fly.toml --app $app_name
    cd ..
}

# Deploy services
deploy_service "connect-server" "openmu-connect"
deploy_service "login-server" "openmu-login"
deploy_service "chat-server" "openmu-chat"
deploy_service "friend-server" "openmu-friend"
deploy_service "guild-server" "openmu-guild"
deploy_service "admin-panel" "openmu-admin"

# Deploy GameServer 0 (Example)
echo "Deploying GameServer 0..."
cd game-server
fly deploy --config fly.toml --app openmu-game-0 --env GS_ID=0
cd ..

echo "Deployment complete!"
