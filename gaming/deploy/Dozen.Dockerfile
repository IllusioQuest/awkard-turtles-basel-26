ARG DESKTOP_IMAGE=ghcr.io/selkies-project/selkies/desktop@sha256:5e877dfc8280afc2326bb24a3a57d37d2edb081e595014842aff1d6cd3569015
FROM ${DESKTOP_IMAGE} AS builder
USER root
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential meson ninja-build cmake pkg-config flex bison \
    python3-mako python3-yaml python3-packaging \
    libdrm-dev libexpat1-dev libzstd-dev zlib1g-dev \
    libxcb-dri3-dev libxcb-present-dev libxcb-randr0-dev libxcb-shm0-dev libxcb-sync-dev \
    libx11-xcb-dev libxshmfence-dev libxrandr-dev libudev-dev libvulkan-dev \
    directx-headers-dev spirv-tools && rm -rf /var/lib/apt/lists/*
WORKDIR /build
ADD --checksum=sha256:caf1c0061a68e88dfa74967a7e780c0e85d65b6c4e334cd69095a5dc54ad78bc \
    https://archive.mesa3d.org/mesa-26.0.8.tar.xz /build/mesa.tar.xz
RUN tar -xf mesa.tar.xz && meson setup /build/out /build/mesa-26.0.8 \
    --prefix=/opt/digitalheat-dzn --libdir=lib --buildtype=release --wrap-mode=nofallback \
    -Dvulkan-drivers=microsoft-experimental -Dgallium-drivers=[] -Dplatforms=x11 \
    -Dglx=disabled -Degl=disabled -Dgbm=disabled -Dgles1=disabled -Dgles2=disabled \
    -Dopengl=false -Dllvm=disabled -Dmicrosoft-clc=disabled -Dbuild-tests=false \
    && timeout 1200 ninja -C /build/out -j2 && meson install -C /build/out \
    && dpkg-query -W > /opt/digitalheat-dzn/build-packages.txt \
    && cp /build/out/meson-info/intro-buildoptions.json /opt/digitalheat-dzn/
FROM ${DESKTOP_IMAGE}
COPY --from=builder /opt/digitalheat-dzn /opt/digitalheat-dzn
COPY --from=builder /build/mesa-26.0.8/docs/license.rst /opt/digitalheat-dzn/mesa-license.rst
# Opt-in image only: system OpenGL libraries are not overwritten.
ENV VK_DRIVER_FILES=/opt/digitalheat-dzn/share/vulkan/icd.d/dzn_icd.x86_64.json
ENV LD_LIBRARY_PATH=/opt/digitalheat-dzn/lib:/usr/lib/wsl/lib
