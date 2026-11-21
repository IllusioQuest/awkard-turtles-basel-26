#!/bin/sh
# Local LLM job: summarise each transcript in the queue into German meeting minutes with llama.cpp.
#
#   run    summarise every document; PASSES=0 repeats forever (live demo), N = N passes
#
# Contract with the host (see docs/jobs/llm-summary.md):
#   /work/in/doc_NNN.txt          transcript to summarise
#   /work/in/doc_NNN.minutes      audio minutes behind that transcript (the work unit)
#   /work/out/pass_NNN/*.md       one summary per document; a document is done once its .md exists
#   /work/out/log.csv             pass, doc, audio_minutes, prompt_tokens, generated_tokens,
#                                 prompt_ms, generation_ms, start_unix_ns, end_unix_ns
#   /work/progress.json           work_unit, units_done (audio minutes summarised), docs_done, docs_total, pass
# A freeze (docker pause) loses nothing; a crash loses the current document.
set -eu

MODEL="${MODEL:-/models/Apertus-8B-Instruct-2509-Q4_K_M.gguf}"
THREADS="${THREADS:-8}"
PASSES="${PASSES:-1}"
MAX_NEW_TOKENS="${MAX_NEW_TOKENS:-400}"
CONTEXT_TOKENS="${CONTEXT_TOKENS:-8192}"
SEED="${SEED:-42}"
WORK="${WORK:-/work}"
SYSTEM_PROMPT="${SYSTEM_PROMPT:-Du bist ein sorgfältiger Protokollführer. Antworte auf Deutsch.}"
INSTRUCTION="${INSTRUCTION:-Fasse das folgende Transkript zusammen: zuerst eine Zusammenfassung in höchstens fünf Sätzen, dann die wichtigsten Punkte als Liste, dann offene Fragen oder Aufgaben als Liste. Erfinde nichts, was nicht im Transkript steht.}"

write_progress() {  # $1 pass, $2 docs done in this pass, $3 total docs, $4 audio minutes done (all passes)
  awk -v p="$1" -v d="$2" -v t="$3" -v m="$4" 'BEGIN {
    printf "{\"work_unit\": \"audio minute summarised\", \"units_done\": %.3f, \"pass\": %d, \"docs_done\": %d, \"docs_total\": %d}\n", m, p, d, t
  }' > "$WORK/progress.json.tmp"
  mv "$WORK/progress.json.tmp" "$WORK/progress.json"
}

perf() {  # extract "<what> time = X ms / N tokens" from llama.cpp's performance report
  sed -n "s/.*$1 time = *\([0-9.]*\) ms \/ *\([0-9]*\) .*/\1 \2/p" "$2" | tail -1
}

run() {
  [ -f "$MODEL" ] || { echo "model not found: $MODEL" >&2; exit 2; }
  total=$(ls "$WORK/in"/doc_*.txt 2>/dev/null | wc -l)
  [ "$total" -gt 0 ] || { echo "no documents in $WORK/in" >&2; exit 2; }
  mkdir -p "$WORK/out"
  [ -f "$WORK/out/log.csv" ] || echo "pass,doc,audio_minutes,prompt_tokens,generated_tokens,prompt_ms,generation_ms,start_unix_ns,end_unix_ns" > "$WORK/out/log.csv"
  done_minutes=0
  pass=0
  while [ "$PASSES" -eq 0 ] || [ "$pass" -lt "$PASSES" ]; do
    out=$(printf "%s/out/pass_%03d" "$WORK" "$pass")
    mkdir -p "$out"
    n=0
    for doc in "$WORK/in"/doc_*.txt; do
      name=$(basename "$doc" .txt)
      minutes=$(cat "$WORK/in/$name.minutes")
      if [ ! -f "$out/$name.md" ]; then
        prompt="$out/$name.prompt"
        { printf '%s\n\n' "$INSTRUCTION"; cat "$doc"; } > "$prompt"
        start=$(date +%s%N)
        # greedy decoding (temperature 0) and a fixed seed: deterministic output
        llama-completion -m "$MODEL" -t "$THREADS" -c "$CONTEXT_TOKENS" -n "$MAX_NEW_TOKENS" \
          --temp 0 --seed "$SEED" --jinja -sys "$SYSTEM_PROMPT" -f "$prompt" -cnv -st --no-display-prompt \
          2> "$out/$name.log" < /dev/null | sed "s/ *\[end of text\]//" > "$out/$name.part"
        end=$(date +%s%N)
        set -- $(perf "prompt eval" "$out/$name.log"); p_ms=${1:-}; p_tok=${2:-}
        set -- $(perf " eval" "$out/$name.log"); g_ms=${1:-}; g_tok=${2:-}
        mv "$out/$name.part" "$out/$name.md"
        rm -f "$prompt"
        echo "$pass,$name,$minutes,$p_tok,$g_tok,$p_ms,$g_ms,$start,$end" >> "$WORK/out/log.csv"
      fi
      n=$((n + 1))
      done_minutes=$(awk -v a="$done_minutes" -v b="$minutes" 'BEGIN { printf "%.3f", a + b }')
      write_progress "$pass" "$n" "$total" "$done_minutes"
    done
    pass=$((pass + 1))
  done
}

case "${1:-run}" in
  run) run ;;
  *) echo "usage: run_queue.sh run" >&2; exit 2 ;;
esac
