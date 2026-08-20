# ADR-0015: les trois capacités d'Hermes — sous-agents, crons, issues

**Date**: 2026-08-19
**Status**: accepted
**Deciders**: le propriétaire (décision), Claude Code (rédaction, rôle CTO)

Amendement 001 (2026-08-20, décision propriétaire, ADR-0016) : les crons
quotidiens de lecture / mesure / proposition sont autorisés dès maintenant.
Le bilan écrit des lots `021`–`023` n’est plus un préalable. La règle 2
ci-dessous reste vraie sur le fond : **aucun cron ne fusionne**. Le
déverrouillage ne passe plus par ce bilan.

Complète ADR-0014, qui partage les rôles sans dire ce qu'Hermes a le droit
d'employer pour tenir le sien. Ne remplace ni ADR-0013 ni ADR-0014.

## Contexte

Le `2026-08-19`, Hermes quitte le PC du propriétaire pour un VPS Linux dédié,
joignable par Discord. ADR-0014, accepté le `2026-08-16`, lui confie déjà
déclencher et rendre compte. Trois capacités de son outillage deviennent alors
utilisables, et **aucune n'est encadrée par le dépôt** : la délégation à des
sous-agents en parallèle, les tâches planifiées, et la création d'issues GitHub.

Chacune rencontre une règle déjà écrite. Les trois rencontres ont été vérifiées
dans le dépôt le `2026-08-19`, sur `master` = `88864b6`.

**Les sous-agents touchent l'interdiction de juger.** L'usage le plus vanté de
la délégation — relire un travail avec un regard neuf — est précisément celui
qu'ADR-0014 retire à Hermes. Le dépôt avait de plus déjà écarté nommément
l'évaluation par un sous-agent engendré par le producteur, au motif que
*le producteur cadrerait son juge*.

**Les crons touchent une interdiction dont la condition a expiré.** ADR-0013
n'interdit pas les tâches planifiées indéfiniment : sa formule est « **pendant
trois lots pilotes** : aucun cron, aucun auto-merge et une seule tâche ». Les
trois lots (`021`, `022`, `023`) sont livrés, le dernier depuis le `2026-08-16`.
La condition est donc éteinte — mais le bilan écrit qui clôt le pilote n'existe
pas, et personne n'a prononcé cette clôture.

**Les issues touchent la règle la mieux protégée, par un angle qu'elle ne voit
pas.** Le dépôt impose qu'exactement un document dise à un agent ce qu'il doit
faire : le brief. `harness/tests/test_single_source_of_instruction.py` la
protège — mais il ne parcourt que les fichiers `.md` **du dépôt**. Une issue
GitHub lui est invisible. Une « issue prête pour un agent » serait donc une
seconde source d'instruction qu'aucun contrôle ne verrait passer.

## Décision

Trois règles, une par capacité.

**1. Les sous-agents lisent, ils ne jugent pas.** Un sous-agent lancé par Hermes
est Hermes : il hérite de son interdiction de juger. Lui sont ouverts la
lecture, la reconstruction de mesures, la comparaison de sources, la recherche
de contre-exemples. Lui est fermée toute appréciation de la recevabilité d'un
lot. Et **un seul agent écrit** : les sous-agents rendent du texte, l'agent
principal écrit le fichier — Hermes n'écrivant lui-même que `ROADMAP.md` et
`hermes/**`.

**2. Les crons lisent, mesurent et proposent ; ils ne fusionnent pas.**
ADR-0016 autorise un cron quotidien dès le `2026-08-20`. Le premier cron
est en lecture / mesure / proposition (contrat : `hermes/crons/`).
**Aucun cron ne fusionne**, n'écrit du code produit, ni n'instruit un
exécutant.

**3. Une issue pointe vers un brief, elle ne le récrit pas.** Une issue créée
par Hermes porte un titre, un contexte et **la référence du brief** qui fait
autorité. Elle ne porte ni condition de succès, ni critère d'acceptation, ni
consigne à un exécutant. C'est la règle du dépôt appliquée telle quelle :
*tout fichier peut pointer vers le brief, aucun ne le paraphrase*.

## Alternatives Considered

### Alternative 1 : tout autoriser, et s'en remettre à la discipline
- **Pros** : rien à écrire ; les trois capacités sont utilisables aujourd'hui ;
  le propriétaire est seul utilisateur et connaît les règles.
