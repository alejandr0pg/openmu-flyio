#!/bin/bash
daprd -app-id guildServer -app-port 8080 -dapr-http-port 3500 -components-path /components &
dotnet MUnique.OpenMU.GuildServer.Host.dll
