FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish tools/Tod.LicenseGenerator/Tod.LicenseGenerator.csproj -c Release -p:TargetFramework=net10.0 -p:EnableWindowsTargeting=true -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /work

COPY --from=build /app/publish /app

ENTRYPOINT ["dotnet", "/app/Tod.LicenseGenerator.dll"]
