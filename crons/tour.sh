#!/usr/bin/env bash
# Un cron, un rôle. Si la boîte est vide : exit 0, pas d'agent.
# Personne n'appelle le cron suivant — c'est ce qui a brûlé le lot 035.
#
# Ce script ne compose aucune ligne de commande : `atelier invocation`
# la construit en Python, le script l'exécute. Et il ne l'exécute que
# sous ATELIER_INVOQUER=1.
set -euo pipefail

ROLE="${1:?usage: tour.sh <briefer|planifier|coder|relire>}"
case "$ROLE" in
    briefer|planifier|coder|relire) ;;
    *) echo "rôle inconnu : $ROLE" >&2 ; exit 2 ;;
esac

PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
export PYTHONPATH="${ATELIER}${PYTHONPATH:+:$PYTHONPATH}"
VERROUS="${ATELIER_VERROUS:-${TMPDIR:-/tmp}}"
DELAI="${ATELIER_TIMEOUT:-1800}"

# --- un flock par rôle, jamais un flock global ---------------------------
# Deux « briefer » ne se marchent pas ; un briefer et un coder tournent
# ensemble sans se parler. Si le rôle est déjà pris, on se recouche : la
# carte sera là au prochain réveil.
if [[ -z "${ATELIER_VERROU_TENU:-}" ]]; then
    if command -v flock >/dev/null 2>&1; then
        mkdir -p "$VERROUS"
        set +e
        ATELIER_VERROU_TENU=1 flock -n -E 75 "$VERROUS/atelier-$ROLE.lock" "$0" "$@"
        code=$?
        set -e
        if [[ $code -eq 75 ]]; then
            echo "$ROLE : un tour est déjà en cours, on se recouche."
            exit 0
        fi
        exit $code
    fi
    echo "flock absent : $ROLE tourne sans garde de concurrence." >&2
fi

# Chaque rôle dans son répertoire : un agent, un worktree.
nom_workdir="ATELIER_WORKDIR_${ROLE}"
WORKDIR="${!nom_workdir:-$PROJET}"
cd "$WORKDIR"

# --- la carte ------------------------------------------------------------
if ! carte="$(python3 -m atelier prochain --projet "$PROJET" --role "$ROLE")"; then
    echo "$ROLE : boîte illisible, aucun agent lancé." >&2
    exit 1
fi
if [[ "$carte" == "RIEN" ]]; then
    exit 0
fi
lot="$(python3 -m atelier prochain --projet "$PROJET" --role "$ROLE" --champ lot)"
brief="$(python3 -m atelier prochain --projet "$PROJET" --role "$ROLE" --champ brief)"
pr="$(python3 -m atelier prochain --projet "$PROJET" --role "$ROLE" --champ pr)"

# Le numéro de PR est une coordonnée, pas une consigne : il dit au
# relecteur où regarder. S'il n'y en a pas, on nomme la branche.
inv=(--role "$ROLE" --projet "$PROJET" --lot "$lot" --brief "$brief")
if [[ -n "$pr" ]]; then
    inv+=(--pr "$pr")
fi

echo "carte $ROLE : $carte"
if ! python3 -m atelier invocation "${inv[@]}"; then
    echo "$ROLE : branchement illisible, aucun agent lancé." >&2
    exit 1
fi
if [[ "$ROLE" == "coder" ]]; then
    echo -n "branche du lot : "
    python3 -m atelier branche --projet "$PROJET" --lot "$lot"
fi

# « Celui qui a écrit le code ne dit pas s'il est recevable » ne tient
# que si le relecteur n'a pas la main qui écrit. Si le binaire que le
# branchement désigne ne sait pas qu'on la lui retire, on le dit — dans
# les deux modes, parce que le mode à sec est fait pour voir.
if [[ "$ROLE" == "relire" ]]; then
    garde="$(python3 -m atelier poste --projet "$PROJET" --role relire --champ lecture_seule)"
    if [[ "$garde" != "tenue" ]]; then
        echo "relire : ce relecteur n'a pas de garde de lecture seule — il garde la main qui écrit." >&2
    fi
fi

# --- l'interrupteur ------------------------------------------------------
if [[ "${ATELIER_INVOQUER:-0}" != "1" ]]; then
    echo "ATELIER_INVOQUER n'est pas posé : aucun agent lancé."
    exit 0
fi

# --- la garde de quota, facultative --------------------------------------
# llmquota lit, il ne lance rien. S'il est absent, on continue : un quota
# inconnu vaut -1, il ne se compte pas comme 0. S'il dit 0, la carte reste
# où elle est et le rôle se recouche.
# Quel abonnement ce rôle consomme : le branchement du produit le dit,
# pas une table de ce script. Deux rôles peuvent tirer le même compteur.
if ! ABO="$(python3 -m atelier poste --projet "$PROJET" --role "$ROLE" --champ abo)"; then
    echo "$ROLE : branchement illisible, aucun agent lancé." >&2
    exit 1
fi
QUOTA_CMD="${ATELIER_QUOTA_CMD:-}"
if [[ -z "$QUOTA_CMD" ]] && command -v llmquota >/dev/null 2>&1; then
    QUOTA_CMD="llmquota"
fi
restant=-1
if [[ -n "$QUOTA_CMD" ]]; then
    brut="$($QUOTA_CMD "$ABO" 2>/dev/null || true)"
    if [[ "$brut" =~ ^-?[0-9]+$ ]]; then
        restant="$brut"
    else
        echo "$ROLE : quota $ABO inconnu — on ne le compte pas pour zéro." >&2
    fi
