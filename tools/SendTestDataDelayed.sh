#!/usr/bin/env bash

set -euo pipefail

delay_seconds="${1:-30}"
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

broker="${MQTT_HOST:-}"
port="${MQTT_PORT:-}"

# Fall back to the endpoint reference format injected by Aspire.
if [[ -z "$broker" && -n "${services__mqtt__mqtt__0:-}" ]]; then
    endpoint="${services__mqtt__mqtt__0#*://}"
    endpoint="${endpoint%%/*}"
    broker="${endpoint%:*}"
    port="${endpoint##*:}"
fi

if [[ -z "$broker" || -z "$port" ]]; then
    echo "MQTT_HOST and MQTT_PORT environment variables are not set. Cannot send test data." >&2
    exit 1
fi

use_tls="${MQTT_USE_TLS:-false}"

echo "Waiting ${delay_seconds} seconds for broker and clients to be ready..."
sleep "$delay_seconds"

echo "Sending test data to ${broker}:${port} (TLS: ${use_tls}) ..."
pwsh -File "$script_dir/SendTestData.ps1" -Broker "$broker" -BrokerPort "$port"

echo "Test data sent successfully."