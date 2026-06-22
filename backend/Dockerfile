FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore backend/STYS.csproj
RUN dotnet publish backend/STYS.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG APP_VERSION=unknown
ARG IMAGE_TAG=unknown
ARG GIT_SHA=unknown
ARG BUILD_TIME=unknown

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV APP_VERSION=$APP_VERSION
ENV IMAGE_TAG=$IMAGE_TAG
ENV GIT_SHA=$GIT_SHA
ENV BUILD_TIME=$BUILD_TIME

LABEL org.opencontainers.image.version=$APP_VERSION
LABEL org.opencontainers.image.revision=$GIT_SHA
LABEL org.opencontainers.image.created=$BUILD_TIME
LABEL org.opencontainers.image.ref.name=$IMAGE_TAG

EXPOSE 8080

ENTRYPOINT ["dotnet", "STYS.dll"]
