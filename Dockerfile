# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Layer A — chỉ copy .csproj (cached cho đến khi dependencies thay đổi)
# Không copy test projects vì không cần trong production image
COPY src/Beacon.Api/Beacon.Api.csproj                         Beacon.Api/
COPY src/Beacon.Application/Beacon.Application.csproj         Beacon.Application/
COPY src/Beacon.Domain/Beacon.Domain.csproj                   Beacon.Domain/
COPY src/Beacon.Infrashtructure/Beacon.Infrashtructure.csproj Beacon.Infrashtructure/
COPY src/Beacon.Shared/Beacon.Shared.csproj                   Beacon.Shared/

RUN dotnet restore "Beacon.Api/Beacon.Api.csproj"

# Layer B — copy source code (rebuild chỉ khi code thay đổi, restore được cache từ layer trên)
COPY src/Beacon.Api/             Beacon.Api/
COPY src/Beacon.Application/     Beacon.Application/
COPY src/Beacon.Domain/          Beacon.Domain/
COPY src/Beacon.Infrashtructure/ Beacon.Infrashtructure/
COPY src/Beacon.Shared/          Beacon.Shared/

RUN dotnet publish "Beacon.Api/Beacon.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime (image nhỏ gọn, không có SDK) ──────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Beacon.Api.dll"]
