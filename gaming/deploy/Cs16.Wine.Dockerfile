ARG DESKTOP_IMAGE=ghcr.io/selkies-project/selkies/desktop@sha256:5e877dfc8280afc2326bb24a3a57d37d2edb081e595014842aff1d6cd3569015
FROM ${DESKTOP_IMAGE}
USER root
ENV DEBIAN_FRONTEND=noninteractive
# Ubuntu repository compatibility packages; supplied game assets are never committed or downloaded here.
RUN dpkg --add-architecture i386 && apt-get update \
    && apt-get install -y --no-install-recommends wine wine32:i386 wine64 \
       libgl1-mesa-dri:i386 libgl1:i386 \
    && dpkg-query -W > /opt/digitalheat-cs16-packages.txt \
    && rm -rf /var/lib/apt/lists/*
ENV WINEDEBUG=-all WINEPREFIX=/home/ubuntu/.wine-cs16
COPY desktop-background.py /opt/digitalheat/desktop-background.py
# Apply before the desktop process starts, including when a persisted home has old wallpaper settings.
RUN sed -i 's|^Exec=.*|Exec=python3 /opt/digitalheat/desktop-background.py --desktop|' /etc/xdg/autostart/lxqt-desktop.desktop
USER ubuntu
