#!/bin/bash
daprd -app-id loginServer -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.LoginServer.Host.dll
