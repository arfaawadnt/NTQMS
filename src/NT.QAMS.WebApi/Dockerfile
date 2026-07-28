# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/NT.QAMS.WebApi/NT.QAMS.WebApi.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# DEPLOY-002: evidence files live on a mounted volume owned by the app user;
# created here so a bind-less run still works.
RUN mkdir -p /app/data/files && chown -R $APP_UID /app/data
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    FileStorage__RootPath=/app/data/files
EXPOSE 8080
# OPS-008: probe readiness (process up AND PostgreSQL answering), not bare liveness.
HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -qO- http://localhost:8080/health/ready || exit 1
# DEPLOY-002: never run the API as root — 'app' is the distroless-style
# unprivileged user the aspnet base image ships (uid $APP_UID).
USER $APP_UID
ENTRYPOINT ["dotnet", "NT.QAMS.WebApi.dll"]
