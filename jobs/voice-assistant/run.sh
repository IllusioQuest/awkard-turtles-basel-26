#!/bin/sh
# One voice request, two stages, one container: transcribe the spoken request (whisper.cpp), then
# answer it with a local LLM (llama.cpp). The scheduler pauses/resumes the whole container from
# outside (docker pause). Every stage writes its result as soon as it exists.
#
#   /data/models/<whisper model>, /data/models/<llm model>   read only
#   /data/work/request/audio.*  (or request/text.txt)        input
#   /data/work/stage            transcribing | answering | done
#   /data/work/transcript.txt   recognised request
#   /data/work/answer.txt       LLM answer, written while it is generated
#   /data/work/timings.csv      stage,start|end,unix_ns
#
# Arguments: threads max_new_tokens language whisper_model llm_model system_prompt
# (all from config/voice-demo.json; the system prompt carries the case figures, sourced there).
set -eu
THREADS="$1"; MAX_NEW_TOKENS="$2"; LANGUAGE="$3"; WHISPER_MODEL="$4"; LLM_MODEL="$5"; SYSTEM_PROMPT="$6"
WORK=/data/work; MODELS=/data/models

stage() { echo "$1" > "$WORK/stage.part"; mv "$WORK/stage.part" "$WORK/stage"; }
mark() { echo "$1,$2,$(date +%s%N)" >> "$WORK/timings.csv"; }

if [ ! -s "$WORK/transcript.txt" ]; then
  stage transcribing; mark transcribe start
  audio=$(ls "$WORK"/request/audio.* 2>/dev/null | head -n 1 || true)
  if [ -n "$audio" ]; then
    ffmpeg -nostdin -loglevel error -y -i "$audio" -ar 16000 -ac 1 -c:a pcm_s16le "$WORK/request.wav"
    # whisper.cpp pads every input to a 30 s window (1500 encoder frames, 50 per second). A short
    # request only needs its own length plus a margin; this cuts the encoder time several-fold.
    bytes=$(wc -c < "$WORK/request.wav")
    ctx=$(( (bytes - 44) / 32000 * 50 + 150 )); [ "$ctx" -gt 1500 ] && ctx=1500
    # Greedy decoding (-bs 1 -bo 1) and a fixed language avoid the 5-beam search and a second
    # language-detection pass.
    whisper-cli -m "$MODELS/$WHISPER_MODEL" -l "$LANGUAGE" -t "$THREADS" -bs 1 -bo 1 -nf -ac "$ctx" -np -otxt \
      -of "$WORK/transcript.part" -f "$WORK/request.wav" >/dev/null 2>&1
    tr -s ' \n' ' ' < "$WORK/transcript.part.txt" | sed 's/^ *//; s/ *$//' > "$WORK/transcript.tmp"
    rm -f "$WORK/transcript.part.txt"
  else
    cp "$WORK/request/text.txt" "$WORK/transcript.tmp"
  fi
  mv "$WORK/transcript.tmp" "$WORK/transcript.txt"; mark transcribe end
fi

stage answering; mark answer start
llama-completion -m "$MODELS/$LLM_MODEL" -t "$THREADS" -c 2048 -n "$MAX_NEW_TOKENS" --temp 0 --seed 1 \
  --jinja -sys "$SYSTEM_PROMPT" -p "$(cat "$WORK/transcript.txt")" -cnv -st --no-display-prompt \
  > "$WORK/answer.txt" 2> "$WORK/llm.log"
sed -i 's/ *\[end of text\]//' "$WORK/answer.txt"
mark answer end
stage done
