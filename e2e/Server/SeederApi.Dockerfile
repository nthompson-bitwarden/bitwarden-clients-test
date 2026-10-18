# Keep Cargo's default target directory: RustSdk.csproj copies native libraries from it.
# The upstream Dockerfile overrides CARGO_TARGET_DIR and omits libsdk.so from publication.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential curl pkg-config libssl-dev \
    && rm -rf /var/lib/apt/lists/*
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --profile minimal
ENV PATH="/root/.cargo/bin:${PATH}"
WORKDIR /source
COPY . .
RUN dotnet publish util/SeederApi/SeederApi.csproj -c Release -o /out
ARG TARGETARCH
RUN case "$TARGETARCH" in amd64) sdk_arch=x64 ;; arm64) sdk_arch=arm64 ;; *) exit 1 ;; esac \
    && mkdir -p "/out/runtimes/linux-$sdk_arch/native" \
    && cp util/RustSdk/rust/target/release/libsdk.so "/out/runtimes/linux-$sdk_arch/native/libsdk.so"

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
HEALTHCHECK CMD curl -f http://localhost:5000/alive || exit 1
ENTRYPOINT ["dotnet", "/app/SeederApi.dll"]
