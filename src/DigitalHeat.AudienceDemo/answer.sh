#!/bin/sh
set -eu
question="$1"
exec llama-completion -m /models/apertus.gguf -t 1 -c 2048 -n 64 --temp 0 --seed 42 --jinja \
  -sys 'Answer the question concisely in English. Do not claim to have private data or legal authority.' \
  -p "$question" -cnv -st --no-display-prompt
