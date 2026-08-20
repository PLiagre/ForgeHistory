# Propositions Hermes

Améliorations **proposées** par Hermes (session ou cron quotidien).
Ce n’est pas une file de briefs. Une proposition n’instruit personne.

## Règles

- Format : `PROPOSITION-AAAAMMJJ-<slug>.md`, frontmatter `kind: proposition`.
- Dire le constat, pourquoi ça compte, ce que le propriétaire pourrait
  demander ensuite. Pas de conditions de succès d’exécutant, pas de
  consigne de code.
- Si un brief existe déjà, **pointer** vers lui. Ne pas le recopier.
- Statuts : `OPEN` (à trancher) → `HANDED_TO_CTO` (brief demandé) →
  `REFLECTED_IN_ROADMAP` ou `CLOSED`.
- Un cron ne crée une proposition que s’il a **un constat nouveau**.
  Sinon il met à jour `DERNIERE-VEILLE.md` seulement. Ce fichier est
  **local** (gitignoré) : le cron ne doit jamais salir le dépôt.
