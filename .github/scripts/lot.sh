#!/usr/bin/env bash
# Une demande de lot devient une fiche au registre, en proposition.
#
# `outils saisie --ecrire` a déjà lu le formulaire et posé la fiche. Ici on
# l'emballe : une branche, un commit, une proposition, et un mot sur la
# demande pour dire où elle est partie.
#
#   DEPOT    proprietaire/nom
#   LIGNE    « lot NNN <souche> »
#   BASE     la branche d'arrivée (master)
#   ISSUE    le numéro de la demande, pour lui répondre
set -euo pipefail

: "${DEPOT:?DEPOT manquant}"
: "${LIGNE:?LIGNE manquante}"
BASE="${BASE:-master}"
ISSUE="${ISSUE:-}"

numero=$(echo "$LIGNE" | cut -d' ' -f2)
souche=$(echo "$LIGNE" | cut -d' ' -f3)
branche="feuille/$souche"

if git ls-remote --exit-code --heads origin "$branche" > /dev/null 2>&1; then
  echo "$branche existe déjà : la demande a déjà été traitée" >&2
  exit 1
fi

{
  printf "Le lot %s entre au registre, état « a-briefer ».\n\n" "$numero"
  printf "Il vient de la demande #%s. Le bon de travail reste à écrire : le\n" "${ISSUE:-?}"
  printf "poste d'écriture le fera, et sa proposition passera la fiche à « pret ».\n"
} > corps.txt
{ printf "Le lot %s entre au registre.\n\n" "$numero"; cat corps.txt; } > message.txt

# La même connexion que pour un palier : GitHub doit pouvoir relier ce
# commit à quelqu'un, sinon la relecture refuse avant de regarder quoi que
# ce soit (AGENTS.md, règle de rôle).
git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git checkout -b "$branche"
git add ROADMAP.md
git commit --file message.txt
git push -u origin "$branche"

lien=$(gh pr create --base "$BASE" --head "$branche" \
  --title "Lot $numero entre au registre" --body-file corps.txt)
numero_pr=${lien##*/}

# Une proposition ouverte par le jeton d'Actions ne déclenche aucun
# contrôle : GitHub refuse la récursion. On les demande donc nommément.
gh workflow run tests.yml --ref "$branche" -f base="$BASE" -f branche="$branche"
gh workflow run security.yml --ref "$branche"
gh workflow run relecture.yml --ref "$BASE" -f pr="$numero_pr"

if [ -n "$ISSUE" ]; then
  gh issue comment "$ISSUE" --body "Le lot $numero entre au registre : $lien"
  gh issue close "$ISSUE" --reason completed
fi
echo "lot $numero déposé par la proposition $numero_pr"
