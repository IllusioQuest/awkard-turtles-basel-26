ARG DESKTOP_IMAGE=digitalheat-selkies-dzn:experiment
FROM ${DESKTOP_IMAGE}
USER root
# Official unmodified release; checksum published at https://dl.xonotic.org/xonotic-0.8.6.sha512
RUN curl --fail --location --silent --show-error --retry 2 --max-time 600 \
      https://dl.xonotic.org/xonotic-0.8.6.zip -o /tmp/xonotic.zip \
    && echo 'cb39879e96f19abb2877588c2d50c5d3e64dd68153bec3dd1bebedf4d765e506afa419c28381d7005aed664cb1a042571c132b5b319e4308cab67745d996c2a6  /tmp/xonotic.zip' | sha512sum -c - \
    && unzip -q /tmp/xonotic.zip -d /opt && rm /tmp/xonotic.zip \
    && test -x /opt/Xonotic/xonotic-linux64-sdl
USER ubuntu
