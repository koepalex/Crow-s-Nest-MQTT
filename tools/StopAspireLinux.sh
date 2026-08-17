#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
apphost_pattern="${repo_root}/src/AppHost/bin/.*/AppHost"

mapfile -t apphost_pids < <(pgrep -f "$apphost_pattern" || true)

if ((${#apphost_pids[@]} == 0)); then
    echo "No existing Aspire AppHost processes found."
    exit 0
fi

for apphost_pid in "${apphost_pids[@]}"; do
    mapfile -t dcp_pids < <(pgrep -f -- "--monitor ${apphost_pid}" || true)

    if ((${#dcp_pids[@]} > 0)); then
        for dcp_pid in "${dcp_pids[@]}"; do
            mapfile -t controller_pids < <(pgrep -P "$dcp_pid" || true)

            if ((${#controller_pids[@]} > 0)); then
                kill -TERM "${controller_pids[@]}" 2>/dev/null || true
            fi
        done

        kill -TERM "${dcp_pids[@]}" 2>/dev/null || true
    fi

done

kill -TERM "${apphost_pids[@]}" 2>/dev/null || true
echo "Stopped existing Aspire AppHost processes for ${repo_root}."
