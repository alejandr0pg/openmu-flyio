#!/bin/bash
daprd -app-id gameServer$GS_ID -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.GameServer.Host.dll
