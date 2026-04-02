# ------------------- Build Stage -------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Якщо є .csproj — копіюємо його
COPY *.csproj ./
RUN dotnet restore

# Копіюємо весь код
COPY . .

# Публікуємо в framework-dependent режимі (менший розмір)
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    --self-contained false

# ------------------- Runtime Stage -------------------
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Важливо для UDP
EXPOSE 9000/udp

# Запуск (Fly.io запускає саме так)
ENTRYPOINT ["dotnet", "UdpChatServer.dll"]
