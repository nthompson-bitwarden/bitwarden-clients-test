# Native Rust SDK support is required to generate real, client-decryptable vaults.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential curl pkg-config libssl-dev \
    && rm -rf /var/lib/apt/lists/*
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --profile minimal
ENV PATH="/root/.cargo/bin:${PATH}"
WORKDIR /source
COPY . .
RUN dotnet publish util/SeederUtility/SeederUtility.csproj -c Release -o /out

ARG TARGETARCH
RUN case "$TARGETARCH" in amd64) sdk_arch=x64 ;; arm64) sdk_arch=arm64 ;; *) exit 1 ;; esac \
    && mkdir -p "/out/runtimes/linux-$sdk_arch/native" \
    && cp util/RustSdk/rust/target/release/libsdk.so "/out/runtimes/linux-$sdk_arch/native/libsdk.so"

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "/app/SeederUtility.dll"]
