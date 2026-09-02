#!/usr/bin/env bash
# Hermes : reçoit la décision du matin, il ne la prend pas.
#
# La décision — quelle carte déposer, quelle carte rapprocher, ou RIEN —
# est calculée par `atelier piloter` en Python, d'après la feuille de
# route du produit. Hermes n'invoque ni Claude ni Cursor, n'invente ni
# numéro de lot ni statut ; il tient l'identité et l'horloge, et résume
# au propriétaire ce que l'atelier a trouvé. Comme tour.sh, rien ne se
# dépose et rien ne se dépense sans ATELIER_INVOQUER=1.
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

# --- la feuille de route du jour -----------------------------------------
# Le pilote lit la feuille de master, pas celle d'hier : un lot fusionné
# la veille au soir doit être vu livré ce matin. Une avance rapide
# seulement ; si elle échoue (dépôt modifié, pas de réseau), on le dit et
# on décide sur ce qu'on a — on n'écrase rien.
if [[ "${ATELIER_SANS_PULL:-0}" != "1" ]] && git -C "$PROJET" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    if ! git -C "$PROJET" pull --ff-only --quiet 2>/dev/null; then
        echo "pilote : la feuille n'a pas pu être mise à jour (git pull --ff-only a échoué) — décision sur l'état local." >&2
    fi
fi

# --- la décision ---------------------------------------------------------
# Calculée en Python, à sec sans drapeau, pour de vrai avec. Le mode à sec
# est fait pour voir : la décision et l'invocation exacte qui partirait.
# Une feuille incohérente est un FAIL : rien n'est déposé, et c'est cela
# qu'Hermes reçoit à résumer au propriétaire.
if [[ "${ATELIER_INVOQUER:-0}" != "1" ]]; then
    set +e
    decision="$(python3 -m atelier piloter --projet "$PROJET" 2>&1)"
    code_decision=$?
    set -e
    printf '%s\n' "$decision"
    python3 -m atelier invocation --role pilote --projet "$PROJET" --decision "$decision"
    echo "ATELIER_INVOQUER n'est pas posé : aucune carte déposée, aucun agent lancé."
    exit "$code_decision"
fi

# --- sous drapeau : déposer, puis dire -----------------------------------
set +e
decision="$(python3 -m atelier piloter --projet "$PROJET" --run 2>&1)"
code_decision=$?
set -e
printf '%s\n' "$decision"

# Rien à déposer, rien à signaler : Hermes n'a rien à dire, on ne le paie pas.
if [[ $code_decision -eq 0 && "$decision" == "RIEN" ]]; then
    exit 0
fi

if ! command -v hermes >/dev/null 2>&1; then
    echo "hermes absent — la décision reste dans ce journal." >&2
    exit "$code_decision"
fi

mapfile -d '' -t argv < <(
    python3 -m atelier invocation --role pilote --projet "$PROJET" --decision "$decision" --nul
)

# OPENAI_API_KEY ferait payer l'API à l'unité au lieu de l'abo ChatGPT
# Plus (OAuth openai-codex). Les clés Anthropic n'ont rien à faire ici :
# Hermes n'est pas le cerveau de Claude.
set +e
env -u OPENAI_API_KEY -u OPENAI_BASE_URL -u ANTHROPIC_API_KEY \
    -u ANTHROPIC_AUTH_TOKEN -u CURSOR_API_KEY \
    timeout "$DELAI" "${argv[@]}"
code_hermes=$?
set -e
# Une feuille incohérente reste un échec du tour, même si Hermes l'a résumée.
if [[ $code_decision -ne 0 ]]; then
    exit "$code_decision"
fi
exit "$code_hermes"
