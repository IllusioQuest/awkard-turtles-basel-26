#!/bin/sh
set -eu
: "${APP_PORT:?APP_PORT must match the node published container port}"
# Dedicated nested engine only. Install before starting its application.
# Existing connections may reply, new outbound app connections may not start.
iptables -N WH-APP-FWD 2>/dev/null || true
iptables -F WH-APP-FWD
iptables -A WH-APP-FWD -i docker0 -m conntrack --ctstate ESTABLISHED,RELATED -j RETURN
iptables -A WH-APP-FWD -i docker0 -j DROP
iptables -A WH-APP-FWD -o docker0 -p tcp --dport "$APP_PORT" -j RETURN
iptables -A WH-APP-FWD -o docker0 -j DROP
iptables -A WH-APP-FWD -j RETURN
iptables -C DOCKER-USER -j WH-APP-FWD 2>/dev/null || iptables -I DOCKER-USER 1 -j WH-APP-FWD
# Traffic destined for the engine itself traverses INPUT, not DOCKER-USER.
iptables -N WH-APP-HOST 2>/dev/null || true
iptables -F WH-APP-HOST
iptables -A WH-APP-HOST -m conntrack --ctstate ESTABLISHED,RELATED -j RETURN
iptables -A WH-APP-HOST -j DROP
iptables -C INPUT -i docker0 -j WH-APP-HOST 2>/dev/null || iptables -I INPUT 1 -i docker0 -j WH-APP-HOST
