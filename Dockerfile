# ---------- 1) 前端构建 ----------
FROM node:24-alpine AS web
WORKDIR /src/web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# ---------- 2) 后端构建 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ObjectDetection/ObjectDetection.csproj ObjectDetection/
RUN dotnet restore ObjectDetection/ObjectDetection.csproj

COPY ObjectDetection/ ObjectDetection/
# 用刚构建的前端产物替换仓库里的 wwwroot（避免旧资源残留）
RUN rm -rf ObjectDetection/wwwroot && mkdir -p ObjectDetection/wwwroot
COPY --from=web /src/web/dist/ ObjectDetection/wwwroot/

RUN dotnet publish ObjectDetection/ObjectDetection.csproj -c Release -o /app/publish --no-restore

# ---------- 3) 运行时 ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# 健康检查需要 curl（基础镜像不带）
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ObjectDetection.dll"]
