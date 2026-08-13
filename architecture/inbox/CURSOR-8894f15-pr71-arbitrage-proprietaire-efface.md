---
audit_id:                CURSOR-8894f15-pr71-arbitrage-proprietaire-efface
auditor:                 cursor-cloud
target_branch:           forge-bot/review-CURSOR-16ff5ac-contre-audit-perdu-a-la-publication-31683198126
target_commit:           8894f1527615b7f2f38f099651f752f669a04b6d
created_at:              2026-08-13T11:11:22Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #71 — « challenge : revue de l'audit CURSOR-16ff5ac-contre-audit-perdu-a-la-publication »

PR relue : <https://github.com/PLiagre/ForgeHistory/pull/71>
Commit audité : `8894f15` (tête de la branche
`forge-bot/review-CURSOR-16ff5ac-…-31683198126`)
Fusion : squash sur `master` en `74e0349` (parent unique `cc1b34c`), donc
`8894f15` n'est **pas** un ancêtre de `master` — d'où le `target_branch`
ci-dessus, comme pour l'audit frère `CURSOR-ab0e7f0`.
Référentiel de critique : `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat) et le contrat
`architecture/agents/cursor-auditor.md`.

Cet audit **ne décide rien et n'autorise rien** : il propose. La recevabilité
reste au propriétaire / au policy engine (`architecture/README.md`,
ADR-0005/0006). Les trois flags `*_authorized` du frontmatter sont à `false`.

---

## 1. Résumé exécutif

La PR #71 ajoute **un seul fichier** de 102 lignes,
`architecture/reviews/CLAUDE-CURSOR-16ff5ac-contre-audit-perdu-a-la-publication.md` :
le contre-audit de Claude sur l'audit `CURSOR-16ff5ac`. Sur la forme c'est un
bon artefact : périmètre tenu (rien hors `architecture/reviews/**`), sept
lignes de verdict que la machine lit sans peine, mesures parfois coûteuses
rejouées, et une réfutation courageuse — la revue démonte le récit central de
l'audit qu'elle relit, au lieu de le tamponner.

Le problème n'est pas la qualité du texte. Il est que **la seule chose que la
revue demande vraiment n'existe pas pour la machine**. La revue conclut que le
constat 1 de l'audit est faux dans son récit, et porte trois questions
explicites au propriétaire (§ 3), dont « la sévérité P0 du constat 1 doit être
réexaminée ». Quinze secondes après la fusion, le moteur de politique a
enregistré `AUDIT_APPROVED` avec `retained_points: [1, 2, 3, 4, 5, 6, 7]` —
le point 1 retenu **en entier**, et aucune trace des trois arbitrages. J'ai
rejoué la chaîne en bac à sable : elle reproduit ces chiffres à l'identique, et
la variante où le point 1 porterait `NEEDS_OWNER` ne fait pas mieux — le point
disparaît alors purement et simplement. Les deux seules issues machine pour un
point nuancé sont donc « retenu comme s'il était confirmé » ou « évaporé ».

Deuxième fait, indépendant : ce document a été écrit à **08:49:57Z** et fusionné
à **11:00:01Z**, soit 2 h 10 plus tard, parce que `gh pr create` avait échoué
en silence — le défaut même que la revue confirme. Entre-temps le dépôt a
bougé, et trois de ses mesures sont devenues fausses : `25 passed` sur `sim/`
alors que la base de fusion en donne `35`, un extrait de `sim/engine.py` cité
mot pour mot alors que le correctif du lot 013 l'avait supprimé onze minutes
avant la fusion, et un arbitrage propriétaire réclamé sur un registre déjà
réparé. La revue justifie explicitement de ne pas avoir figé son arbre en
écrivant « aucun changement de code n'affecte ces sorties depuis `16ff5ac` » :
c'est cette phrase-là qui est fausse, et je la réfute par des nombres.

Troisième fait, de gouvernance : la PR a été fusionnée **28 secondes** après
son ouverture, 25 secondes après le lancement de son propre auditeur. Le
maillon que l'ADR-0010 déclare « critique » n'a pas pu peser — encore une
fois.

Je ne propose **qu'un seul brief**, et je dis en § 9 pourquoi les autres
constats, tout réels qu'ils soient, ne doivent pas gonfler la file : ils sont
déjà consignés dans des audits non arbitrés, ou tranchés par une décision
enregistrée.

## 2. Provenance et intention (lentille 1 : intention avant diff)

| Élément | Valeur mesurée |
|---|---|
| PR | #71, ouverte 2026-08-13T10:59:33Z, fusionnée 11:00:01Z par `PLiagre` |
| Commit audité | `8894f1527615b7f2f38f099651f752f669a04b6d`, auteuré par `forge-bot` le 2026-08-13T08:49:57Z |
| Diff | 1 fichier, +102 / −0, entièrement sous `architecture/reviews/**` |
| Revue non demandée | `reviews: []` (aucune revue GitHub humaine) |
| Fusion | squash → `74e0349`, parent unique `cc1b34c` |

**Intention déclarée.** Le corps de la PR est explicite et honnête : le
workflow `pipeline-challenge` (run 31683198126) a poussé la branche mais n'a pas
pu ouvrir la PR, le réglage GitHub « Allow GitHub Actions to create and approve
pull requests » étant inactif ; la PR a donc été ouverte à la main « sans
modification du contenu ». Il annonce aussi le contenu : « verdicts par point,
lignes de tableau : 5 CONFIRMED, 2 PARTIAL ». Ce chiffre-là est **juste** —
c'est la première fois dans cette série que le corps d'une PR de bot annonce
des verdicts que l'analyseur retrouve vraiment (voir § 5 (a)).

**Le bon problème ?** Oui : produire le contre-audit de `CURSOR-16ff5ac` est
exactement ce que la boucle attend, et le fichier livré est bien à sa place
(`reviews/` est le dossier de Claude, `architecture/README.md` § « Un seul rôle
écrit dans chaque dossier »). Les bonnes contraintes ? Non, sur un point
précis : la contrainte que la revue s'impose à elle-même en § 1 — « mesures
rejouées directement sur ce dépôt (pas dans un worktree séparé, mais sans
écriture) » — est contredite deux fois par son propre corps (§ P2-4), et la
fraîcheur des mesures n'est contrainte par rien du tout (§ P1-1).

## 3. Constats

### P0-1 — Fusionnée 28 secondes après son ouverture : l'auditeur « critique » d'ADR-0010 n'a pas pu peser

Chronologie mesurée, à la seconde :

| Horodatage | Événement |
|---|---|
| 10:59:33Z | PR #71 ouverte (`gh pr view 71 --json createdAt`) |
| 10:59:36Z | `pipeline-audit` démarre le job `invoke-cursor-auditor` |
| 11:00:01Z | PR fusionnée en squash (`mergedAt`, `mergedBy: PLiagre`) |
| 11:00:16Z | `AUDIT_CHALLENGED` **et** `AUDIT_APPROVED` écrits au registre |

Le job `invoke-cursor-auditor` sort `SUCCESS` parce qu'il a **lancé** un Cloud
Agent, pas parce qu'un audit existe : le présent fichier est écrit après la
fusion. `.github/workflows/pipeline-audit.yml` se déclenche bien sur chaque
`pull_request` non-brouillon, conformément au contrat
(`architecture/agents/cursor-auditor.md` § Déclencheur), mais rien dans la
chaîne n'attend son résultat. La critique est donc structurellement
post-mortem.

**Pas de brief.** Ce motif est déjà retenu : proposition 1 du § 8 de
`CURSOR-a600532-fusion-sans-contre-audit`, audit passé `AUDIT_APPROVED`, et
`CURSOR-ab0e7f0` P0-1 (6 secondes, mesure du 2026-08-13). Le rouvrir serait du
bruit (`review-guidelines.md` § « Pas de rubber-stamping inverse »).

**Élément nouveau, en revanche** : la conversion censée traiter ce motif a
produit `harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md`, une
graine qui liste **17 points retenus** et dont *toutes* les sections utiles
sont encore `<<TODO (planificateur)>>` (`Authored: 2026-08-13T08:40:34Z`). La
décision est donc enregistrée, mais rien n'est encore instruit — ce qui est
cohérent avec le § P1-2 ci-dessous : retenir dix-sept points d'un bloc ne
produit pas un lot atomique, il produit un formulaire vide.

### P1-1 — Le document fusionné mesure un dépôt qui n'existait plus : trois preuves numériques de la dérive

Entre la rédaction (08:49:57Z, date d'auteur du commit) et la fusion
(11:00:01Z) il s'est écoulé **2 h 10**, et deux fusions ont changé le code que
la revue cite : PR #65 à 10:47:51Z et PR #69 (lot 013, « le tick nourrit une
fois ») à 10:48:46Z. Trois mesures de la revue sont devenues fausses.

**(a) `25 passed` contre `35 passed`.** La ligne 7 du tableau affirme :
« Ré-exécuté directement sur `master` actuel … `python3 -m pytest sim/tests/ -q`
→ `25 passed in 1.61s` ». Rejeu sur `cc1b34c`, qui est exactement le parent de
la fusion de cette PR :

```
$ git rev-parse --short HEAD
cc1b34c
$ .venv/bin/python -m pytest sim/tests/ -q | tail -1
35 passed in 2.11s
```

Dix tests d'écart : le lot 013 a ajouté `test_survie_derivee.py`,
`test_kg_transportes_est_arrives.py`, `test_mortalite_continue.py` et
`test_tick_nourrit_une_fois.py`, aucun présent dans l'arbre `16ff5ac`
(`git ls-tree --name-only 16ff5ac sim/tests/`, sortie complète en § 5 (d)).

**(b) Le code cité au constat 3 n'existe plus.** La ligne 3 cite « `sim/engine.py:_apply_commerce`, lignes
`cell_b.food_stock_kg += transfer` **et** `cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)`
dans la même passe ». Cette ligne est bien là à `16ff5ac`
(`sim/engine.py:95` et `:107`), et elle a disparu de `master` : le fichier y
porte désormais, en tête, « Le maillon commerce ne modifie plus
`food_deficit_kg` (SC1) » et, en `sim/engine.py:161`, le commentaire « Passe 2 :
appliquer tous les transferts (jamais `food_deficit_kg`) ». Autrement dit la
revue confirme un défaut en citant un code que le correctif avait supprimé
onze minutes avant la fusion de la revue.

**(c) La justification que la revue se donne est précisément ce qui est faux.**
La ligne 7 assume de ne pas figer son arbre, entre parenthèses : « pas un
worktree figé, mais **aucun changement de code n'affecte ces sorties depuis
`16ff5ac`** ». (a) et (b) réfutent cette phrase. Les deux autres nombres de la
même ligne, eux, tiennent : `314 passed, 16 skipped` et `SCORE: 20/24` se
reproduisent au chiffre près sur `cc1b34c` (§ 5 (c)).

Pourquoi P1 et pas P0 : la dérive n'a rien détruit, et le motif « une preuve
périmée entre dans la décision automatique » est déjà porté par le **brief 1
du § 8 de `CURSOR-ab0e7f0`** (lier un verdict au commit sur lequel il a été
mesuré, refuser ou marquer périmé un point dont la base n'est plus ancêtre de
la base de fusion). Je n'ouvre pas de brief concurrent ; j'apporte la mesure
la plus nette obtenue jusqu'ici. Doctrine externe : une transition d'état de
cycle de vie n'est admise que sur une preuve **fraîche et liée à l'état de
source suivi** [S1] ; « toutes les vérifications validées contre la tête
courante de la PR — une preuve périmée n'est jamais crue » [S2].

### P1-2 — Le seul arbitrage que la revue demande n'a aucune existence machine, et le point qu'elle réfute est retenu en entier

C'est le constat central de cet audit.

Ce que la revue dit, en propre : le récit du constat 1 (« perdu à la
publication », « il n'a jamais atteint master », « personne n'était là ») « ne
résiste pas à la vérification » (§ 4), et « la sévérité P0 du constat 1 doit
être réexaminée » (§ 3, premier point). Elle porte **trois** questions au
propriétaire dans son § 3.

Ce que la machine en a fait, à 11:00:16Z, ligne 49 de
`architecture/audit-ledger.jsonl` :

```
{"timestamp": "2026-08-13T11:00:16Z", "audit_id": "CURSOR-16ff5ac-…",
 "event": "AUDIT_APPROVED", "actor": "policy:auto",
 "reason": "policy: ledger_AUDIT_APPROVED_retained_points_confirmed_union_partial …",
 "retained_points": [1, 2, 3, 4, 5, 6, 7]}
```

Et `architecture/decisions/DECISION-CURSOR-16ff5ac-….md` : « **Verdict :
APPROVED** … Points retenus : 1, 2, 3, 4, 5, 6, 7 ». Le point 1 est retenu
exactement comme les cinq `CONFIRMED`. Les trois arbitrages n'apparaissent
nulle part.

Le mécanisme, lu dans le code :

- `harness/audit_decision.py:270` — `retained = sorted({n for n, v in points if v in ("CONFIRMED", "PARTIAL")})`. Un `PARTIAL` est donc **indistinguable** d'un `CONFIRMED` dans l'ensemble retenu ; la nuance est perdue à l'écriture, pas plus loin.
- `harness/audit_decision.py:272` — `has_needs_owner = any(v == "NEEDS_OWNER" for _n, v in points)` : seule la **colonne du tableau** est lue, jamais le § 3 en prose.
- `harness/audit_decision.py:283-290` puis `:292-299` — la règle « ≥ 1 point CONFIRMED ou PARTIAL → APPROVED » (`review_has_confirmed_or_partial`) est évaluée **avant** `review_needs_owner_only`, et elle retourne. La règle dont l'objet est de ne pas décider sans le propriétaire est donc inatteignable dès qu'un point est confirmé.
- `harness/audit_convert.py:91-95` puis `:98-114` — la conversion relit `retained_points` sur l'événement `AUDIT_APPROVED` et l'écrit tel quel dans la provenance de la graine de brief (« Points retenus : … »). Le point 1 voyage donc en aval sans aucune marque de sa réfutation.

Rejeu complet en bac à sable (aucune écriture dans le dépôt ; `inbox/`,
`reviews/`, `decisions/` et le registre recréés dans un dossier temporaire —
script en § 5 (b)) :

```
§ 3 « Points à porter au propriétaire » : 3 puces de prose
occurrences du mot NEEDS_OWNER dans tout le document : 3

--- A. revue telle que fusionnée (point 1 = PARTIAL)
colonne Verdict lue par la machine : [(1, 'PARTIAL'), (2, 'CONFIRMED'), (3, 'CONFIRMED'), (4, 'CONFIRMED'), (5, 'PARTIAL'), (6, 'CONFIRMED'), (7, 'CONFIRMED')]
ledger AUDIT_CHALLENGED.verdicts = {'CONFIRMED': 8, 'REFUTED': 2, 'PARTIAL': 3, 'NEEDS_OWNER': 3}
décision automatique = AUDIT_APPROVED  retained_points = [1, 2, 3, 4, 5, 6, 7]
le fichier de décision mentionne-t-il 'propriétaire' ? False
le fichier de décision mentionne-t-il 'NEEDS_OWNER' ? False

--- B. même revue, point 1 = NEEDS_OWNER
colonne Verdict lue par la machine : [(1, 'NEEDS_OWNER'), (2, 'CONFIRMED'), …]
ledger AUDIT_CHALLENGED.verdicts = {'CONFIRMED': 8, 'REFUTED': 2, 'PARTIAL': 2, 'NEEDS_OWNER': 4}
décision automatique = AUDIT_APPROVED  retained_points = [2, 3, 4, 5, 6, 7]
le fichier de décision mentionne-t-il 'propriétaire' ? False
le fichier de décision mentionne-t-il 'NEEDS_OWNER' ? False
```

La variante A reproduit **à l'identique** les deux champs de la ligne réelle du
registre (`verdicts` et `retained_points`) : la reproduction est fidèle. La
variante B montre l'alternative : si le challenger avait écrit dans la colonne
le verdict qui correspond à ce qu'il demande vraiment, le point 1 aurait
simplement **disparu** de la décision, sans que personne ne soit appelé. Un
point nuancé n'a donc que deux issues machine : retenu comme s'il était
confirmé, ou évaporé. Il n'existe aucune troisième valeur pour dire « retenu,
mais son récit est réfuté et sa sévérité est à revoir ».

Délimitation honnête de ce qui est neuf ici. Deux audits touchent au voisinage
et je ne les rouvre pas : `CURSOR-2a4f808` P1-4 porte sur l'**analyseur** (une
cellule composite fait gagner le jeton de tête, donc l'ordre des mots décide) ;
`CURSOR-ab0e7f0` P2-5 porte sur le fait que le § 3 en prose n'a pas
d'existence machine, et conclut explicitement « pas de brief » parce que
« pas de propriétaire en `full_auto` » est une décision enregistrée
(ADR-0006). Ce que ni l'un ni l'autre ne demande, et que je propose en § 8 :
que `retained_points` **porte le verdict** de chaque point retenu, pour qu'un
`PARTIAL` ne soit plus indiscernable d'un `CONFIRMED` en aval. Ce n'est pas
réintroduire un propriétaire dans `full_auto` ; c'est cesser de perdre une
information que le challenger a déjà écrite.

Doctrine externe : la sortie d'un agent doit franchir une frontière sous
**schéma** — verdict énuméré et compteurs par sévérité — et l'orchestrateur
consomme ce contrat plutôt que la prose [S3] ; un constat découvert doit être
« résolu ou formellement escaladé », jamais disparaître [S4].

### P2-1 — Le deuxième arbitrage demandé était déjà résolu avant la fusion, et il contredit la ligne 4 de la même revue

Le § 3 de la revue demande au propriétaire de trancher « si un rattrapage
manuel du registre s'impose », au motif que
`audit_ledger.current_state_for('CURSOR-a4de4bb-…')` « renvoie toujours `None`
aujourd'hui (rejoué dans cette relecture) ».

Deux problèmes, dans l'ordre.

**C'était vrai à la rédaction, faux à la fusion.** Les trois lignes de registre
de `CURSOR-a4de4bb` sont horodatées `2026-08-13T08:40:11Z` et `08:40:34Z`, mais
elles sont arrivées sur `master` avec le commit `4c45718`, porté par la PR #65
fusionnée à **10:47:51Z** — donc après la rédaction (08:49:57Z) et **avant** la
fusion de cette revue (11:00:01Z). État réel au moment où l'arbitrage a été
publié : `AUDIT_CONVERTED`, avec `briefs: ["harness/queue/briefs/013-sim-tick-nourrit-une-fois"]`,
lot lui-même fusionné en PR #69 à 10:48:46Z. Le propriétaire est donc invité à
réparer ce qui était réparé, et à statuer sur un audit qui avait déjà produit
son lot.

**Et la même revue dit le contraire deux lignes plus haut.** Sa ligne 4
vérifie et **accepte** la convention : « absence de ligne `AUDIT_PROPOSED` =
convention documentée, pas un défaut d'enregistrement —
`harness/audit_ledger.py:74-83` ». Son § 3 traite le même `None` comme
l'indice d'un bug. Le `None` en question est d'ailleurs une propriété de la
fonction choisie, pas du registre : `audit_ledger.current_state_for` renvoie
`None` quand l'audit n'a aucun événement, là où `audits.current_state` renvoie
`AUDIT_PROPOSED` sur exactement la même entrée (sources en § 5 (e)). Sur deux
lectures possibles, la revue a retenu celle qui alarme, sans le dire.

Pas de brief : la fraîcheur est couverte par le brief 1 du § 8 de
`CURSOR-ab0e7f0`, et le reste est éditorial.

### P2-2 — Le registre a de nouveau inscrit des comptes de verdicts faux (8 / 2 / 3 / 3 contre 5 / 2 / 0 / 0)

La ligne `AUDIT_CHALLENGED` porte
`{"CONFIRMED": 8, "REFUTED": 2, "PARTIAL": 3, "NEEDS_OWNER": 3}`. La colonne
« Verdict » du tableau, elle, contient 5 `CONFIRMED` et 2 `PARTIAL`, **zéro**
`REFUTED` et **zéro** `NEEDS_OWNER` (§ 5 (a)). Le registre — la trace
permanente — annonce donc deux réfutations et trois renvois au propriétaire qui
n'existent dans aucune ligne du tableau.

La cause est un seul mot : `harness/audit_review.py:174` écrit
`verdicts = parse_verdicts(text)`, qui compte les **occurrences du mot** dans
tout le document (`:127-134`), alors que la décision lit
`parse_point_verdicts`. Le docstring de ce dernier promet pourtant, en
`harness/audit_decision.py:203-205`, « la MÊME analyse … un seul analyseur, un
seul contrat, aucun second endroit qui pourrait contredire le premier ». Le
second endroit est la ligne 174 d'`audit_review.py`.

**Pas de brief** : déjà consigné trois fois et jamais arbitré —
`CURSOR-779d97c` P1-3, `CURSOR-063d7eb` P1-2, `CURSOR-ab0e7f0` P2-1. Je ne
fais qu'ajouter la quatrième mesure.

### P2-3 — `architecture/reviews/**` n'a toujours aucune porte de schéma, et `reviewed_at` en donne ici une illustration impossible

Le job `schema` d'`audit-guard.yml` est vert sur cette PR. Il n'a pourtant pas
ouvert le fichier qu'elle ajoute : `harness/audit_schema.py:26` fixe
`INBOX = architecture/inbox` et `:98` ne parcourt que
`inbox.glob("CURSOR-*.md")`. Une PR dont le seul fichier est sous
`architecture/reviews/**` traverse donc la porte de schéma sans être lue —
lentille 3 à l'envers : la porte mécanique a tourné, mais sur autre chose.

Illustration inédite de ce que ça laisse passer : le frontmatter annonce
`reviewed_at: 2026-08-13T10:15:00Z`, alors que le commit qui le porte est
auteuré à **08:49:57Z**. La revue se déclare écrite 85 minutes après le commit
qui la contient — pas seulement invérifiable : impossible. Symétriquement,
l'audit relu déclare `created_at: 2026-08-13T08:55:00Z`, cinq minutes **après**
le commit de sa propre revue. Or c'est sur cette valeur de 08:55:00Z que repose
tout le calcul horaire par lequel la revue réfute le constat 1 (« 20 minutes
avant que l'audit ne soit créé »). L'argument central est adossé à une chaîne
d'horodatages saisis à la main, tous à la minute ronde, qu'aucune porte ne
confronte à `git log`.

**Pas de brief** : déjà porté par `CURSOR-779d97c` P2-6, le brief 3 du § 6 de
`CURSOR-063d7eb`, et `CURSOR-ab0e7f0` P2-3.

### P2-4 — La section « Provenance » décrit une méthode que le corps du document contredit deux fois

Le § 1 de la revue annonce : « Mesures rejouées directement sur ce dépôt (**pas
dans un worktree séparé**, mais **sans écriture** — `git status` propre
avant/après) ». Puis :

- la ligne 3 décrit deux variantes obtenues par « patch appliqué localement reproduisant exactement le extrait de code du § 7.6 de l'audit » — donc avec écriture ;
- la ligne 4 annonce « Rejoué dans un **worktree séparé** positionné exactement sur `16ff5ac` (`git worktree add`) ».

Aucune de ces méthodes n'est mauvaise ; un worktree figé est même exactement ce
qu'il fallait faire. Le défaut est que la section censée dire *comment* les
mesures ont été prises ne décrit pas ce qui a été fait, et qu'un lecteur qui
s'y fie se trompe sur la fraîcheur de chaque nombre. Constat éditorial, pas de
brief.

### P3-1 — Une exécution annulée en silence, et ce qui tient dans cette revue

`hermes-observer` a une exécution `cancelled` sur `8894f15` (démarrée
10:59:36Z) et quatre en `queued` sur la fusion — comportement de concurrence
déjà consigné par `CURSOR-2a4f808` (point 2 : trois fusions coup sur coup
perdent l'exécution du milieu, désormais en silence). Aucun élément nouveau,
aucun brief.

Ce qui tient, et qu'il ne faut pas défaire en corrigeant le reste :

| Affirmation de la revue | Rejeu indépendant | Résultat |
|---|---|---|
| Le tableau est lisible par la machine | `audit_decision.parse_point_verdicts` | 7 lignes, `(1,PARTIAL) … (7,CONFIRMED)` — aucune ligne perdue |
| `314 passed, 16 skipped` (harnais) | `pytest harness/tests/ -q` sur `cc1b34c` | `314 passed, 16 skipped in 16.80s` — identique |
| `SCORE: 20/24` (`harness_audit.py`) | rejeu sur `cc1b34c` | `SCORE: 20/24` — identique |
| Le mécanisme du constat 1 (`\|\| echo` avalant l'échec de `gh pr create`) | lecture de `.github/workflows/pipeline-challenge.yml:197-201` | présent, inchangé — et il a frappé **cette PR même** (branche poussée 08:49:57Z, PR ouverte à la main 10:59:33Z) |
| `16ff5ac` est bien dans `master` | `git log -1 16ff5ac…` | commit de fusion de la PR #60 — confirmé |
| Périmètre tenu | `git show --stat 8894f15` | 1 fichier, `architecture/reviews/**` uniquement |
| Le corps de la PR annonce « 5 CONFIRMED, 2 PARTIAL » | comparaison à `parse_point_verdicts` | **exact** — première fois dans cette série |

Et la revue fait ce qu'un contre-audit doit faire : elle réfute. Elle démonte
le récit central de l'audit qu'elle relit au lieu de le confirmer par
politesse, et elle le fait avec des preuves. Le défaut n'est pas là ; il est
que la boucle ne sait pas quoi faire d'une réfutation partielle.

## 4. Lentille 5 — taille et découpage

1 fichier, +102 / −0, largement sous le seuil où une relecture honnête décroche
(~5 fichiers / quelques centaines de lignes). Rien à découper. Une seule
réserve de forme : le tableau du § 2 tient en 7 lignes physiques mais 14,4 ko
de texte, la ligne 1 pesant à elle seule près de 1 900 caractères. Le contenu
est bon ; sa lisibilité en diff est mauvaise. Pas de constat séparé, c'est déjà
la substance du § P1-2 (ce que la machine lit d'une ligne pareille est une
seule cellule de verdict).

## 5. Commandes rejouées et sorties

Toutes les commandes ci-dessous ont été exécutées sur ce VM Linux, en lecture
seule sur le dépôt (`.venv/bin/python` conformément à `AGENTS.md`).

**(a) Les deux analyseurs sur la revue effectivement fusionnée**

```
$ .venv/bin/python -c "
import sys; sys.path.insert(0,'harness')
import audit_decision, audit_review, subprocess
from collections import Counter
text = subprocess.run(['git','show','origin/master:architecture/reviews/CLAUDE-CURSOR-16ff5ac-contre-audit-perdu-a-la-publication.md'],capture_output=True,text=True).stdout
print('len chars:', len(text))
rows = audit_decision.parse_point_verdicts(text)
print('parse_point_verdicts ->', rows)
print('per-point column counts:', dict(Counter(v for _,v in rows)))
print('parse_verdicts (ce qui part au registre) ->', audit_review.parse_verdicts(text))"
len chars: 14423
parse_point_verdicts -> [(1, 'PARTIAL'), (2, 'CONFIRMED'), (3, 'CONFIRMED'), (4, 'CONFIRMED'), (5, 'PARTIAL'), (6, 'CONFIRMED'), (7, 'CONFIRMED')]
per-point column counts: {'PARTIAL': 2, 'CONFIRMED': 5}
parse_verdicts (ce qui part au registre) -> {'CONFIRMED': 8, 'REFUTED': 2, 'PARTIAL': 3, 'NEEDS_OWNER': 3}
```

**(b) Rejeu isolé de la chaîne `review_recorded` → décision** — le script
recrée `inbox/`, `reviews/`, `decisions/` et un registre vide dans un dossier
temporaire, y copie l'audit réel et la revue réelle, puis appelle
`audit_review.record_challenge` et `audit_decision.decide_auto` avec le vrai
`harness/pipeline/auto_policy.yaml`. Sortie complète citée au § P1-2. La
variante B remplace le seul verdict de la ligne 1 (`**PARTIAL**` →
`**NEEDS_OWNER**`) et ne touche rien d'autre.

**(c) Les nombres mécaniques de la ligne 7, sur le parent de la fusion**

```
$ git rev-parse --short HEAD
cc1b34c
$ .venv/bin/python -m pytest harness/tests/ -q | tail -1
314 passed, 16 skipped in 16.80s
$ .venv/bin/python -m pytest sim/tests/ -q | tail -1
35 passed in 2.11s
$ .venv/bin/python harness/harness_audit.py | tail -1
SCORE: 20/24
```

(`20/24` : le `FAIL` de `no_premature_stub_content` est une hypothèse périmée
de l'outil d'audit lui-même, documentée dans `AGENTS.md` — pas un défaut de
cette PR.)

**(d) Les tests `sim/` qui existaient à `16ff5ac`**

```
$ git ls-tree --name-only 16ff5ac77e618551b033b3bccda88ba83523c423 sim/tests/
sim/tests/__init__.py
sim/tests/proof_red
sim/tests/test_adr_compliance.py
sim/tests/test_causal_chain.py
sim/tests/test_commerce.py
sim/tests/test_engine.py
sim/tests/test_no_hardcoded.py
sim/tests/test_rng.py
sim/tests/test_seeding.py
sim/tests/test_world.py
sim/tests/test_write_coverage.py
```

Les quatre fichiers du lot 013 (`test_survie_derivee.py`,
`test_kg_transportes_est_arrives.py`, `test_mortalite_continue.py`,
`test_tick_nourrit_une_fois.py`) sont absents de cet arbre et présents sur
`master`.

**(e) Les deux fonctions d'état, et l'état réel des audits cités**

```
$ .venv/bin/python -c "…inspect.getsource(audits.current_state)…"
def current_state(audit_id, events) -> str:
    \"\"\"The last ledger event for this audit, or PROPOSED if none.\"\"\"
    …
    return DEFAULT_STATE
---
def current_state_for(audit_id, ledger_path) -> str | None:
    \"\"\"… or None if it has no prior events in this ledger yet …\"\"\"

$ # états reconstruits depuis le registre de master
CURSOR-3b47ffe-pr57-monde-sans-faim                  -> AUDIT_ARCHIVED
CURSOR-16ff5ac-contre-audit-perdu-a-la-publication   -> AUDIT_APPROVED
CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois     -> AUDIT_CONVERTED
```

**(f) Le code du commerce, avant et après**

```
$ git show 16ff5ac77e618551b033b3bccda88ba83523c423:sim/engine.py \
    | grep -n 'food_deficit_kg = max(0.0'
 95:            cell_b.food_deficit_kg = max(0.0, cell_b.food_deficit_kg - transfer)
107:            cell_a.food_deficit_kg = max(0.0, cell_a.food_deficit_kg - transfer)
$ git show origin/master:sim/engine.py | grep -n food_deficit_kg | head -3
  9:    _apply_consumption → consomme le stock, accumule food_deficit_kg si manque ;
 20: Le maillon commerce ne modifie plus food_deficit_kg (SC1) ; les transferts
161:    # Passe 2 : appliquer tous les transferts (jamais food_deficit_kg)
```

**(g) Chronologie des fusions voisines**

```
$ gh pr list --state merged --limit 12 --json number,mergedAt,title …
65 | 2026-08-13T10:47:51Z | Boucle d'audit post-PR #60 : décision a4de4bb récupérée…
69 | 2026-08-13T10:48:46Z | Brief 013 : le tick nourrit une fois…
71 | 2026-08-13T11:00:01Z | challenge: revue de l'audit CURSOR-16ff5ac-…
```

## 6. Classification de la CI du commit audité

Sur `8894f15` (tête de la PR, exécutions relancées à l'ouverture de la PR à
10:59:36Z) :

| Workflow | Job(s) | Conclusion |
|---|---|---|
| `audit-guard` | `schema` | SUCCESS |
| `audit-guard` | `cursor-scope` | SKIPPED (branche `forge-bot/*`, pas `cursor/*`) |
| `harness-ci` | `tests`, `sim-tests`, `f0-demo` | SUCCESS |
| `security` | `actionlint`, `gitleaks` | SUCCESS |
| `merge-bot` | `check-and-automerge` | SUCCESS |
| `pipeline-audit` | `invoke-cursor-auditor` | SUCCESS (a **lancé** l'auditeur ; voir P0-1) |
| `hermes-observer` | `Reconcile local Hermes state` | **CANCELLED** (concurrence) |

Sur la fusion `74e0349` : `audit-guard`, `harness-ci`, `security`,
`pipeline-audit`, `hermes-dashboard` et `pipeline-orchestrate` en SUCCESS,
`pipeline-failure-escalate` SKIPPED, quatre `hermes-observer` en `queued`.

**Classification : verte.** Aucun job rouge, ni avant ni après fusion. C'est
précisément le point : tout ce que décrit cet audit s'est produit sous une CI
entièrement verte. Une porte qui ne lit pas le fichier livré (P2-3) et un
auditeur lancé mais jamais attendu (P0-1) sont verts par construction.

## 7. Risques par sévérité

| Sévérité | Risque | Conséquence si rien n'est fait |
|---|---|---|
| **P0** | La critique indépendante est fusionnée avant d'exister (28 s) | La boucle à quatre acteurs n'a que trois acteurs effectifs ; déjà retenu, non encore instruit (brief 014 vide) |
| **P1** | Une preuve vieille de 2 h 10 devient une décision en 15 s | Des décisions `APPROVED` reposent sur un dépôt disparu ; ici trois mesures fausses sur sept |
| **P1** | Un `PARTIAL` est retenu comme un `CONFIRMED`, et le § 3 n'existe pas pour la machine | Un récit réfuté est converti en lot à faire ; les arbitrages demandés se perdent sans trace |
| **P2** | Le registre porte des comptes de verdicts que le tableau démentit | La trace permanente est inexploitable pour compter ce que Claude a réellement conclu |
| **P2** | `reviews/**` n'a aucun schéma : `reviewed_at` peut être impossible | Le calcul de fraîcheur repose sur des chaînes saisies à la main |
| **P2** | La section « Provenance » ne décrit pas la méthode employée | Un lecteur se trompe sur la fraîcheur de chaque nombre |
| **P3** | Une exécution `hermes-observer` annulée en silence | Un état Hermes potentiellement non réconcilié ; déjà consigné |

## 8. Brief atomique proposé (1 — très en dessous du plafond de 3)

Rappel : un audit **ne pré-autorise rien**. Cette proposition n'a valeur
d'instruction qu'après conversion explicite en brief par le propriétaire
(`CLAUDE.md` › Single Source of Instruction).

**Brief 1 — Qu'un point retenu porte son verdict, et qu'un arbitrage demandé
laisse une trace (P1-2).**

Portée : `harness/audit_decision.py` (l'écriture de `retained_points` et le
rendu du fichier de décision), `harness/audit_convert.py` (la provenance de la
graine de brief). Hors portée, explicitement : le choix « pas de propriétaire
en `full_auto` » (ADR-0006) n'est pas rouvert — la décision reste automatique
et reste `APPROVED`.

Objet : que l'ensemble retenu cesse d'être une liste de numéros nus. Un point
retenu doit être traçable à son verdict (`CONFIRMED` ou `PARTIAL`), et le
fichier de décision comme la graine de brief doivent distinguer les deux, afin
qu'un lot issu d'un `PARTIAL` ne soit pas instruit comme si la revue l'avait
confirmé. Et qu'une revue qui nomme des arbitrages hors tableau ne les fasse
pas disparaître : la décision doit au minimum en compter et en citer
l'existence, sans avoir besoin de les résoudre.

Preuve rouge attendue avant tout correctif : un test qui rejoue exactement le
cas de cette PR — la revue réelle
`CLAUDE-CURSOR-16ff5ac-contre-audit-perdu-a-la-publication.md`, dont le point 1
est `PARTIAL` et dont le § 3 porte trois arbitrages — et qui échoue aujourd'hui
parce que `retained_points` vaut `[1, 2, 3, 4, 5, 6, 7]` sans qualification et
parce que le fichier de décision produit ne contient ni « propriétaire » ni
`NEEDS_OWNER` (sorties A et B du § 5 (b), reproductibles).

Doctrine externe : franchissement de frontière sous schéma, avec verdict
énuméré et compteurs par sévérité, consommé par l'orchestrateur à la place de
la prose [S3] ; tout constat découvert est « résolu ou formellement escaladé »,
jamais silencieusement abandonné [S4] ; une porte n'a d'effet que si elle est
obligatoire à un point défini du flux [S1].

**Aucun deuxième ni troisième brief.** Les autres constats sont soit déjà
consignés et non arbitrés (P1-1 → brief 1 du § 8 de `CURSOR-ab0e7f0` ; P2-2 →
`CURSOR-779d97c` P1-3 et `CURSOR-063d7eb` P1-2 ; P2-3 → `CURSOR-779d97c` P2-6
et brief 3 du § 6 de `CURSOR-063d7eb`), soit couverts par une décision déjà
retenue (P0-1 → proposition 1 de `CURSOR-a600532`, `AUDIT_APPROVED`, graine
`brief 014`), soit éditoriaux (P2-1, P2-4, P3-1). Les proposer gonflerait la
file sans rien ajouter — c'est exactement ce que
`review-guidelines.md` appelle du bruit.

## 9. Déclaration de non-duplication

Je déclare ce que j'ai vérifié, et je l'ai vérifié avant de le déclarer.

- **Briefs ouverts.** `harness/queue/briefs/` compte 15 dossiers (`ls -d harness/queue/briefs/*/ | wc -l`). Le seul directement voisin est `014-pipeline-contre-audit-porte` : je l'ai lu, c'est une **graine non remplie** (toutes ses sections utiles sont `<<TODO (planificateur)>>`) issue de `CURSOR-a600532`, dont la provenance liste 17 points retenus. Son objet — la porte du contre-audit avant fusion — est le motif P0-1, que je ne rouvre pas. Mon brief 1 porte sur un autre objet : la qualification des points retenus **après** que la décision a été prise.
- **Audits déjà déposés.** P0-1 : couvert par `CURSOR-a600532` (proposition 1, `AUDIT_APPROVED`) et `CURSOR-ab0e7f0` P0-1 — je n'ajoute que la mesure de 28 s. P1-1 : couvert par le brief 1 du § 8 de `CURSOR-ab0e7f0` — je n'ajoute que trois preuves numériques de dérive. P1-2 : voisiné par `CURSOR-2a4f808` P1-4 (l'analyseur et l'ordre des mots) et `CURSOR-ab0e7f0` P2-5 (le § 3 en prose, « pas de brief ») ; ni l'un ni l'autre ne demande que `retained_points` porte son verdict — c'est la part neuve, et c'est le seul brief que je propose. P2-2 : couvert par `CURSOR-779d97c` P1-3 et `CURSOR-063d7eb` P1-2. P2-3 : couvert par `CURSOR-779d97c` P2-6 et le brief 3 du § 6 de `CURSOR-063d7eb`. P3-1 : couvert par `CURSOR-2a4f808`.
- **Décisions enregistrées non rouvertes.** L'absence de propriétaire en `full_auto` (ADR-0006), la dérogation à la protection de branche (`.github/merge-bot.yaml`), et la nature « documentation seule » du scalaire `mode:` d'`auto_policy.yaml`.

## 10. Sources externes

Recherche du jour, consultée le **2026-08-13**, sur les trois thèmes exigés par
le contrat (`autonomous AI dev pipeline`, `agent orchestration CI`,
`token budget LLM agents`).

| # | source | date de publication | consulté le |
|---|---|---|---|
| S1 | *Proof-or-Stop: Don't Trust the Agent, Trust the Evidence — Loop Engineering for Verifiable Evidence-Gated Lifecycle Control*, arXiv:2607.14890 — <https://arxiv.org/html/2607.14890v1> — « les états de cycle de vie (relu, testé, fait, prêt-à-fusionner) restent des *prétentions* tant qu'un système en aval ne peut décider si la preuve **fraîche et liée à l'état de source suivi** les soutient » ; preuve manquante, périmée ou incomplète → réparation bornée, dégradation honnête, escalade ou arrêt | 2026-07 (identifiant arXiv) | 2026-08-13 |
| S2 | *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green* — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> — « une CI verte n'est pas la preuve qu'un changement écrit par un agent peut atterrir » ; paquet de preuves imposé (commandes exactes rejouées, résultats, périmètre non couvert, SHA vert précédent) et autorité de fusion **humaine et étagée** | 2026 (page éditeur) | 2026-08-13 |
| S3 | MinimumCD — *Agentic CD : Coding & Review Setup* — <https://beyond.minimumcd.org/docs/agentic-cd/architecture/agent-configuration/> — « chaque frontière d'agent est une frontière de budget de jetons » ; l'orchestrateur de revue « ne raisonne pas lui-même : il agrège des sorties **structurées** » et renvoie un objet JSON — « analysez le JSON directement ; ne re-résumez pas ses constats en prose » | documentation vivante (2026) | 2026-08-13 |
| S4 | *AI Coding Tools Shipped More CVEs in March Than in All of 2025* — <https://medium.com/design-bootcamp/ai-coding-tools-shipped-more-cves-in-march-than-in-all-of-2025-0e9f69abf6c2> — registre de constats et marqueurs de cycle de résolution : « tout constat découvert doit être **résolu ou formellement escaladé** … il ne peut pas disparaître » ; fusionner est un acte de responsabilité, donc une confirmation explicite | 2026 (page éditeur) | 2026-08-13 |
| S5 | *LLM Token Budget Strategies for Agents: 5 Layers With Code Examples (2026)* — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> — cinq couches (plafond par requête, budget roulant par session, plafond mensuel par clé, routage par palier de modèle, disjoncteur sur la vélocité de dépense), appliquées **hors du code de l'agent** pour qu'il ne puisse pas les contourner | 2026 (page éditeur) | 2026-08-13 |
| S6 | *Agent Guardrails: Loop Limits, Cost Caps, and Human Approval Gates* — <https://dev.to/gabrielanhaia/agent-guardrails-loop-limits-cost-caps-and-human-approval-gates-56fn> — trois garde-fous avant que la boucle ne voie du trafic : plafond de pas, budget **par exécution libellé en dollars** (pas en jetons, dont le prix varie), et porte d'approbation sur les verbes qui changent le monde | 2026 (page éditeur) | 2026-08-13 |

## 11. Ce que cet audit ne fait pas

- Il **ne décide pas**. `status: PROPOSED`, les trois flags `*_authorized` à `false`. La recevabilité appartient au policy engine et au propriétaire.
- Il **n'instruit rien**. La proposition du § 8 n'a d'effet qu'après conversion explicite en brief ; jusque-là, aucune phrase de ce fichier n'est un ordre.
- Il **ne rouvre pas** les décisions enregistrées listées en § 9.
- Il **ne juge pas la valeur métier** du contre-audit relu, seulement la solidité de ses preuves et le sort que la boucle leur fait.
- Il **n'a pas rejoué** les trois variantes de simulation à 200 ticks de la ligne 3 (morts, cellules affamées, kg transportés) : le code cité ayant été remplacé sur `master` par le lot 013, un rejeu au chiffre près ne serait plus comparable. Je m'en tiens à ce que j'ai mesuré, et je le dis plutôt que de l'affirmer.
- Budget d'appels : sous le plafond de 60 du contrat, en une seule passe.
