#!/bin/bash
daprd -app-id friendServer -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.FriendServer.Host.dll
