FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env

COPY . /discordbot
WORKDIR /discordbot
COPY . .
CMD [ "dotnet", "run" ]