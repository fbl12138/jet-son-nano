# ---------- 阶段 1：编译 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ObjectDetection/ObjectDetection.csproj ObjectDetection/
RUN dotnet restore ObjectDetection/ObjectDetection.csproj

COPY ObjectDetection/ ObjectDetection/
RUN dotnet publish ObjectDetection/ObjectDetection.csproj -c Release -o /app/publish --no-restore

# ---------- 阶段 2：运行 ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ObjectDetection.dll"]