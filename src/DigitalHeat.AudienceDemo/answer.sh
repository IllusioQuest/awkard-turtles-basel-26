#!/bin/sh
set -eu
question="$1"
exec llama-completion -m /models/Apertus-8B-Instruct-2509-Q4_K_M.gguf -t 4 -c 1024 -n 40 --temp 0 --seed 42 --jinja \
  -sys 'Answer in one clear English sentence, at most 30 words. Do not claim to have private data or legal authority.' \
  -p "$question" -cnv -st --no-display-prompt
