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

# Un lot, un répertoire. On ne sait pas encore lequel : on part du
# produit, et on ira dans le worktree du lot dès qu'on l'aura pris.
# Un worktree par rôle ne peut pas être sur deux branches à la fois, et
# c'est exactement ce que le cycle automatique demande.
WORKDIR="$PROJET"
cd "$WORKDIR"

# --- ce qui revient tout seul de echec/ ----------------------------------
# Une panne passagère — un délai dépassé, un agent qui plante — ne doit
# pas coûter une commande tapée par une personne. `rappeler` ne remet en
# circulation que ce qui se retente, et il le borne : voir atelier/reprise.py.
# Il ne fait jamais échouer le tour ; au pire il ne rappelle rien.
python3 -m atelier rappeler --projet "$PROJET" --role "$ROLE" || true

# --- la carte ------------------------------------------------------------
# On ne lit pas puis on écrit : on prend. `prendre` liste la boîte,
# déplace la première carte prenable vers `en-cours` et pose le verrou de
# ses ressources — le tout sous une même serrure, et tout ou rien. Le
# `prochain` d'avant laissait un intervalle entre la lecture et le
# verrou ; avec plusieurs tours d'un même rôle, cet intervalle est le cas
# nominal, pas une course rare.
#
# `prendre` sort 0 avec RIEN quand rien n'est libre : une file vide ou
# tenue n'est pas une panne.
if ! lot="$(python3 -m atelier prendre --projet "$PROJET" --role "$ROLE")"; then
    echo "$ROLE : boîte illisible, aucune carte prise." >&2
    exit 1
fi
if [[ "$lot" == "RIEN" ]]; then
    exit 0
fi

# La carte est sortie de la boîte du rôle. Elle doit y retourner sur
# TOUS les chemins de sortie — y compris ceux qu'on n'a pas prévus —
# sinon le prochain réveil ne la trouve plus. Un `trap` couvre ce qu'une
# liste de `if` oublierait toujours.
rendre_la_carte() {
    [[ -n "${lot:-}" && "$lot" != "RIEN" ]] || return 0
    python3 -m atelier rendre --projet "$PROJET" --role "$ROLE" --lot "$lot" \
        >/dev/null 2>&1 || true
}
trap rendre_la_carte EXIT

brief="$(python3 -m atelier carte --projet "$PROJET" --lot "$lot" --etat en-cours --champ brief)"
pr="$(python3 -m atelier carte --projet "$PROJET" --lot "$lot" --etat en-cours --champ pr)"

# Le numéro de PR est une coordonnée, pas une consigne : il dit au
# relecteur où regarder. S'il n'y en a pas, on nomme la branche.
inv=(--role "$ROLE" --projet "$PROJET" --lot "$lot" --brief "$brief")
if [[ -n "$pr" ]]; then
    inv+=(--pr "$pr")
fi

echo "carte $ROLE : $lot (prise ; verrou posé si ce rôle écrit)"
if ! python3 -m atelier invocation "${inv[@]}"; then
    echo "$ROLE : branchement illisible, aucun agent lancé." >&2
    exit 1
fi
echo -n "branche du lot  : "
python3 -m atelier branche --projet "$PROJET" --lot "$lot"
echo -n "worktree du lot : "
python3 -m atelier worktree --projet "$PROJET" --lot "$lot"

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
        # `echouer` lit la boîte du rôle : la carte y retourne d'abord.
        # `echouer_le_lot` n'est pas encore défini à ce point du script.
        rendre_la_carte
        python3 -m atelier echouer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
            --raison "brief introuvable : $brief" --cause brief-absent >/dev/null
        echo "$ROLE : brief introuvable ($brief). Rien n'a été dépensé." >&2
        exit 1
    fi
fi

echouer_le_lot() {
    # Une carte prise ne reste jamais en place. Le fichier d'échange
    # disparaît pour ne pas contaminer le lot suivant. Le verrou du
    # coder se lève sur tous les chemins d'échec.
    #
    # La cause est un mot, pas une phrase : c'est elle que `rappeler`
    # compare pour savoir si la carte revient seule. Une note se
    # réécrit, une cause se compare.
    local raison="$1"
    local cause="${2:-inconnue}"
    rm -f "$WORKDIR/atelier-echange/pr.txt"
    # `echouer` lit la boîte du rôle : la carte y retourne d'abord.
    rendre_la_carte
    python3 -m atelier echouer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
        --raison "$raison" --cause "$cause" >/dev/null
    if [[ "$ROLE" == "coder" ]]; then
        python3 -m atelier lever --projet "$PROJET" --lot "$lot" >/dev/null
    fi
    echo "$raison" >&2
    exit 1
}

# --- le répertoire du lot ------------------------------------------------
# Un lot actif a son worktree, sur sa branche. Deux lots ne se partagent
# plus un répertoire, et l'un ne salit plus celui de l'autre. Le chemin
# est dérivé du nom du produit et du slug : ce script ne le compose pas.
if ! WORKDIR="$(python3 -m atelier worktree --projet "$PROJET" --lot "$lot" --run)"; then
    echouer_le_lot "$ROLE : impossible de préparer le worktree du lot $lot" worktree
fi
cd "$WORKDIR"

