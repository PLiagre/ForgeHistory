#!/usr/bin/env bash
# Hermes : dépose une carte, ou s'arrête. Il n'invoque ni Claude ni
# Cursor, et il n'est le cerveau de personne — il tient l'identité et
# l'horloge. Comme tour.sh, il ne dépense rien sans ATELIER_INVOQUER=1.
set -euo pipefail

PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
export PYTHONPATH="${ATELIER}${PYTHONPATH:+:$PYTHONPATH}"
VERROUS="${ATELIER_VERROUS:-${TMPDIR:-/tmp}}"
DELAI="${ATELIER_TIMEOUT:-1800}"

if [[ -z "${ATELIER_VERROU_TENU:-}" ]]; then
    if command -v flock >/dev/null 2>&1; then
        mkdir -p "$VERROUS"
        set +e
        ATELIER_VERROU_TENU=1 flock -n -E 75 "$VERROUS/atelier-pilote.lock" "$0" "$@"
        code=$?
        set -e
        if [[ $code -eq 75 ]]; then
            echo "pilote : un tour est déjà en cours, on se recouche."
            exit 0
        fi
        exit $code
    fi
    echo "flock absent : le pilote tourne sans garde de concurrence." >&2
fi

nom_workdir="ATELIER_WORKDIR_pilote"
cd "${!nom_workdir:-$PROJET}"

python3 -m atelier invocation --role pilote --projet "$PROJET"

if [[ "${ATELIER_INVOQUER:-0}" != "1" ]]; then
    echo "ATELIER_INVOQUER n'est pas posé : aucun agent lancé."
    exit 0
fi

if ! command -v hermes >/dev/null 2>&1; then
    echo "hermes absent — RIEN" >&2
    exit 0
fi

mapfile -d '' -t argv < <(
    python3 -m atelier invocation --role pilote --projet "$PROJET" --nul
)

# OPENAI_API_KEY ferait payer l'API à l'unité au lieu de l'abo ChatGPT
# Plus (OAuth openai-codex). Les clés Anthropic n'ont rien à faire ici :
# Hermes n'est pas le cerveau de Claude.
exec env -u OPENAI_API_KEY -u OPENAI_BASE_URL -u ANTHROPIC_API_KEY \
         -u ANTHROPIC_AUTH_TOKEN -u CURSOR_API_KEY \
     timeout "$DELAI" "${argv[@]}"
