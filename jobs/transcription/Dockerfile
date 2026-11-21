# Transcription job image: whisper.cpp CLI + ffmpeg + queue runner. No model inside the image;
# the model is mounted at /models (see docs/jobs/transcription.md). Runs offline once built.
#
#   docker build -t digitalheat/transcription-whisper jobs/transcription
#
# GGML_NATIVE=ON optimises for the CPU that builds the image (we build and measure on the same
# machine). Set --build-arg GGML_NATIVE=OFF for an image that runs on any x86-64 CPU.
ARG DEBIAN_IMAGE=debian:bookworm-slim

FROM ${DEBIAN_IMAGE} AS build
ARG WHISPER_CPP_VERSION=v1.9.5
ARG GGML_NATIVE=ON
RUN apt-get update && apt-get install -y --no-install-recommends \
      build-essential cmake git ca-certificates && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch "${WHISPER_CPP_VERSION}" https://github.com/ggml-org/whisper.cpp /src
RUN cmake -S /src -B /build -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF \
      -DGGML_NATIVE="${GGML_NATIVE}" -DWHISPER_BUILD_TESTS=OFF \
 && cmake --build /build -j --config Release --target whisper-cli

FROM ${DEBIAN_IMAGE}
ARG WHISPER_CPP_VERSION=v1.9.5
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
COPY --from=build /build/bin/whisper-cli /usr/local/bin/whisper-cli
COPY run_queue.sh /usr/local/bin/run_queue.sh
RUN chmod +x /usr/local/bin/run_queue.sh
LABEL org.digitalheat.whisper-cpp-version="${WHISPER_CPP_VERSION}"
ENV WORK=/work MODEL=/models/ggml-base.bin
ENTRYPOINT ["/usr/local/bin/run_queue.sh"]
CMD ["run"]
