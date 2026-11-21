#!/bin/sh
set -eu
# Consent must be supplied explicitly at launch; no committed eula.txt defaults to acceptance.
if [ "${MC_EULA_ACCEPTED:-false}" != true ]; then
    echo 'Minecraft EULA not accepted. Review https://www.minecraft.net/en-us/eula and explicitly opt in.' >&2
    exit 64
fi
for value in "${MC_HEAP_MIN_MIB:-}" "${MC_HEAP_MAX_MIB:-}"; do
    case "$value" in ''|*[!0-9]*) echo 'Explicit numeric heap limits in MiB are required.' >&2; exit 64;; esac
done
if [ "$MC_HEAP_MIN_MIB" -le 0 ] || [ "$MC_HEAP_MAX_MIB" -lt "$MC_HEAP_MIN_MIB" ]; then
    echo 'Invalid JVM heap range.' >&2; exit 64
fi
cd /data
if [ ! -e server.properties ]; then cp /opt/minecraft/default-server.properties server.properties; fi
# Refuse a manually altered authentication setting rather than enabling offline authentication.
if ! grep -qx 'online-mode=true' server.properties; then
    echo 'This lab requires online-mode=true.' >&2; exit 64
fi
printf 'eula=true\n' > eula.txt
mkdir -p /data/.digitalheat
restore_pause() {
    previous_file=/data/.digitalheat/calibration-pause.previous
    if [ -f "$previous_file" ]; then
        previous=$(cat "$previous_file")
        if [ "$previous" != __ABSENT__ ] && ! printf '%s\n' "$previous" | grep -Eq -- '^-?[0-9]+$'; then
            echo 'Invalid saved calibration setting; review before startup.' >&2
            return 1
        fi
        sed -i '/^pause-when-empty-seconds=/d' server.properties
        if [ "$previous" != __ABSENT__ ]; then printf '\npause-when-empty-seconds=%s\n' "$previous" >> server.properties; fi
        rm -f "$previous_file"
    fi
}
# Recover the setting after an interrupted calibration, including before a scheduled startup.
restore_pause || exit 65
if [ "${MC_CALIBRATION_MODE:-false}" = true ]; then
    # Keep the explicitly measured idle condition stable rather than auto-pausing after 60 seconds.
    previous=$(sed -n 's/^pause-when-empty-seconds=//p' server.properties | tail -n 1)
    printf '%s\n' "${previous:-__ABSENT__}" > /data/.digitalheat/calibration-pause.previous
    sed -i '/^pause-when-empty-seconds=/d' server.properties
    printf '\npause-when-empty-seconds=-1\n' >> server.properties
fi
# Route TERM to the game's console stop command, so saving finishes before PID 1 exits.
console=/tmp/digitalheat-minecraft-console
umask 077
boot_id=$(cat /proc/sys/kernel/random/uuid)
printf '{"boot_id":"%s","version":"%s"}\n' "$boot_id" "$MC_VERSION" > /data/.digitalheat/run.json
rm -f /data/.digitalheat/exit.json
record_exit() {
    printf '{"boot_id":"%s","java_exit_code":%s,"stop_requested":%s}\n' "$boot_id" "$1" "$2" > /data/.digitalheat/exit.json.tmp
    mv /data/.digitalheat/exit.json.tmp /data/.digitalheat/exit.json
}
rm -f "$console"
mkfifo "$console"
exec 3<> "$console"
java "-Xms${MC_HEAP_MIN_MIB}M" "-Xmx${MC_HEAP_MAX_MIB}M" -jar /opt/minecraft/server.jar nogui "$@" < "$console" &
server_pid=$!
stop_server() {
    trap '' TERM INT
    printf 'stop\n' >&3
    wait "$server_pid"
    result=$?
    restore_pause || exit 65
    record_exit "$result" true
    printf 'DIGITALHEAT_JAVA_EXIT=%s\n' "$result"
    exit "$result"
}
trap stop_server TERM INT
set +e
wait "$server_pid"
result=$?
exec 3>&-
restore_pause || exit 65
record_exit "$result" false
printf 'DIGITALHEAT_JAVA_EXIT=%s\n' "$result"
exit "$result"
