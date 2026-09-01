# Brief 001 — Profils Hermes et invocation réelle

## But

Que l'atelier sache **invoquer** Claude Code et Cursor CLI depuis un
cron, chaque rôle dans son profil Hermes, sans qu'un agent en attende
un autre. Après ce lot, `ATELIER_INVOQUER=1 /opt/ForgeAtelier/crons/tour.sh coder`
lance Composer **ou** sort `RIEN` (code 0) si la boîte est vide.

## Règle du monde

Aucun fondement dans `sim/MODELE.md`. Ce lot ne change aucun nombre
du jeu. Il implémente [docs/MISE-EN-PLACE.md](../docs/MISE-EN-PLACE.md)
et [ANALYSE.md](../ANALYSE.md).

**Fidélité : hors jeu.**

Le pilote Hermes dépose une carte et s'arrête. Claude Pro facture
`claude -p`, Cursor Pro facture `agent -p`. Hermes n'est pas le
cerveau de Claude ni de Cursor.

Grok n'est pas sur le chemin de Composer.

## Périmètre

Écriture autorisée, et rien d'autre :

- `atelier/backends.py`
- `atelier/boite.py`
- `atelier/__main__.py`
- `crons/tour.sh`
- `crons/pilote.sh`
- `crons/veille.sh`
- `crons/installer-profils.sh` (nouveau)
- `docs/MISE-EN-PLACE.md`
- `ROADMAP.md`
- `tests/test_boite.py`
- `tests/test_invocation.py` (nouveau, ajoute des cas au fichier
  s'il existe déjà ; n'en crée un **que** s'il n'existe pas —
  l'invariant est « une invocation hors contrat refuse »)

Interdit : `sim/`, `viewer/`, `data/`, `VISION.md` du jeu,
fusionner, invoquer un agent pendant les tests CI.

## Conditions de succès

### SC1 — une boîte vide n'est pas un échec

```bash
python3 -m atelier prochain --projet /tmp --role coder
# stdout = RIEN, code = 0
```

### SC2 — Composer ne dépend pas de Grok

Un test pose une carte dans `a-planifier/` et une dans `a-coder/`.
`prochain --role coder` rend la carte coder. Le planificateur a
toujours la sienne.

### SC3 — fusionner refuse

```bash
python3 -m atelier fusionner
# code = 2
```

### SC4 — sans ATELIER_INVOQUER, aucun binaire d'agent n'est lancé

```bash
ATELIER_INVOQUER=0 ./crons/tour.sh coder
# imprime l'invocation, exit 0, `pgrep -a claude` / `pgrep -a agent` inchangé
```

### SC5 — installer les profils est une commande, pas une cérémonie orale

```bash
./crons/installer-profils.sh --dry-run
# imprime hermes profile create pilote|briefer|coder|relire
# n'écrit rien sous ~/.hermes
```

## Hors périmètre

- Créer le dépôt GitHub `PLiagre/ForgeAtelier` (jeton sans droit).
- Installer Superpowers / llmquota sur le VPS (le script peut
  *imprimer* les commandes, pas les exécuter sans drapeau).
- Un conseil, Mem0, E2B, Browser Use, Qwen, Goose.
- Toute fusion. Toute invocation dans la CI.