- **Cons** : le dépôt a déjà mesuré ce que valent les règles que rien ne
  vérifie. `HANDOFF.md` consigne ce que la boucle sans garde a produit — fusions
  sans contre-audit, rôles committant malgré l'interdiction.
- **Why not** : la capacité la plus dangereuse des trois, l'issue, est
  précisément celle qu'aucune mécanique n'attrape. Une règle non vérifiée y est
  une promesse, pas une garde.

### Alternative 2 : tout interdire jusqu'à ce que la mécanique existe
- **Pros** : aucune régression possible ; cohérent avec la prudence des ADR
  `0006` et `0007`.
- **Cons** : bloquerait le bilan des trois lots lui-même, qui est la meilleure
  première mission de délégation qu'on puisse imaginer — trois reconstructions
  indépendantes, aucune appréciation.
- **Why not** : interdire la lecture parallèle ne protège de rien. Le risque ne
  vient pas de lire à trois, il vient de juger ou d'instruire.

### Alternative 3 : étendre d'abord le contrôle mécanique aux corps d'issues
- **Pros** : ferme l'angle mort au lieu de le documenter ; c'est la réponse
  fidèle à l'esprit du dépôt.
- **Cons** : c'est un brief à écrire et à faire exécuter ; rien n'avance
  pendant ce temps.
- **Why not** : retenu comme **suite**, pas comme préalable. Tant que l'usage
  reste le suivi, la règle 3 suffit ; le jour où une issue doit porter plus, le
  contrôle doit exister d'abord.

## Consequences

### Positive
- Les trois capacités deviennent utilisables sans qu'aucune ne contourne une
  règle acquise.
- Le bilan des trois lots gagne une méthode : trois résultats indépendants,
  synthétisés par Hermes, dont aucun n'est un jugement.
- La séparation producteur / juge devient **mécaniquement vraie** dans le
  nouveau partage : Cursor produit, Claude juge, les acteurs diffèrent
  réellement — là où le backend natif écrivait des rôles nus que la porte ne
  savait pas distinguer.

### Negative
- Trois règles de plus à connaître, dont une seule est vérifiée par la machine.
- La règle 3 bride volontairement une capacité que l'outillage d'Hermes offre
  en entier.

### Risks
- **Une issue déborde et devient une instruction.** Atténuation partielle : la
  règle 3 et le format imposé. Atténuation réelle : le brief nommé plus bas.
  Le risque reste ouvert tant qu'il n'est pas écrit.
- **Un sous-agent rend un jugement déguisé en mesure.** Atténuation : la
  frontière porte sur le *résultat demandé*, pas sur l'intention — une mission
  dont le livrable est une appréciation est refusée à l'énoncé.
- **Le bilan est écrit pour déverrouiller les crons plutôt que pour dire vrai.**
  Atténuation : il conclut par une proposition qui peut être *retirer le
  pilote* ; un bilan qui ne peut conclure qu'au maintien n'est pas un bilan.

## Ce que cet ADR ne décide pas

1. **Le plafond mensuel Claude.** Point ouvert d'ADR-0014, toujours sans chiffre.
2. **L'auto-fusion.** Le « full auto » demandé par le propriétaire suppose
   d'armer le verrou de fusion conditionnel spécifié par le lot `010c` et jamais
   activé. Il déroge à ADR-0014, qui garde le veto au propriétaire : ce sera un
   ADR à part, pas une conséquence de celui-ci.
3. **La gouvernance du canal Discord.** Qui peut ordonner, ce qui est tracé, ce
   qu'un message ne déclenche jamais. Cet ADR ne fait qu'en constater
   l'existence.

## Briefs que cet ADR appelle

Aucun n'est écrit ici ; ils sont nommés pour ne pas se perdre.

| objet | pourquoi c'est un brief | état |
|---|---|---|
| étendre le contrôle de la source unique aux corps d'issues écrits par Hermes | du code de contrôle, et la seule façon de fermer l'angle mort | à écrire |
| `forgepilot evaluate` : la commande qui produit un `verdict.md` | ADR-0014 confie le jugement à Claude sans lui donner de commande | à écrire |
| gouverner le canal Discord comme entrée traçable | du contrat, pas une décision d'architecture | à écrire |
