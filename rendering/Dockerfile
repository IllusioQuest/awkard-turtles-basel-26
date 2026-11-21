FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc
RUN apt-get update && apt-get install -y --no-install-recommends blender ca-certificates && rm -rf /var/lib/apt/lists/*
COPY landscape.py scene_builder.py scene_settings.json /opt/render/
RUN mkdir /work && chown 65534:65534 /work
USER 65534:65534
VOLUME ["/work"]
ENTRYPOINT ["blender","--background","--threads","1","--python-exit-code","1","--python","/opt/render/landscape.py","--"]

