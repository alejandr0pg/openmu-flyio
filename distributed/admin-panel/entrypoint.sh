#!/bin/bash
daprd -app-id adminPanel -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.AdminPanel.Host.dll
