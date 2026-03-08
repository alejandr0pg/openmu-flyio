#!/bin/bash
daprd -app-id connectServer -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.ConnectServer.Host.dll
