# Profil « jour » — la boucle qui produit des lots.
#
# Six rôles, treize réveils, sur ForgeHistory. C'est le crontab d'avant,
# déplacé mot pour mot dans un fichier que `hermes` peut lire et que le
# répartiteur charge : les heures n'ont pas bougé, seul l'endroit où
# elles vivent a changé.
#
# Trois lots par jour, et non plus un : le pilote ne dépose qu'une carte
# par passage, le coder n'en consomme qu'une. Il faut donc autant de
# réveils que de lots. Un réveil sans travail sort sur RIEN avant tout
# agent : surprovisionner ne coûte rien.
#
# L'ordre compte : un pilote dépose, le coder qui suit ramasse.

export TZ=Europe/Paris
export PATH="/srv/ForgeHistory/.venv/bin:/home/hermes/.local/bin:/usr/local/bin:/usr/bin:/bin"
export ATELIER_PROJET="${ATELIER_PROJET:-/srv/ForgeHistory}"
export ATELIER_LOGS="${ATELIER_LOGS:-/home/hermes/.atelier/logs}"
export ATELIER_TIMEOUT="${ATELIER_TIMEOUT:-1800}"

# L'armement vit ici, pas dans `/etc/cron.d`. C'est ce qui rend la
# bascule possible sans root : désarmer, c'est changer de profil.
# Le retirer (ou le mettre à 0) remet le pipeline en mode à sec, sans
# rien casser : les cartes restent où elles sont.
export ATELIER_INVOQUER=1

# Un agent, un worktree (voir crons/installer-profils.sh --dry-run).
# Un lot actif a son worktree, et son chemin se dérive du nom du
# produit et du slug : /srv/ForgeHistory-047-le-bourg. Il n'y a plus
# un répertoire par rôle — un répertoire par rôle ne peut pas être
# sur deux branches à la fois.
export ATELIER_WORKTREES=/srv

# Facultatif : si llmquota est installé, il est lu tout seul. Sinon
# ATELIER_QUOTA_CMD nomme la commande qui rend un entier (-1 = inconnu).
# export ATELIER_QUOTA_CMD="llmquota restant"
# export ATELIER_RESERVE_planifier=1   # Grok laisse sa marge à Composer

# heure locale → rôle. Une ligne, un réveil.
REVEILS=(
    "06:15 veille"
    "07:00 pilote"
    "07:30 coder"
    "08:30 briefer"
    "09:00 pilote"
    "09:30 coder"
    "10:00 planifier"
    "11:00 pilote"
    "11:30 coder"
    "13:00 relire"
    "15:00 relire"
    "17:00 relire"
    "19:00 relire"
)

roles_du_moment() {
    local maintenant="$1" ligne
    for ligne in "${REVEILS[@]}"; do
        [[ "${ligne%% *}" == "$maintenant" ]] && echo "${ligne##* }"
    done
    # Une minute sans réveil est le cas courant, pas une panne : sans ce
    # retour, la fonction rendrait le code du dernier test de la boucle.
    return 0
}

prochain_reveil() {
    # Le prochain de la journée, ou le premier de demain. On compare des
    # minutes depuis minuit : deux chaînes « HH:MM » se comparent mal dès
    # qu'on veut savoir laquelle vient après.
    local maintenant="${1:-$(date +%H:%M)}"
    local courante=$((10#${maintenant%%:*} * 60 + 10#${maintenant##*:}))
    local meilleur="" delta_min=99999 ligne heure minutes delta
    for ligne in "${REVEILS[@]}"; do
        heure="${ligne%% *}"
        minutes=$((10#${heure%%:*} * 60 + 10#${heure##*:}))
        delta=$(( (minutes - courante + 1440) % 1440 ))
        [[ $delta -eq 0 ]] && delta=1440
        if [[ $delta -lt $delta_min ]]; then
            delta_min=$delta
            meilleur="$ligne"
        fi
    done
    echo "$meilleur (dans ${delta_min} min)"
}
