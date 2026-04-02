# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копіюємо твій файл (тепер правильно)
COPY Program.cs .

# Створюємо стандартний консольний проєкт і публікуємо
RUN dotnet new console --framework net9.0 --force && \
    mv Program.cs Program.cs && \          # просто переміщуємо, щоб не було конфліктів
    dotnet publish -c Release -o /app/publish --no-self-contained

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 9000/udp

# Запускаємо саме те, що реально створює publish (зазвичай Program.dll)
ENTRYPOINT ["dotnet", "Program.dll"]
