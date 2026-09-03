# Profil « atelier » — la boucle pour travailler sur l'atelier lui-même.
#
# Un tour complet de ForgeHistory prend une journée : treize réveils
# étalés de 6h15 à 19h. Pour corriger la plomberie, c'est une journée
# par essai. Ce profil rejoue le même cycle en quatre minutes, sur un
# produit d'épreuve monté par `crons/banc.sh` — jamais sur ForgeHistory.
#
# Il ne consomme aucun quota, et pas par convention : le PATH commence
# par les faux binaires du banc. Un vrai `agent` n'est pas *choisi de ne
# pas être appelé*, il est *hors de portée*. C'est la seule garantie qui
# tienne quand on relance cent fois.

BANC="${ATELIER_BANC:-$HOME/.atelier/banc}"

export TZ=Europe/Paris
# Les faux agents d'abord : rien de payant ne peut être trouvé.
export PATH="$BANC/faux:/srv/ForgeHistory/.venv/bin:/home/hermes/.local/bin:/usr/local/bin:/usr/bin:/bin"
export ATELIER_PROJET="$BANC/produit"
export ATELIER_LOGS="${ATELIER_LOGS:-$HOME/.atelier/logs-banc}"
export ATELIER_VERROUS="$BANC/verrous"
# Court : un agent qui dort plus de trente secondes sur un banc est un
# agent qu'on veut voir dépasser son délai.
export ATELIER_TIMEOUT="${ATELIER_TIMEOUT:-30}"
export ATELIER_INVOQUER=1
# Le pilote ne va pas chercher le réseau pour un produit qui n'a pas de
# remote : sans ça, chaque tour attend un `git pull` qui échouera.
export ATELIER_SANS_PULL=1

export ATELIER_WORKDIR_briefer="$BANC/produit-briefer"
export ATELIER_WORKDIR_planifier="$BANC/produit-planifier"
export ATELIER_WORKDIR_coder="$BANC/produit-coder"
export ATELIER_WORKDIR_relire="$BANC/produit-relire"

# Le cycle : quatre minutes, quatre gestes. Le pilote dépose, le coder
# ramasse, le relecteur relit, le briefer écrit — l'ordre du profil
# « jour », resserré. La minute de l'heure suffit à cadencer : pas
# d'horloge de plus à tenir, et le cycle repart de lui-même à chaque
# heure ronde.
CYCLE=(pilote coder relire briefer)

roles_du_moment() {
    local maintenant="$1"
    local minute=$((10#${maintenant##*:}))
    echo "${CYCLE[$((minute % ${#CYCLE[@]}))]}"
}

prochain_reveil() {
    local maintenant="${1:-$(date +%H:%M)}"
    local minute=$((10#${maintenant##*:}))
    local suivant=$(((minute + 1) % ${#CYCLE[@]}))
    echo "dans 1 min : ${CYCLE[$suivant]} (cycle de ${#CYCLE[@]} min)"
}
