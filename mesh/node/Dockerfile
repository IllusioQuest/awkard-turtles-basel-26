FROM docker:29.8.2-dind@sha256:1e08cdb63405ca788aea94ef35b792d1e299607c667d33d7bcbcae1fd2611ced
RUN apk add --no-cache libstdc++ libgcc
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ASPNETCORE_URLS=http://+:8080 DOCKER_TLS_CERTDIR=""
COPY . /app/
RUN sed -i 's/\r$//' /app/start-node.sh && chmod +x /app/start-node.sh
ENTRYPOINT ["/app/start-node.sh"]
