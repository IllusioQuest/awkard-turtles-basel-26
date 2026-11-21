# Local LLM job image: llama.cpp (CPU) + queue runner. No model inside the image; the GGUF model
# is mounted at /models (see docs/jobs/llm-summary.md). Runs offline once built.
#
#   docker build -t digitalheat/llm-summary jobs/llm
#
# GGML_NATIVE=ON optimises for the CPU that builds the image (we build and measure on the same
# machine). BUILD_JOBS limits compile parallelism so the host stays usable.
ARG DEBIAN_IMAGE=debian:bookworm-slim

FROM ${DEBIAN_IMAGE} AS build
ARG LLAMA_CPP_VERSION=b12600
ARG GGML_NATIVE=ON
ARG BUILD_JOBS=4
RUN apt-get update && apt-get install -y --no-install-recommends \
      build-essential cmake git ca-certificates && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch "${LLAMA_CPP_VERSION}" https://github.com/ggml-org/llama.cpp /src
RUN cmake -S /src -B /build -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF \
      -DGGML_NATIVE="${GGML_NATIVE}" -DLLAMA_CURL=OFF -DLLAMA_BUILD_TESTS=OFF -DLLAMA_BUILD_SERVER=OFF \
 && cmake --build /build -j "${BUILD_JOBS}" --config Release --target llama-completion llama-bench \
 && ls /build/bin

FROM ${DEBIAN_IMAGE}
ARG LLAMA_CPP_VERSION=b12600
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /build/bin/ /usr/local/bin/
COPY run_queue.sh /usr/local/bin/run_queue.sh
RUN chmod +x /usr/local/bin/run_queue.sh
LABEL org.digitalheat.llama-cpp-version="${LLAMA_CPP_VERSION}"
ENV WORK=/work
ENTRYPOINT ["/usr/local/bin/run_queue.sh"]
CMD ["run"]
