#!/bin/sh
# Transcription job: a queue of audio chunks processed one by one with whisper.cpp.
#
#   prepare <audio file>   convert to 16 kHz mono WAV and split into CHUNK_SECONDS chunks in /work/in
#   run                    transcribe every chunk; PASSES=0 repeats forever (live demo), N = N passes
#
# Contract with the host (see docs/jobs/transcription.md):
#   /work/in/chunk_NNNN.wav       input queue
#   /work/out/pass_NNN/*.txt      one transcript per chunk; a chunk counts as done once its .txt exists
#   /work/out/log.csv             pass, chunk, audio_seconds, start_unix_ns, end_unix_ns
#   /work/progress.json           work_unit, units_done (audio minutes), chunks_done, chunks_total, pass
# Work is written per chunk, so a freeze (docker pause) loses nothing and a crash loses one chunk.
set -eu

MODEL="${MODEL:-/models/ggml-base.bin}"
LANGUAGE="${LANGUAGE:-de}"
THREADS="${THREADS:-8}"
PASSES="${PASSES:-1}"
CHUNK_SECONDS="${CHUNK_SECONDS:-30}"
WORK="${WORK:-/work}"
WAV_BYTES_PER_SECOND=32000   # 16 kHz x 16 bit x 1 channel
WAV_HEADER_BYTES=44
SECONDS_PER_MINUTE=60

prepare() {
  rm -rf "$WORK/in" && mkdir -p "$WORK/in"
  ffmpeg -nostdin -loglevel error -i "$1" -ar 16000 -ac 1 -c:a pcm_s16le \
    -f segment -segment_time "$CHUNK_SECONDS" "$WORK/in/chunk_%04d.wav"
  echo "prepared $(ls "$WORK/in" | wc -l) chunks of up to ${CHUNK_SECONDS} s in $WORK/in"
}

audio_seconds() {  # exact for the PCM WAV written by prepare()
  bytes=$(stat -c %s "$1")
  awk -v b="$bytes" -v h="$WAV_HEADER_BYTES" -v r="$WAV_BYTES_PER_SECOND" 'BEGIN { printf "%.3f", (b - h) / r }'
}

write_progress() {  # $1 pass, $2 chunks done in this pass, $3 total chunks, $4 audio seconds done (all passes)
  awk -v p="$1" -v d="$2" -v t="$3" -v s="$4" -v m="$SECONDS_PER_MINUTE" 'BEGIN {
    printf "{\"work_unit\": \"audio minute\", \"units_done\": %.3f, \"pass\": %d, \"chunks_done\": %d, \"chunks_total\": %d}\n", s / m, p, d, t
  }' > "$WORK/progress.json.tmp"
  mv "$WORK/progress.json.tmp" "$WORK/progress.json"
}

run() {
  [ -f "$MODEL" ] || { echo "model not found: $MODEL" >&2; exit 2; }
  total=$(ls "$WORK/in"/chunk_*.wav 2>/dev/null | wc -l)
  [ "$total" -gt 0 ] || { echo "no chunks in $WORK/in; run prepare first" >&2; exit 2; }
  mkdir -p "$WORK/out"
  [ -f "$WORK/out/log.csv" ] || echo "pass,chunk,audio_seconds,start_unix_ns,end_unix_ns" > "$WORK/out/log.csv"
  done_seconds=0
  pass=0
  while [ "$PASSES" -eq 0 ] || [ "$pass" -lt "$PASSES" ]; do
    out=$(printf "%s/out/pass_%03d" "$WORK" "$pass")
    mkdir -p "$out"
    n=0
    for wav in "$WORK/in"/chunk_*.wav; do
      name=$(basename "$wav" .wav)
      secs=$(audio_seconds "$wav")
      if [ ! -f "$out/$name.txt" ]; then
        start=$(date +%s%N)
        # -nf: no temperature fallback, so the output is deterministic (greedy decoding only)
        whisper-cli -m "$MODEL" -l "$LANGUAGE" -t "$THREADS" -nf -np -otxt -of "$out/$name.part" -f "$wav" >/dev/null 2>&1
        mv "$out/$name.part.txt" "$out/$name.txt"
        end=$(date +%s%N)
        echo "$pass,$name,$secs,$start,$end" >> "$WORK/out/log.csv"
      fi
      n=$((n + 1))
      done_seconds=$(awk -v a="$done_seconds" -v b="$secs" 'BEGIN { printf "%.3f", a + b }')
      write_progress "$pass" "$n" "$total" "$done_seconds"
    done
    pass=$((pass + 1))
  done
}

case "${1:-run}" in
  prepare) shift; prepare "$@" ;;
  run) run ;;
  *) echo "usage: run_queue.sh prepare <audio> | run" >&2; exit 2 ;;
esac
