ARG JDK_IMAGE
ARG JRE_IMAGE
FROM ${JDK_IMAGE} AS probe
WORKDIR /build
COPY MinecraftStatus.java .
RUN javac MinecraftStatus.java

FROM ${JRE_IMAGE}
ARG MC_VERSION
ARG SERVER_SHA256
ARG MC_USER_ID
ARG MC_PORT
LABEL digitalheat.minecraft.version=${MC_VERSION} digitalheat.minecraft.server_sha256=${SERVER_SHA256}
WORKDIR /opt/minecraft
COPY server.jar ./server.jar
RUN echo "${SERVER_SHA256}  /opt/minecraft/server.jar" | sha256sum -c -
COPY --from=probe /build/MinecraftStatus.class /opt/probe/
COPY start-server.sh /opt/minecraft/start-server.sh
COPY server.properties /opt/minecraft/default-server.properties
RUN chmod 0555 /opt/minecraft/start-server.sh && mkdir /data && chown ${MC_USER_ID}:${MC_USER_ID} /data
ENV MC_VERSION=${MC_VERSION}
USER ${MC_USER_ID}:${MC_USER_ID}
STOPSIGNAL SIGTERM
WORKDIR /data
EXPOSE ${MC_PORT}
ENTRYPOINT ["/opt/minecraft/start-server.sh"]