fi
# Le rôle facultatif laisse une marge au rôle critique du même abo :
# Grok (planifier) et Composer (coder) tirent le même Cursor Pro.
nom_reserve="ATELIER_RESERVE_${ROLE}"
if [[ "$ROLE" == "planifier" ]]; then
    reserve="${!nom_reserve:-1}"
else
    reserve="${!nom_reserve:-0}"
fi
if [[ "$restant" -ge 0 && "$restant" -le "$reserve" ]]; then
    echo "$ROLE : quota $ABO à $restant (réserve $reserve). Carte intacte."
    exit 0
fi

# --- ce qui doit exister avant de dépenser -------------------------------
# Le briefer écrit le brief : il est le seul à ne pas l'exiger.
if [[ "$ROLE" != "briefer" ]]; then
    if [[ "$brief" == /* ]]; then chemin_brief="$brief"; else chemin_brief="$PROJET/$brief"; fi
    if [[ ! -f "$chemin_brief" ]]; then
        python3 -m atelier echouer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
            --raison "brief introuvable : $brief" >/dev/null
        echo "$ROLE : brief introuvable ($brief). Rien n'a été dépensé." >&2
        exit 1
    fi
fi

echouer_le_lot() {
    # Une carte prise ne reste jamais en place. Le fichier d'échange
    # disparaît pour ne pas contaminer le lot suivant. Le verrou du
    # coder se lève sur tous les chemins d'échec.
    local raison="$1"
    rm -f "$WORKDIR/atelier-echange/pr.txt"
    python3 -m atelier echouer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
        --raison "$raison" >/dev/null
    if [[ "$ROLE" == "coder" ]]; then
        python3 -m atelier lever --projet "$PROJET" --lot "$lot" >/dev/null
    fi
    echo "$raison" >&2
    exit 1
}

# Seul le coder écrit du code : lui seul tient les fichiers.
if [[ "$ROLE" == "coder" ]]; then
    if ! python3 -m atelier verrouiller --projet "$PROJET" --role coder --lot "$lot"; then
        python3 -m atelier echouer --projet "$PROJET" --role coder --lot "$lot" \
            --raison "verrou refusé : un autre lot tient un fichier du périmètre" >/dev/null
        exit 1
    fi
    # Un numéro périmé ne doit jamais être relu si l'agent n'écrit rien.
    rm -f "$WORKDIR/atelier-echange/pr.txt"
    # La branche du lot, avant d'invoquer Cursor : le worktree du rôle
    # n'est pas la branche du lot.
    if ! attendue="$(python3 -m atelier branche --projet "$PROJET" --lot "$lot" \
            --worktree "$WORKDIR" --run)"; then
        echouer_le_lot "$ROLE : impossible de préparer la branche du lot $lot"
    fi
    if ! courante="$(git -C "$WORKDIR" branch --show-current)"; then
        echouer_le_lot "$ROLE : impossible de lire la branche courante de $WORKDIR"
    fi
    if [[ "$courante" != "$attendue" ]]; then
        echouer_le_lot "$ROLE : branche courante « $courante », attendue « $attendue » — aucun agent lancé"
    fi
fi

# --- l'invocation --------------------------------------------------------
mapfile -d '' -t argv < <(python3 -m atelier invocation "${inv[@]}" --nul)

# Une clé d'API bascule la facture de l'abonnement vers l'unité. On ne
# veut pas découvrir la réponse sur la facture : le cron les retire.
prefixe=(env -u ANTHROPIC_API_KEY -u ANTHROPIC_AUTH_TOKEN -u CURSOR_API_KEY
         -u OPENAI_API_KEY -u OPENAI_BASE_URL)
if command -v timeout >/dev/null 2>&1; then
    prefixe+=(timeout "$DELAI")
else
    echo "timeout absent : $ROLE tourne sans délai maximum." >&2
fi

set +e
"${prefixe[@]}" "${argv[@]}"
code=$?
set -e

# --- une carte prise ne reste jamais en place ----------------------------
if [[ $code -eq 0 ]]; then
    suite=()
    if [[ "$ROLE" == "coder" ]]; then
        # Un code 0 ne suffit pas : sans entier positif, la carte n'entre
        # jamais dans a-relire. On ne concatène pas les chiffres d'un texte.
        if ! numero="$(python3 -m atelier pr --fichier "$WORKDIR/atelier-echange/pr.txt" \
                --branche "$attendue" --worktree "$WORKDIR")"; then
            echouer_le_lot "$ROLE : $lot n'a pas déposé de numéro de PR valide dans atelier-echange/pr.txt"
        fi
        rm -f "$WORKDIR/atelier-echange/pr.txt"
        suite+=(--pr "$numero")
    elif [[ "$ROLE" == "briefer" && -f "$WORKDIR/atelier-echange/pr.txt" ]]; then
        if numero="$(python3 -m atelier pr --fichier "$WORKDIR/atelier-echange/pr.txt")"; then
            suite+=(--pr "$numero")
        else
            echo "$ROLE : atelier-echange/pr.txt ne portait pas un entier positif unique." >&2
        fi
        rm -f "$WORKDIR/atelier-echange/pr.txt"
    fi
    if python3 -m atelier avancer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
            ${suite[@]+"${suite[@]}"} >/dev/null; then
        echo "$ROLE : $lot avancé."
        exit 0
    fi
    # L'agent a tourné : la carte ne reste pas en place, sinon le rôle
    # la retrouve demain et la repaie demain.
    echouer_le_lot "$ROLE : $lot n'a pas pu avancer (la file suivante l'a déjà ?)"
elif [[ $code -eq 124 ]]; then
    echouer_le_lot "$ROLE : délai dépassé (${DELAI}s)"
else
    echouer_le_lot "$ROLE : l'agent a rendu le code $code"
fi
