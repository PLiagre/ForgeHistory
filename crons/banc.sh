#!/usr/bin/env bash
# Le produit d'épreuve du profil « atelier » : un dépôt jetable, ses
# worktrees de rôle, et de faux agents.
#
# Jamais ForgeHistory. Un banc sert à voir un tour complet en minutes ;
# s'il touchait le produit, il faudrait relire chaque essai avant de le
# lancer, et on ne lancerait plus rien.
#
# Les faux agents obéissent à l'environnement plutôt qu'à un modèle :
# FAUX_CODE (le code de sortie), FAUX_DORT (les secondes), FAUX_PR (ce
# qu'il écrit dans pr.txt), FAUX_SALIT (le fichier qu'il laisse traîner),
# FAUX_COMMIT (le fichier qu'il commite). C'est ce qui permet de rejouer
# un délai dépassé ou un agent qui plante sans dépenser un centime.
set -euo pipefail

BANC="${ATELIER_BANC:-$HOME/.atelier/banc}"
NEUF=0
[[ "${1:-}" == "--neuf" ]] && NEUF=1

if [[ -d "$BANC/produit/.git" && $NEUF -eq 0 ]]; then
    echo "banc déjà monté : $BANC (--neuf pour le refaire)"
    exit 0
fi

rm -rf "$BANC"
mkdir -p "$BANC/faux"

cat > "$BANC/faux/_agent" <<'FAUX'
#!/usr/bin/env bash
echo "[faux $(basename "$0")] $# argument(s)" >&2
if [ -n "${FAUX_DORT:-}" ]; then sleep "$FAUX_DORT"; fi
# Par défaut le faux agent fait ce qu'un agent qui a réussi fait : il
# dépose un numéro de PR. Une boucle dont le cas nominal échoue ne
# montre jamais un tour complet, et c'est pour voir un tour complet
# qu'elle existe. FAUX_SANS_PR=1 rejoue l'agent qui n'écrit rien.
if [ -z "${FAUX_SANS_PR:-}" ]; then
    mkdir -p "$PWD/atelier-echange"
    printf '%s' "${FAUX_PR:-$(( (RANDOM % 9000) + 1000 ))}" > "$PWD/atelier-echange/pr.txt"
fi
if [ -n "${FAUX_SALIT:-}" ]; then echo "sale" > "$PWD/$FAUX_SALIT"; fi
if [ -n "${FAUX_COMMIT:-}" ]; then
    mkdir -p "$(dirname "$PWD/$FAUX_COMMIT")"
    echo "travail" > "$PWD/$FAUX_COMMIT"
    git -c user.email=faux@banc -c user.name=Faux add -A >/dev/null 2>&1 || true
    git -c user.email=faux@banc -c user.name=Faux commit -qm "faux travail" >/dev/null 2>&1 || true
fi
exit "${FAUX_CODE:-0}"
FAUX
chmod +x "$BANC/faux/_agent"
# Les binaires que `[roles]` peut nommer, plus `gh` : aucun d'eux ne
# doit pouvoir être le vrai. Un lien par nom, tous vers le même faux.
for b in agent claude codex hermes gh; do ln -sf _agent "$BANC/faux/$b"; done

PROD="$BANC/produit"
mkdir -p "$PROD/briefs"

cat > "$PROD/atelier.toml" <<'TOML'
# Branchement du banc. Le relecteur est `codex` : sur le banc, ce qui
# compte est que l'exécution et le contrôle diffèrent, pas qui les tient.
[projet]
nom = "Banc"
briefs = "briefs"
tests = "true"
fumee = "true"
branche_base = "master"
prefixe_branche = "agent/"
feuille = "ROADMAP.md"

[roles]
ecriture = "claude"
execution = "cursor"
controle = "codex"
TOML

cat > "$PROD/ROADMAP.md" <<'MD'
# Feuille de route du banc

Deux lots, et ils reviennent : `crons/banc.sh --neuf` remet tout à zéro.

<!-- lots:debut -->

### [900 — Le banc mesure](briefs/900-le-banc-mesure.md)
état : pret · couche : 1 · dépend de : — · PR : —

### [901 — Le banc mesure encore](briefs/901-le-banc-mesure-encore.md)
état : a-briefer · couche : 1 · dépend de : — · PR : —

<!-- lots:fin -->
MD

# Un brief qui passe la porte, et dont le périmètre se lit par
# `cycle._fichiers_du_perimetre` — pas seulement par `atelier portes`.
# Les deux ne comptent pas la même chose : la porte compte les backticks,
# le verrou découpe en phrases et écarte celles qui disent « interdit ».
cat > "$PROD/briefs/900-le-banc-mesure.md" <<'MD'
# Brief 900 — le banc mesure

## But

Éprouver la plomberie de l'atelier sans dépenser un quota.

## Règle du monde

Un banc ne produit rien : il mesure.

## Périmètre

Le lot écrit dans `banc/atelier.py`.
Tout autre chemin est interdit, nommément `banc/interdit.py`.

## Conditions de succès

### SC1 — le fichier existe

`python3 -c "print(1)"` rend 1.

## Hors périmètre

Le reste du dépôt.
MD

git -C "$PROD" init -q
git -C "$PROD" config user.email atelier@banc
git -C "$PROD" config user.name Banc
git -C "$PROD" config commit.gpgsign false
git -C "$PROD" checkout -qB master
git -C "$PROD" add -A
git -C "$PROD" commit -qm "banc"

for r in briefer planifier coder relire; do
    git -C "$PROD" worktree add -q "$BANC/produit-$r" -b "atelier/$r" master
done

echo "banc monté : $BANC"
