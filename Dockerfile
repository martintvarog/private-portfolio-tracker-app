FROM node:22-alpine AS client-build
WORKDIR /client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/ ./src/
RUN dotnet publish src/PortfolioTrackerApp.Api -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api-build /publish ./
COPY --from=client-build /client/dist ./wwwroot/
EXPOSE 8080
ENTRYPOINT ["dotnet", "PortfolioTrackerApp.Api.dll"]