# --- ce que la CI a déjà dit de la PR qu'on allait relire -----------------
# Le 3 septembre 2026, un agent a écrit « 164 passent, 3 échecs identiques
# à master (préexistants) ». La CI disait `sim` rouge et trois régressions.
# Rien ne l'a démenti, et le relecteur a été payé pour relire du code
# cassé — il l'a dit lui-même : « à confirmer par pytest ».
#
# Le script ne lit pas `gh` : il appelle la commande de l'atelier, seule à
# savoir comment un verdict se lit. Un inconnu n'est pas un vert — la
# carte reste dans `a-relire`, le tour sort 0, et l'un des quatre réveils
# de `relire` redemandera. Une carte qui attend se voit ; une porte qui
# s'ouvre toute seule ne se voit nulle part.
if [[ "$ROLE" == "relire" && -n "$pr" ]]; then
    set +e
    fautifs="$(python3 -m atelier ci --pr "$pr" --worktree "$WORKDIR")"
    verdict=$?
    set -e
    if [[ $verdict -eq 1 ]]; then
        echouer_le_lot "$ROLE : la PR $pr est rouge — $(echo $fautifs)" ci
    elif [[ $verdict -ne 0 ]]; then
        echo "$ROLE : verdict de la PR $pr illisible — la carte attend le prochain réveil."
        exit 0
    fi
    echo "$ROLE : PR $pr, contrôles obligatoires au vert."
fi

# --- le répertoire dans lequel l'agent va écrire --------------------------
# Le canal d'échange porte sa propre garde git (`*`). Sans elle, un agent
# qui fait `git add -A` enregistre `atelier-echange/pr.txt`, le tour le
# supprime ensuite, et le worktree reste sale : le lot suivant butait sur
# `preparer_lot`. Le tour *nominal* empoisonnait le lot d'après.
if ! python3 -m atelier canal --worktree "$WORKDIR" >/dev/null; then
    echo "$ROLE : canal d'échange impossible dans $WORKDIR" >&2
fi

# Puis on enregistre ce qu'un tour précédent a laissé traîner. On
# n'efface rien : c'est l'option « enregistre » que `preparer_lot`
# proposait déjà, prise toute seule plutôt que tapée par une personne.
if ! ranges="$(python3 -m atelier ranger --worktree "$WORKDIR")"; then
    echouer_le_lot "$ROLE : impossible de ranger $WORKDIR" worktree
fi
echo "$ranges"

# Seul le coder écrit du code : lui seul tient les ressources. Le verrou
# est déjà posé — `prendre` l'a fait dans le même geste que la prise de
# la carte, et il ne l'aurait pas prise sinon. Il n'y a donc plus
# d'intervalle entre « cette carte est à moi » et « ses fichiers sont à
# moi » : c'est tout l'objet de ce lot.
if [[ "$ROLE" == "coder" ]]; then
    # Un numéro périmé ne doit jamais être relu si l'agent n'écrit rien.
    rm -f "$WORKDIR/atelier-echange/pr.txt"
    # La branche est déjà extraite : `atelier worktree --run` l'a posée
    # en créant le répertoire du lot. On lit son nom pour vérifier, on ne
    # la prépare pas une seconde fois.
    if ! attendue="$(python3 -m atelier branche --projet "$PROJET" --lot "$lot")"; then
        echouer_le_lot "$ROLE : impossible de dériver la branche du lot $lot" branche
    fi
    if ! courante="$(git -C "$WORKDIR" branch --show-current)"; then
        echouer_le_lot "$ROLE : impossible de lire la branche courante de $WORKDIR" branche
    fi
    if [[ "$courante" != "$attendue" ]]; then
        echouer_le_lot "$ROLE : branche courante « $courante », attendue « $attendue » — aucun agent lancé" branche
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
            echouer_le_lot "$ROLE : $lot n'a pas déposé de numéro de PR valide dans atelier-echange/pr.txt" pr
        fi
        rm -f "$WORKDIR/atelier-echange/pr.txt"
        suite+=(--pr "$numero")
    elif [[ "$ROLE" == "briefer" ]]; then
        # Un code 0 ne suffit pas ici non plus. `brief-a-fusionner` dit
        # au propriétaire qu'il a une PR à fusionner : sans fichier de
        # brief et sans numéro, la carte lui promet un travail qui
        # n'existe pas. Le 4 septembre 2026, le lot 049 y est entré
        # ainsi — l'agent était bloqué sur une demande d'accord, il est
        # sorti 0, et le brief n'a jamais existé nulle part.
        if [[ "$brief" == /* ]]; then ecrit="$brief"; else ecrit="$WORKDIR/$brief"; fi
        if [[ ! -f "$ecrit" ]]; then
            echouer_le_lot "$ROLE : $lot est sorti sans écrire $brief — une carte ne passe pas sur parole" agent
        fi
        if ! numero="$(python3 -m atelier pr --fichier "$WORKDIR/atelier-echange/pr.txt")"; then
            echouer_le_lot "$ROLE : $lot n'a pas déposé de numéro de PR valide dans atelier-echange/pr.txt" pr
        fi
        rm -f "$WORKDIR/atelier-echange/pr.txt"
        suite+=(--pr "$numero")
    fi
    # `avancer` lit la boîte du rôle : la carte y retourne d'abord.
    rendre_la_carte
    if python3 -m atelier avancer --projet "$PROJET" --role "$ROLE" --lot "$lot" \
            ${suite[@]+"${suite[@]}"} >/dev/null; then
        echo "$ROLE : $lot avancé."
        exit 0
    fi
    # L'agent a tourné : la carte ne reste pas en place, sinon le rôle
    # la retrouve demain et la repaie demain.
    echouer_le_lot "$ROLE : $lot n'a pas pu avancer (la file suivante l'a déjà ?)" avancer
elif [[ $code -eq 124 ]]; then
    echouer_le_lot "$ROLE : délai dépassé (${DELAI}s)" timeout
else
    echouer_le_lot "$ROLE : l'agent a rendu le code $code" agent
fi
