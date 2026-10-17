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

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "/app/SeederUtility.dll"]
