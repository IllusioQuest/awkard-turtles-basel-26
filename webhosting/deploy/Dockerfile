FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:bef136a371963aaa46ec97882845ef6f54dec15728bb4b128b1c2a400ffbc9b4 AS build
ARG PROJECT=src/Webhosting.Gateway/Webhosting.Gateway.csproj
WORKDIR /source
COPY src/ src/
COPY samples/ samples/
RUN dotnet publish "$PROJECT" --configuration Release --output /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:12b1135f5f8b3c0a214f2a680ff2033dff4f684f445d6c697d9524ceeb303586 AS runtime
ARG ENTRY_DLL=Webhosting.Gateway.dll
ENV ENTRY_DLL=$ENTRY_DLL
WORKDIR /app
COPY --from=build /app/ .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ENTRY_DLL\""]
