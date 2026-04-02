# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо код
COPY UdpChatServer.cs .

# Створюємо проєкт і публікуємо
RUN dotnet new console --framework net9.0 --force && \
    mv UdpChatServer.cs Program.cs && \
    dotnet publish -c Release -o /app/publish --no-self-contained

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 9000/udp

ENTRYPOINT ["dotnet", "UdpChatServer.dll"]
