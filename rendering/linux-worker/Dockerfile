# Current official Blender, pinned for identical CPU/GPU frame images across peers.
FROM debian:bookworm-slim@sha256:734a72b3d6142a71993b4bc580ca01d8ca687f77a2c422d96a961d3376b179bc
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates xz-utils libx11-6 libxi6 libxrender1 libxfixes3 libxxf86vm1 libxkbcommon0 libgl1 libegl1 libsm6 libice6 libgomp1 libvulkan1 && rm -rf /var/lib/apt/lists/*
ADD --checksum=sha256:dacb8264197a3f7fda6001285a5a506fc97ee943881261c2c3bd63c882f22e05 https://download.blender.org/release/Blender5.3/blender-5.3.0-linux-x64.tar.xz /tmp/blender.tar.xz
RUN mkdir /opt/blender /work && tar -xJf /tmp/blender.tar.xz -C /opt/blender --strip-components=1 && rm /tmp/blender.tar.xz && chown 65534:65534 /work
COPY landscape.py scene_builder.py scene_settings.json /opt/render/
USER 65534:65534
VOLUME ["/work"]
ENTRYPOINT ["/opt/blender/blender","--background","--threads","1","--python-exit-code","1","--python","/opt/render/landscape.py","--"]
