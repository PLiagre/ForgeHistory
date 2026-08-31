# ADR-0017: Grok juge la PR, Claude témoin rare, fusion mécanique

> **Statut actuel — 2026-08-30 : Archive historique. Les règles de rôle, d'identité, de fournisseur, de relecture, de verdict, de porte, d'orchestration et de fusion décrites ci-dessous sont obsolètes et n'imposent plus rien.**

**Date**: 2026-08-23
**Status**: accepted
**Deciders**: le propriétaire (oui du 2026-08-23), Cursor Cloud (rédaction)

Amende ADR-0014 (qui juge, qui fusionne) et complète ADR-0015 (Hermes
ne fusionne toujours pas). Ne remplace pas la source unique d’instruction
(le brief) ni l’interdiction pour Hermes de juger un lot.

## Context

Le chemin nominal envoyait Claude Opus 5 sur chaque plan et chaque
revue. Le propriétaire veut moins de Claude, Composer seulement pour
le code, Grok 4.6 pour le plan et le juge de PR, et une fusion
automatique si les checks et ce juge sont verts. Claude reste le
meilleur témoin pour une tâche à très haute valeur, à utiliser
rarement. Opus 4.8 est plus faible qu’Opus 5 au même prix ; Fable 5
coûte le double pour un gain marginal.

Hermes doit lire l’avancement (`forgepilot status`) et relancer, sans
devenir juge.

## Decision

1. **Plan** : Cursor, modèle Grok 4.6 (`high` en R1, `xhigh` en R2).
2. **Code et itérations** : Cursor Composer 2.5 uniquement.
3. **Juge avant PR finale** : Cursor Grok 4.6 `xhigh`, invocation
   neuve, une fois par SHA.
4. **Claude** : hors du chemin quotidien. Témoin optionnel
   `forgepilot witness` = Opus 5 effort `high`. Fable seulement sur
   demande ou si ce témoin a déjà échoué sur le même type de
   question. Opus 4.8 n’est plus un choix.
5. **Fusion** : commande mécanique `forgepilot merge` si, sur **ce**
   SHA : juge `PASS`, checks GitHub requis verts, profil de tests du
   risque passé, pas de label d’arrêt. Hermes a toujours
   `can_merge = false`. Un nouveau commit annule le juge.
6. **Hermes principal** : `openai/gpt-5.4` via Nous Portal ;
   sous-agents `openai/gpt-5.4-mini`.

## Alternatives Considered

### Alternative 1 : retirer Claude entièrement
- **Pros** : plus simple, plus de plafond Claude.
- **Cons** : perd le regard d’une autre famille sur l’architecture et
  la sécurité.
- **Why not** : le propriétaire garde Claude pour la haute valeur.

### Alternative 2 : Fable 5 comme témoin
- **Pros** : plafond Anthropic.
- **Cons** : double prix, gain faible hors cas extrêmes.
- **Why not** : Opus 5 au même prix qu’Opus 4.8 suffit en témoin rare.

### Alternative 3 : Sol comme juge quotidien
- **Pros** : autre famille que Grok/Composer.
- **Cons** : même index que Grok, plus cher, hors pool Cursor.
- **Why not** : Grok `xhigh` est le juge quotidien ; Sol n’est pas
  réintroduit sans nouvelle décision.

## Consequences

### Positive
- Claude n’est plus le goulot de chaque lot.
- Auteur (Composer) et juge (Grok, contexte neuf) restent distincts.
- La fusion n’attend plus un clic si les portes sont vraiment vertes.

### Negative
- Plan et juge quotidiens sont la même famille (Grok). Le témoin
  Claude reste le contre-regard.
- La protection de branche GitHub doit accepter une fusion sans
  approbation humaine une fois les checks verts.

### Risks
- **Grok passe trop facilement.** Atténuation : checks mécaniques
  d’abord ; témoin Claude sur les sujets graves ; label d’arrêt.
- **Hermes clique la fusion.** Atténuation : `can_merge` reste faux ;
  seul `forgepilot merge` agit, après preuves.
- **Opus 5 déplaît encore.** Atténuation : il n’est plus le quotidien ;
  Fable reste l’exception nommée.
