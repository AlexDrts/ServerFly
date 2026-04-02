# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо всі файли проєкту
COPY . .

# Публікуємо проєкт (назва DLL буде ServerFly.dll)
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 9000/udp

# Запускаємо саме ServerFly.dll
ENTRYPOINT ["dotnet", "ServerFly.dll"]
