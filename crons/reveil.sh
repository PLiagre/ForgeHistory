#!/usr/bin/env bash
# Réveil horaire : garde l'heure de Paris et conserve une trace lisible
# sans dépendre d'un serveur de courrier ni du journal système.
set -u

HEURE="${1:?usage: reveil.sh HH:MM <veille|pilote|briefer|planifier|coder|relire>}"
ROLE="${2:?usage: reveil.sh HH:MM <veille|pilote|briefer|planifier|coder|relire>}"
case "$ROLE" in
    veille|pilote|briefer|planifier|coder|relire) ;;
    *) echo "rôle inconnu : $ROLE" >&2 ; exit 2 ;;
esac

# Cron Debian suit le fuseau système UTC. Le crontab pose TZ=Europe/Paris :
# cette comparaison garde donc les heures locales pendant les changements
# saisonniers, sans changer le fuseau de tout le VPS.
if [[ "$(date +%H:%M)" != "$HEURE" ]]; then
    exit 0
fi

ATELIER="${ATELIER_ROOT:-$(cd "$(dirname "$0")/.." && pwd)}"
JOURNAUX="${ATELIER_LOGS:-$HOME/.atelier/logs}"
mkdir -p "$JOURNAUX"
exec >>"$JOURNAUX/$ROLE.log" 2>&1

date --iso-8601=seconds
case "$ROLE" in
    veille) "$ATELIER/crons/veille.sh" ;;
    pilote) "$ATELIER/crons/pilote.sh" ;;
    *) "$ATELIER/crons/tour.sh" "$ROLE" ;;
esac
code=$?
echo "$ROLE : code $code"
exit "$code"
