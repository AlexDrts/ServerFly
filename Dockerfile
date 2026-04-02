# ------------------- Stage 1: Build -------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо тільки проектний файл спочатку (для кращого кешування)
COPY *.csproj .
RUN dotnet restore

# Копіюємо весь код і публікуємо
COPY . .
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    --self-contained false \
    -p:PublishSingleFile=false

# ------------------- Stage 2: Runtime -------------------
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Копіюємо опублікований додаток
COPY --from=build /app/publish .

# Важливо для UDP на Fly.io
EXPOSE 9000/udp

# Запускаємо сервер
ENTRYPOINT ["dotnet", "UdpChatServer.dll"]