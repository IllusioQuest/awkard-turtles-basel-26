#!/usr/bin/env bash
# Print the time left until the deadlines in AGENTS.md (section "Deadlines").
#
#   scripts/deadline-reminder.sh                # status of all upcoming deadlines
#   scripts/deadline-reminder.sh --urgent-only  # print only if a deadline is within URGENT_HOURS
#
# Used by the Claude Code hooks in .claude/settings.json; any agent or person can run it.
# Deadlines are read from table rows in AGENTS.md that start with "| YYYY-MM-DD HH:MM |"
# (time zone Europe/Zurich), so AGENTS.md stays the single place to edit them.
# Works with GNU date (Linux) and BSD date (macOS).

set -u

URGENT_HOURS="${DEADLINE_URGENT_HOURS:-6}"   # matches the reminder rule in AGENTS.md
SHOW_PASSED_HOURS=12                           # keep showing a passed deadline for this long
TZ_NAME="Europe/Zurich"

root="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/.." && pwd)}"
agents_file="$root/AGENTS.md"
mode="${1:-}"
[ -f "$agents_file" ] || exit 0

to_epoch() {
  TZ="$TZ_NAME" date -d "$1" +%s 2>/dev/null \
    || TZ="$TZ_NAME" date -j -f "%Y-%m-%d %H:%M:%S" "$1:00" +%s 2>/dev/null
}

fmt_duration() {
  local s=$1 h m
  h=$(( s / 3600 )); m=$(( (s % 3600) / 60 ))
  printf '%dh %02dm' "$h" "$m"
}

now=$(date +%s)
lines=()
urgent=0

while IFS='|' read -r _ when what _rest; do
  when=$(printf '%s' "$when" | sed 's/^ *//; s/ *$//')
  what=$(printf '%s' "$what" | sed 's/^ *//; s/ *$//; s/\*\*//g')
  ts=$(to_epoch "$when") || continue
  left=$(( ts - now ))
  if [ "$left" -lt 0 ]; then
    [ $(( -left )) -le $(( SHOW_PASSED_HOURS * 3600 )) ] || continue
    [ "$mode" = "--urgent-only" ] && continue
    lines+=("- $what ($when): PASSED $(fmt_duration $(( -left ))) ago. Ask the user whether it was done.")
  else
    flag=""
    if [ "$left" -le $(( URGENT_HOURS * 3600 )) ]; then flag=" <-- URGENT"; urgent=1; fi
    lines+=("- $what ($when): $(fmt_duration "$left") left$flag")
  fi
done < <(grep -E '^\| *[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2} *\|' "$agents_file")

[ "${#lines[@]}" -gt 0 ] || exit 0
[ "$mode" = "--urgent-only" ] && [ "$urgent" -eq 0 ] && exit 0

echo "Deadline status (now $(TZ="$TZ_NAME" date '+%Y-%m-%d %H:%M') $TZ_NAME, see AGENTS.md > Deadlines):"
printf '%s\n' "${lines[@]}"
if [ "$urgent" -eq 1 ]; then
  echo "A deadline is less than ${URGENT_HOURS}h away: remind the user in your reply (AGENTS.md > Deadlines)."
fi
exit 0
