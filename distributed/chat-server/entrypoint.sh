#!/bin/bash
daprd -app-id chatServer -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.ChatServer.Host.dll
