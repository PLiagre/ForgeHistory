---
author: hermes
kind: rapport
created_at: 2026-08-12T09:00:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Mise en place du workflow à quatre acteurs

Premier rapport de ce dossier — il inaugure le format et enregistre l'état
au moment où Hermes devient chef de projet (ADR-0010).

## Ce qui est en place

- La feuille de route [ROADMAP.md](../../ROADMAP.md) existe et reflète
  l'état réel : F0 terminé, F1 en cours, `sim/` non commencé.
- La chaîne Hermes → Claude (CTO) → Codex (exécutant, GPT-5.6 Sol) →
  Cursor (critique) est décidée et documentée.
- Les trois workflows d'invocation d'agents sont câblés (plus aucun stub
  `TODO(operator...)`) ; ils attendent les secrets pour agir et consignent
  une dérogation tant qu'ils manquent.
- Le dépôt est nettoyé : plus de branche fusionnée qui traîne, plus de PR
  brouillon en attente, les quatre audits obsolètes sont archivés.

## Ce qui bloque encore

1. Les identifiants CI ne sont pas provisionnés — action propriétaire.
   Décision du 2026-08-12 : en **quota d'abonnement**, jamais en crédit
   API — `CLAUDE_CODE_OAUTH_TOKEN` (jeton `claude setup-token`),
   `CODEX_AUTH_JSON` (fichier `codex login`), `CURSOR_API_KEY` (clé Cloud
   Agents, déjà adossée au forfait Cursor).
2. Le gate des briefs 004/005 (visuel carte) exige des logs Unity que seule
   la machine propriétaire peut produire.

## Prochaine étape recommandée

Voir ROADMAP.md § « Prochaines étapes » — dans l'ordre : secrets, boucle
rejouée sur un brief réel, premier brief F2.
