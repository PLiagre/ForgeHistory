#!/usr/bin/env bash
# Le répartiteur : la seule ligne de cron, et elle ne change plus.
#
# `/etc/cron.d/forgeatelier` appartient à root. Tant que le crontab
# portait les treize réveils et leur environnement, changer de cadence
# — ou seulement désarmer — demandait root. Le répartiteur renverse la
# charge : le crontab l'appelle chaque minute et ne dit rien d'autre ;
# c'est lui qui lit le profil actif dans un fichier que `hermes` écrit.
# Basculer devient l'écriture d'un fichier, pas une modification système.
#
# Un profil dit trois choses : quel environnement poser, quels rôles
# réveiller à cette minute, et quel est le prochain réveil. Rien de plus.
set -uo pipefail

ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
ETAT="${ATELIER_ETAT:-$HOME/.atelier}"
FICHIER_PROFIL="$ETAT/profil"
ARRET="arret"

profil_actif() {
    # Pas de fichier, fichier vide, fichier illisible : à l'arrêt. Le
    # défaut ne réveille personne — on n'arme jamais par accident.
    [[ -r "$FICHIER_PROFIL" ]] || { echo "$ARRET"; return; }
    local nom
    nom="$(head -n 1 "$FICHIER_PROFIL" 2>/dev/null | tr -d '[:space:]')"
    [[ -n "$nom" ]] || nom="$ARRET"
    echo "$nom"
}

chemin_profil() { echo "$ATELIER/crons/profils/$1.sh"; }

charger_profil() {
    local nom="$1" chemin
    chemin="$(chemin_profil "$nom")"
    if [[ ! -r "$chemin" ]]; then
        echo "profil inconnu : $nom (aucun $chemin)" >&2
        return 1
    fi
    # shellcheck disable=SC1090
    source "$chemin"
    for fonction in roles_du_moment prochain_reveil; do
        if ! declare -F "$fonction" >/dev/null; then
            echo "profil $nom incomplet : $fonction manque" >&2
            return 1
        fi
    done
}

# ------------------------------------------------------------------ le tour
main() {
    local nom
    nom="$(profil_actif)"
    if [[ "$nom" == "$ARRET" ]]; then
        exit 0
    fi
    charger_profil "$nom" || exit 2

    local maintenant
    maintenant="$(date +%H:%M)"
    local roles
    roles="$(roles_du_moment "$maintenant")"
    [[ -n "$roles" ]] || exit 0

    mkdir -p "${ATELIER_LOGS:-$ETAT/logs}"
    local role
    for role in $roles; do
        # Un rôle par processus, et le journal du rôle reçoit tout :
        # c'est `reveil.sh` qui tient cette promesse, on ne la refait pas.
        # L'heure qu'on lui passe est celle qu'on vient de lire : il la
        # revérifie, et deux lectures de la même minute concordent.
        "$ATELIER/crons/reveil.sh" "$maintenant" "$role" || true
    done
}

main "$@"
