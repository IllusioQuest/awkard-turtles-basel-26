# Voice request job: one task = transcribe a spoken request (whisper.cpp) and answer it with a
# local LLM (llama.cpp). Built offline from the two existing job images (docs/jobs/transcription.md,
# docs/jobs/llm-summary.md); no models inside the image. Ticket T-78.
#
#   docker build -t digitalheat/voice-assistant jobs/voice-assistant
ARG WHISPER_IMAGE=digitalheat/transcription-whisper:latest
ARG LLM_IMAGE=digitalheat/llm-summary:latest

FROM ${LLM_IMAGE} AS llm

FROM ${WHISPER_IMAGE}
COPY --from=llm /usr/local/bin/llama-completion /usr/local/bin/llama-completion
COPY run.sh /usr/local/bin/voice-request.sh
RUN chmod +x /usr/local/bin/voice-request.sh
ENTRYPOINT ["/usr/local/bin/voice-request.sh"]
