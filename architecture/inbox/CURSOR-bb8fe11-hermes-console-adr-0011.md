---
audit_id: CURSOR-bb8fe11-hermes-console-adr-0011
auditor: cursor-cloud
target_branch: forge/hermes-decision-adr-0011-c2dd
target_commit: bb8fe11b860f8383e5178994f35ca116f89da2fd
created_at: 2026-08-12T12:45:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet** : critique de la pull request [#34](https://github.com/PLiagre/ForgeHistory/pull/34)
(« hermes: décision "ok pour tout" — ADR-0011 (console du propriétaire) et
roadmap reflétée »), SHA de tête `bb8fe11b860f8383e5178994f35ca116f89da2fd`,
branche `forge/hermes-decision-adr-0011-c2dd`, 4 fichiers, +168 −8, aucun
code. Critique conduite selon `architecture/review-guidelines.md` (six
lentilles, sévérités P0–P3, preuve citée par constat).

**Ce que la PR fait** : elle enregistre en ADR-0011 la décision propriétaire
« ok pour tout » — Hermes peut désormais exécuter quatre actions du
propriétaire (fusionner/refuser une PR, poser/retirer `pipeline/pause`,
déclencher `pipeline-forge-run`, déposer une demande) sur ordre explicite —
et reflète cette décision dans `ROADMAP.md` et dans la demande d'origine.

**Verdict de la critique, en une phrase** : le travail documentaire est
propre, la preuve d'exécution annoncée est exacte au caractère près, et la
CI est verte ; mais l'affirmation qui porte toute la sûreté de la décision
— « les conditions de fusion ne sont ni levées ni affaiblies » — n'est
adossée à aucun mécanisme, et la fusion de cette PR elle-même, 56 secondes
après son ouverture et sans qu'aucune des quatre preuves n'ait été lue,
fournit la démonstration mesurée du contraire.

**Aucun constat P0.** La PR ne touche ni code, ni CI, ni brief, ni verdict ;
elle n'affaiblit aucune porte mécanique existante et n'en contourne aucune.
Les constats les plus lourds (P1) portent sur des affirmations non mesurées
et sur un canal de risque non traité, pas sur un défaut d'exécution.

**Décompte** : 4 × P1, 3 × P2, 4 × P3. Trois briefs atomiques proposés
(section 7). Cet audit ne prescrit rien : `status: PROPOSED`, les trois
flags `*_authorized` à `false`.

## Classification de la CI du commit audité

**Verte.** Tous les contrôles du SHA de tête `bb8fe11` sont en succès ;
aucun échec, aucun en attente au moment de l'audit.

| workflow / job | résultat |
|---|---|
| `harness-ci / tests` | pass (25 s) |
| `harness-ci / f0-demo` | pass (12 s) |
| `audit-guard / schema` | pass (13 s) |
| `security / actionlint`, `security / gitleaks` | pass |
| `pipeline-audit / invoke-cursor-auditor` | pass (21 s) — c'est l'appel qui a produit cet audit |
| `hermes-observer / Reconcile local Hermes state` | pass (7 s, après une exécution annulée par la clé de concurrence) |
| `audit-guard / cursor-scope` | **skipping** — la branche n'est pas `cursor/*` (constat P2-3) |
| `merge-bot / check-and-automerge` | **skipping** — la branche n'est ni `cursor/*` ni `forge-bot/*` (constat P3-3) |

Deux jobs « skipping » ne sont pas des échecs : ce sont des conditions `if:`
non remplies, par conception. Ils sont néanmoins porteurs de sens et sont
traités comme tels plus bas.

# 2. Chronologie mesurée (fait central de cet audit)

| horodatage (UTC) | événement | source |
|---|---|---|
| 12:24:28 | PR #34 ouverte | `gh pr view 34 --json createdAt` |
| 12:24:31 | `pipeline-audit / invoke-cursor-auditor` démarre et lance l'auditeur Cursor | `gh run view 31596285401` |
| 12:25:24 | PR #34 **fusionnée** par `PLiagre` (commit de fusion `0269d8e`) | `gh pr view 34 --json mergedAt,mergedBy` |
| 12:45 | dépôt du présent audit — le premier visant `bb8fe11` | ce fichier |

**56 secondes** séparent l'ouverture de la fusion. `reviewDecision` est
vide et `reviews` vaut `0`. Aucun fichier de `architecture/inbox/` ne portait
`target_commit: bb8fe11...` à l'instant de la fusion (`git grep` sur
`origin/master` : aucun résultat). Cette chronologie n'est pas un reproche
adressé au propriétaire — la porte conditionnelle est explicitement
**« spécifiée, non câblée »** (`docs/rules/conditional-merge-gate.md:3`), donc
aucune règle active n'a été enfreinte, et fusionner est sa prérogative.
Elle est citée parce qu'elle **mesure** ce que l'ADR affirme sans le mesurer :
aujourd'hui, les quatre preuves ne sont lues ni consignées par personne.

# 3. Constats P1 — à corriger avant fusion, sauf dérogation écrite

> La PR étant déjà fusionnée (12:25:24, avant que cet audit existe), « avant
> fusion » se lit ici « avant que le câblage H4 soit fait », puisque l'ADR
> lui-même réserve ce câblage à une étape ultérieure.

## P1-1 — La décision qui délègue le droit de fusionner a été fusionnée sans aucune des quatre preuves qu'elle déclare intactes

*Lentilles 2 (preuve d'exécution) et 6 (correction hallucinée).*

`docs/adr/0011-hermes-console-du-proprietaire.md:33` affirme que « les
conditions de fusion elles-mêmes (CI verte, gate ACCEPT, verdict d'un acteur
différent du producteur, audit Cursor) ne sont ni levées ni affaiblies ».
`docs/rules/conditional-merge-gate.md:27-55` décrit ces quatre prédicats
comme des **lectures précises**, refaites « immédiatement avant la tentative
de fusion », invalidées dès que le SHA de tête change, et dont un prédicat
« faux, absent ou illisible » doit laisser la PR ouverte.

Or, pour `bb8fe11` : le prédicat 4 (« exactement un fichier suivi sous
`architecture/inbox/CURSOR-*.md` portant `target_commit: <SHA de tête>` »,
`conditional-merge-gate.md:46-50`) était **impossible à satisfaire** — le
job qui invoque l'auditeur venait de démarrer 53 secondes plus tôt, et le
premier audit visant ce SHA est le présent fichier. Les prédicats 2 et 3
(gate `ACCEPT`, verdict indépendant) sont sans objet ici, faute de brief
associé — ce qui est légitime pour une PR documentaire, mais montre que la
porte à quatre preuves ne s'applique pas telle quelle à ce type de PR, cas
qu'aucun document ne traite.

Conséquence : l'affirmation de la ligne 33 décrit un état de fait qui
n'existe pas. Elle est du type que `review-guidelines.md` § lentille 6
appelle « succès affirmé non mesuré ». La littérature externe est
convergente sur ce point : une porte de fusion n'est fiable que si elle est
tenue par la plateforme et non par la bonne volonté de l'acteur qui fusionne
([S1], [S2], [S3]).

**Ce qui rendrait le constat caduc** : une trace, sur un SHA donné, montrant
les quatre lectures effectuées avant fusion — ou une phrase de l'ADR
reconnaissant que la porte est inactive et que la délégation porte sur un
clic non vérifié.

## P1-2 — L'ADR transforme un clic en jugement, sans que la décision propriétaire ait porté sur ce point

*Lentilles 1 (intention avant diff) et 4 (cadrage adverse).*

La demande d'origine, déjà fusionnée et donc couverte par le « ok pour
tout », formule l'action n° 1 comme un geste mécanique :
« **fusionner ou refuser une PR** (le « clic final humain » actuel) »
(`hermes/requests/DEMANDE-20260812-hermes-tableau-de-bord-pilotage.md:97`).
Ses garde-fous — confirmation explicite, jeton minimal, trace dans
`hermes/reports/`, `127.0.0.1` — figurent aux lignes 102-106 de la même
demande, mot pour mot. J'ai vérifié ce point avant d'écrire quoi que ce
soit : ces garde-fous ne sont pas des ajouts du rédacteur, le « ok » les
couvre réellement.

Ce que l'ADR ajoute, en revanche, n'était pas dans la demande :
« Hermes doit refuser d'exécuter une fusion si une preuve manque et le dire
au propriétaire » (`docs/adr/0011-...:117-118`). Cette phrase confie à
Hermes l'**appréciation** des quatre preuves. Or :

- `docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md:32`
  interdit explicitement à Hermes d'écrire des **verdicts** ; juger si les
  preuves d'une porte sont réunies est un acte de forme verdictale ;
- `docs/adr/0011-...:101-102` soutient que « seule la main qui l'exécute
  change » — ce qui est faux si la main doit aussi décider ;
- `conditional-merge-gate.md:57-61` réserve la traduction de ces lectures à
  « un lot ultérieur qui traduise exactement ces lectures en code et fasse
  l'objet de sa propre évaluation ». L'ADR place la même appréciation dans
  une installation hors dépôt, sans rubrique, sans sortie et sans évaluation.

L'ADR reconnaît d'ailleurs la faille dans sa propre section Negative :
« La frontière "ordre explicite" repose sur la discipline de l'installation
locale, que le dépôt ne peut pas vérifier mécaniquement »
(`docs/adr/0011-...:108-109`). Le constat est donc que l'ADR contient à la
fois l'aveu et l'affirmation contraire, sans les réconcilier.

## P1-3 — Le seul canal d'entrée réel vers l'installation qui portera le jeton n'est pas traité ; la garantie donnée porte sur un autre canal

*Lentille 6 (pièges du code généré par IA) et sécurité.*

L'ADR nomme correctement le risque : « Hermes agit sans ordre (bug,
prompt-injection via un événement reçu) » (`docs/adr/0011-...:116`, section
Risks) et donne comme garantie de surface : « surface réseau inchangée : le
tableau 9119 reste lié à `127.0.0.1` ; aucune exposition réseau sans couche
d'authentification » (`:55-56`).

Cette garantie porte sur la mauvaise surface. Le canal d'entrée existant
n'est pas le port 9119 : c'est `.github/workflows/hermes-observer.yml`, qui
se déclenche sur `pull_request_target` (ligne 4), tourne sur un **runner
auto-hébergé de la machine du propriétaire** (`runs-on: [self-hosted,
Windows, X64, hermes-observer]`, ligne 32) et transmet à un script local
**la totalité de la charge utile de l'événement** :
`-EventPath '${{ github.event_path }}'` (lignes 37-40). Cette charge utile
contient du texte que le dépôt ne contrôle pas — titres et corps de PR,
messages de commit. La demande le confirme : « Hermes local **reçoit donc
déjà les événements du projet** » (`DEMANDE-...:29-34`).

Autrement dit, une fois H4 câblé, la même installation traitera de l'entrée
non fiable **et** détiendra un jeton capable de fusionner et de déclencher
des workflows. C'est exactement la configuration que la littérature 2026
décrit comme la précondition de l'attaque — le « lethal trifecta » /
« Rule of Two » : entrée non fiable, accès en écriture et secrets réunis
dans la même session ([S4], [S5], [S6]). Ces mêmes sources concluent
qu'aucune défense par détection ne tient (taux de contournement > 90 % sous
attaque adaptative) et que seule la **séparation architecturale** — la
session qui lit l'entrée non fiable n'est pas celle qui détient le pouvoir
d'agir — est fiable. Or le garde-fou retenu par l'ADR (« confirmation
conversationnelle ») est précisément un contrôle de type détection, appliqué
à l'intérieur de la session exposée.

**Pourquoi P1 et non P0** : aucun jeton n'existe encore, le câblage est hors
dépôt, et rien de ce qui a été fusionné ici ne confère de pouvoir. Le
constat est P1 parce que l'ADR est le seul endroit où cette contrainte de
séparation pouvait être posée avant que le câblage ne se fasse ailleurs, et
qu'une fois l'ADR marqué `accepted` sans elle, plus rien ne la rappellera.

## P1-4 — ADR-0011 est absent de l'index canonique des ADR

*Lentille 3 (portes mécaniques d'abord).*

`docs/adr/README.md` est le seul index des décisions ; il liste 0001 à 0010.
Après fusion de la PR #34, ADR-0011 n'y figure pas :

```
$ git show origin/master:docs/adr/README.md | rg -c "0011"
0 occurrence
```

Ce n'est pas une omission tolérée par l'usage : le commit qui a introduit
ADR-0010 a mis l'index à jour dans le même lot (`git log --oneline -- docs/adr/README.md`
→ `9ad76ff ADR-0010: Hermes chef de projet, ROADMAP.md, contrat hermes/, …`).
La PR #34 modifie 4 fichiers, aucun n'étant `docs/adr/README.md`.

L'index canonique est donc faux **sur `master`** au moment où j'écris : un
lecteur qui parcourt `docs/adr/README.md` conclura qu'ADR-0010 est la
dernière décision, alors qu'ADR-0011 révise son contour. Second aspect, plus
structurel : aucun test ne couvre cette classe d'erreur (recherche de
`docs/adr` dans `harness/tests/` : uniquement des occurrences dans des
fixtures, aucun contrôle de complétude de l'index). C'est typiquement du
travail que la lentille 3 veut voir confié à une porte mécanique plutôt qu'à
la vigilance d'un relecteur.

# 4. Constats P2 — à planifier

## P2-1 — Le triplet de permissions retenu dépasse le périmètre « fermé » de quatre actions

`docs/adr/0011-...:47-49` spécifie « un fine-grained PAT GitHub limité à ce
dépôt et aux permissions strictement nécessaires (contents, pull-requests,
actions) ». La permission `contents` en écriture autorise la **poussée
directe sur n'importe quelle branche, `master` incluse** — soit bien au-delà
des quatre actions énumérées. Et rien ne la borne côté dépôt : la protection
de branche est indisponible sur ce plan GitHub (`HTTP 403`, vérifié le
2026-08-11 —
`architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md:40`
et `docs/rules/full-auto-pipeline.md:153`).

La nuance est réelle et je la dis : fusionner via l'API exige en pratique
`contents` en écriture, le triplet n'est donc pas gratuit. Le constat porte
sur le mot « strictement » : le périmètre fermé annoncé n'est pas une borne
de permission, c'est une discipline. Les sources externes recommandent des
identifiants de courte durée et un cadrage par action plutôt qu'un jeton
long terme à trois permissions ([S5], [S4]).

## P2-2 — La trace obligatoire est écrite par l'acteur qui agit et n'inclut pas les preuves

`docs/adr/0011-...:50-52` impose que « chaque action exécutée [soit]
consignée dans un rapport `hermes/reports/` (quoi, quand, sur ordre de
qui) ». Deux limites :

1. **Le contenu imposé n'inclut pas les quatre lectures.** La seule chose
   que la trace pourrait prouver et que GitHub ne conserve pas déjà — l'ordre
   reçu et les preuves examinées — est justement ce qu'elle n'exige pas.
   L'identité du fusionneur, elle, est déjà dans l'historique GitHub.
2. **Elle est auto-rédigée.** L'acteur qui exécute rédige seul l'attestation
   de sa propre conformité, après coup. C'est le motif que
   `architecture/review-guidelines.md:36-37` interdit ailleurs (« celui qui
   produit ne prononce pas la recevabilité ») et que le harnais vérifie
   mécaniquement pour les verdicts
   (`verdict_audit.check_verdict_not_self_authored`, cité en
   `docs/adr/0010-...:91-92`).

Les sources externes formulent la même exigence sous le nom d'« evidence
pack » : identifiants de run, commandes exécutées, portée non couverte —
attachés à la PR, pas au récit de l'agent ([S3], [S2]).

## P2-3 — La garde de périmètre atteste un préfixe de branche, pas une identité d'acteur

Le corps de la PR #34 énonce lui-même le mécanisme : « Branche `forge/*` et
non `cursor/*` : `audit-guard` (job `cursor-scope`) échoue mécaniquement
toute PR `cursor/*` touchant autre chose que `architecture/inbox/` ». C'est
exact : `.github/workflows/audit-guard.yml:30` conditionne le job à
`startsWith(github.head_ref, 'cursor/')`. Résultat mesuré sur cette PR :
`cursor-scope` → `skipping`, alors que les deux commits portent
`Author: Cursor Agent <cursoragent@cursor.com>`.

**Je ne rejuge pas la délégation de rédaction à Cursor** : elle est
enregistrée (`docs/adr/0010-...:6` « rédaction déléguée à Cursor »), le
propriétaire l'a voulue, et la reprendre serait du bruit au sens de
`review-guidelines.md` (« répéter un motif déjà écarté par une décision
enregistrée »). Le constat est autre, et il est neuf : la garde n'atteste
pas ce qu'on croit qu'elle atteste. Elle contrôle une **chaîne de
caractères** choisie par l'auteur de la PR, alors que l'identité de l'acteur
est disponible dans les métadonnées de commit et dans l'auteur de la PR.
Toute phrase de la forme « une PR Cursor est mécaniquement bornée à
`architecture/inbox/` » est donc fausse en général — y compris quand elle
protège les Interdits de mon propre contrat
(`architecture/agents/cursor-auditor.md` § Interdits).

# 5. Constats P3 — information

## P3-1 — Ni ADR-0010 ni `hermes/README.md:41` ne sont annotés alors que leur prémisse est révisée

`docs/adr/0011-...:14-16` (Context) résume ADR-0010 comme donnant à Hermes
« aucun droit d'exécution ». ADR-0010 conserve `**Status**: accepted` sans
annotation, et sa section Negative continue d'affirmer que la dérive est
« bornée … par le fait qu'aucun workflow n'exécute ce que Hermes écrit »
(`docs/adr/0010-...:96-97`). Plus frappant : la PR insère sa nouvelle
section « Ce qu'Hermes peut exécuter (ADR-0011) » à la ligne 43 de
`hermes/README.md`, **juste après** la phrase inchangée « Aucun workflow
n'exécute ce que Hermes écrit » (ligne 41).

Sévérité P3 et non davantage, parce qu'à la lettre les deux énoncés restent
compatibles : Hermes appellera l'API GitHub de sa propre initiative
d'exécution, il n'est pas *exécuté par* un workflow du dépôt. C'est un
défaut de lisibilité, pas une contradiction formelle. Le gabarit prévoit
le vocabulaire pour le lever (`docs/adr/template.md:4` : `superseded by
ADR-NNNN`).

## P3-2 — Deux imprécisions dans `ROADMAP.md`

- `ROADMAP.md:68` conserve en **position 1** de « Prochaines étapes (dans
  l'ordre) » (`:66`) une étape barrée et marquée « fait le 2026-08-12 » :
  une liste de prochaines étapes dont la première est passée.
- `ROADMAP.md:94` (historique des révisions) parle de « H1-H5 », alors que
  le corps de la roadmap n'énumère que H1, H2, H3 et H4 (`:77-83`) ; H5
  n'est défini que dans la demande (`DEMANDE-...:108-122`). Un lecteur de la
  seule roadmap ne peut pas résoudre « H5 ».

## P3-3 — Deux affirmations de conformité du corps de PR sont inexactes, sans changer la conclusion

- « Chemins `hermes/**` et `ROADMAP.md` hors allowlist du merge-bot :
  relecture humaine voulue. » La conclusion est juste, la raison ne l'est
  pas : le job `check-and-automerge` **ne s'exécute pas du tout** sur une
  branche `forge/*` (`.github/workflows/merge-bot.yml:27` le limite à
  `cursor/` et `forge-bot/`) — d'où le `skipping` observé. L'allowlist n'a
  jamais été consultée.
- « Commit `hermes:` pour les fichiers Hermes/roadmap, commit séparé pour
  l'ADR. » `hermes/README.md` — chemin `hermes/**` — se trouve dans le commit
  `adr-0011:` (`git show --stat e641c0b` → `docs/adr/0011-….md`,
  `hermes/README.md`). Défendable, `hermes/README.md` étant le contrat et non
  du contenu rédigé par Hermes ; l'énoncé de la PR reste inexact.

## P3-4 — L'affirmation « HTTP 403 vérifié » ne dit pas où la vérification est consignée

`docs/adr/0011-...:90-92` invoque « la protection de branche indisponible
sur ce plan GitHub (`HTTP 403` vérifié) » sans renvoi. La vérification
existe pourtant bel et bien, datée du 2026-08-11
(`DECISION-CURSOR-e9a6f4c-...:40` et `docs/rules/full-auto-pipeline.md:153`).
Constat de forme : une preuve non citée est indistinguable d'une preuve
inventée pour un lecteur qui ne connaît pas le dépôt.

# 6. Ce que la PR fait bien — vérifié, pas supposé

Ces points sont listés parce qu'une critique honnête mesure aussi ce qui
tient, et parce que plusieurs constats candidats sont tombés à la
vérification.

1. **La preuve d'exécution annoncée est exacte au caractère près.** Le corps
   de PR annonce « 309 passed, 16 skipped » ; rejeu sur l'état final :
   identique (sortie en section 8). C'est la lentille 2 tenue.
2. **La CI est intégralement verte** sur le SHA audité (tableau en section 1).
3. **La taille du diff est dans les limites d'une relecture honnête** :
   4 fichiers, +168 −8, sous le seuil d'environ 5 fichiers de la lentille 5.
   Aucun `NEEDS_SPLIT` à signaler.
4. **Le cycle de vie de la demande est respecté** : `OPEN` →
   `REFLECTED_IN_ROADMAP` est exactement la transition prévue par
   `hermes/README.md:61-75`, et la valeur appartient à l'énumération
   autorisée (`hermes/README.md:51`).
5. **La correction factuelle est permise, signalée et vraie.** L'en-tête de
   `ROADMAP.md` autorise « une correction factuelle (statut devenu faux) …
   à tout acteur, en la signalant dans le message de commit » ; le message de
   `bb8fe11` la signale ; et le fait est confirmé par `HANDOFF.md:5` (« Les
   secrets d'abonnement ont été provisionnés par le propriétaire »).
6. **Les actions n° 2 et n° 3 décrivent exactement les mécanismes réels** :
   `pipeline-forge-run.yml:31-34` expose bien un `workflow_dispatch` avec
   l'entrée `brief_dir`, et `pipeline/pause` est bien le coupe-circuit lu par
   les trois workflows d'invocation. Aucune dépendance inventée.
7. **L'action n° 3 n'ouvre pas de dépense non plafonnée** : le chemin
   déclenché est déjà borné en amont et en aval —
   `pipeline-forge-run.yml:121` (`ci_budget_guard.py precheck`, plafond
   mensuel), `:192` (`--max-budget-usd 5.00`, plafond natif qui coupe avant
   la dépense), `:202` (`record`, marquage post-hoc). C'est la configuration
   que la littérature sur le budget de jetons recommande : un plafond que
   l'agent ne peut pas contourner, à l'étage de l'orchestration, et non un
   simple compteur ([S7], [S8]).
8. **Les garde-fous de l'ADR ne sont pas des ajouts du rédacteur.** J'ai
   comparé garde-fou par garde-fou avec la demande déjà fusionnée
   (`DEMANDE-...:102-106`) : confirmation, jeton minimal, trace,
   `127.0.0.1` y figurent déjà. Le constat « le rédacteur a fait passer ses
   propres exigences pour une décision du propriétaire » était mon hypothèse
   de départ ; il est **réfuté** et n'est donc pas émis.
9. **L'ADR suit le gabarit** (`docs/adr/template.md`) : Date, Status,
   Deciders, Context, Decision, trois alternatives argumentées avec « Why
   not », Consequences en Positive / Negative / Risks.

# 7. Briefs atomiques proposés (3 — aucun n'est autorisé par ce document)

Propositions, pas instructions. Un audit n'instruit rien : seule une
conversion explicite par le propriétaire en fait un brief, et le brief
devient alors la source unique d'instruction (`CLAUDE.md` › Single Source of
Instruction ; `architecture/README.md`).

1. **Rendre lisible et enregistrable ce qui doit être vrai avant une fusion
   exécutée sur ordre** — soit en portant dans le dépôt la liste des lectures
   à effectuer et à consigner, soit en inscrivant noir sur blanc qu'ADR-0011
   déroge à `docs/rules/conditional-merge-gate.md` et que la délégation porte
   sur un clic non vérifié. Répond à P1-1, P1-2, P2-2.
2. **Garde mécanique de complétude de l'index des ADR** : un contrôle qui
   échoue lorsqu'un `docs/adr/NNNN-*.md` suivi par Git n'a pas de ligne dans
   `docs/adr/README.md`. Répond à P1-4, et déplace la classe entière du
   jugement humain vers la machine (lentille 3).
3. **Faire porter la garde de périmètre sur l'identité de l'acteur plutôt
   que sur le préfixe de branche** : `audit-guard / cursor-scope` juge
   aujourd'hui une chaîne choisie par l'auteur de la PR. Répond à P2-3.

# 8. Commandes rejouées — sorties collées

```
$ .venv/bin/python -m pytest harness/tests/ -q
........................................................................ [ 22%]
........................................................................ [ 44%]
........................................................................ [ 66%]
......................ssssssssssssssss.................................. [ 88%]
.....................................                                    [100%]
309 passed, 16 skipped in 16.74s
```

```
$ gh pr checks 34 | sort
actionlint                      pass  11s
actionlint                      pass  14s
check-and-automerge             skipping  0
cursor-scope                    skipping  0
cursor-scope                    skipping  0
f0-demo                         pass  10s
f0-demo                         pass  12s
gitleaks                        pass  12s
gitleaks                        pass  14s
invoke-cursor-auditor           pass  21s
Reconcile local Hermes state    pass  7s
schema                          pass  12s
schema                          pass  13s
tests                           pass  25s
tests                           pass  27s
```

```
$ gh pr view 34 --json createdAt,mergedAt,mergedBy,mergeCommit,reviews
{"created":"2026-08-12T12:24:28Z","merged":"2026-08-12T12:25:24Z",
 "mergedBy":"PLiagre","mergeCommit":"0269d8e90231e554db356cbc57aea1f70bc3f507",
 "reviewDecision":"","reviews":0}
```

```
$ git grep -l "bb8fe11b860f8383e5178994f35ca116f89da2fd" origin/master -- architecture/
aucun audit ne vise ce SHA
```

```
$ git show origin/master:docs/adr/README.md | rg -c "0011"
0 occurrence

$ git log --oneline -3 -- docs/adr/README.md
9ad76ff ADR-0010: Hermes chef de projet, ROADMAP.md, contrat hermes/, guide de critique sourcé pour Cursor
42679d7 harness: ajouter Codex comme backend officiel du lot 010b
62a0fe2 harness: Générateur lot 010a -- le contrôle d'auto-jugement distingue enfin l'acteur du rôle
```

```
$ for c in bb8fe11 e641c0b; do git show --stat --format= $c; done
 ROADMAP.md                                         | 23 +++++++++++++++-------
 ...NDE-20260812-hermes-tableau-de-bord-pilotage.md | 22 ++++++++++++++++++++-
 2 files changed, 37 insertions(+), 8 deletions(-)
 docs/adr/0011-hermes-console-du-proprietaire.md | 120 ++++++++++++++++++++++++
 hermes/README.md                                |  11 +++
 2 files changed, 131 insertions(+)
```

```
$ rg -n "workflow_dispatch|brief_dir|ci_budget_guard|max-budget-usd" \
    .github/workflows/pipeline-forge-run.yml
31:  workflow_dispatch:
33:      brief_dir:
34:        description: "harness/queue/briefs/NNN-<slug> to run"
121:          python harness/pipeline/ci_budget_guard.py precheck
192:            --max-budget-usd 5.00 \
202:          python harness/pipeline/ci_budget_guard.py record \
```

# 9. Sources externes

Recherche conduite le 2026-08-12 sur les trois thèmes imposés par
`architecture/agents/cursor-auditor.md` § Preuve de fin. Ce sont des sources
secondaires d'autorité inégale (billets d'ingénierie, un préprint arXiv non
relu par les pairs) ; elles servent à situer les constats dans l'état de
l'art 2026, jamais à trancher un fait du dépôt — pour cela, seules les
preuves des sections 2 à 8 comptent.

| # | thème | source | consulté le |
|---|---|---|---|
| S1 | autonomous AI dev pipeline | Velyr — *What Is the Approval-Gate Pattern for AI Code Changes?* — <https://velyr.io/blog/approval-gate-pattern-ai-code-changes> | 2026-08-12 |
| S2 | autonomous AI dev pipeline | stdub.org — *The Merge Gate* (2026-06-10) — <https://stdub.org/ai/technical/2026/06/10/The-Merge-Gate.html> | 2026-08-12 |
| S3 | autonomous AI dev pipeline | DEV Community — *Evidence Gates for AI Coding Agents in CI — Recoverable Merge over Mean Time to Green* — <https://dev.to/lo_an_e746e473b842ff53cf9/evidence-gates-for-ai-coding-agents-in-ci-recoverable-merge-over-mean-time-to-green-2a8h> | 2026-08-12 |
| S4 | agent orchestration CI | Safeguard — *Prompt Injection in CI/CD Pipelines: Attack Paths and Defenses* — <https://safeguard.sh/resources/blog/prompt-injection-in-ci-cd-pipelines-attack-paths-and-defenses> | 2026-08-12 |
| S5 | agent orchestration CI | Iternal — *AI Agent Security Checklist* (OWASP LLM01/LLM06, NIST AI RMF) — <https://iternal.ai/ai-agent-security-checklist> | 2026-08-12 |
| S6 | agent orchestration CI | arXiv (préprint) — *GitInject: Real-World Prompt Injection Attacks in AI-Powered CI/CD Pipelines* — <https://arxiv.org/html/2606.09935v1> | 2026-08-12 |
| S7 | token budget LLM agents | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30) — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-08-12 |
| S8 | token budget LLM agents | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers* (2026) — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026-08-12 |

Ce que ces sources disent, en une phrase par thème :

- **Porte de fusion** : le motif dominant reste « l'agent propose, l'humain
  décide », et la garantie doit être tenue par la plateforme (permissions,
  protection de branche, politique en code) et non par une consigne que
  l'agent est censé respecter ; toute affirmation « ça marche » doit
  s'accompagner d'un dossier de preuve rejouable [S1, S2, S3].
- **Orchestration d'agents en CI** : l'injection de prompt n'est pas
  éliminable par filtrage ; la seule défense fiable est de ne jamais réunir
  dans la même session l'entrée non fiable, l'accès en écriture et les
  secrets, et de préférer des identifiants courts et cadrés par action
  [S4, S5, S6].
- **Budget de jetons** : un plafond n'a de valeur que s'il coupe avant la
  dépense et hors de portée de l'agent ; un compteur qui mesure sans arrêter
  n'est pas un plafond [S7, S8].

# 10. Limites de cet audit

- **Lecture seule.** Aucun fichier hors `architecture/inbox/` n'a été
  modifié ; les trois flags `*_authorized` valent `false` ; ce document ne
  prononce ni `APPROVED` ni `REJECTED`, ce qui n'appartient pas à l'auditeur
  (`architecture/agents/cursor-auditor.md` § Identité).
- **L'installation Hermes locale est hors dépôt** et donc invérifiable
  d'ici : H1 et H4 ne sont pas auditables, seules leurs traces dans le dépôt
  le sont. Les constats P1-3 et P2-1 portent sur ce que l'ADR **écrit** de
  cette installation, pas sur son état réel.
- **La PR était déjà fusionnée** (12:25:24) quand cet audit a été produit —
  fait dont l'audit tire un constat plutôt que de le taire.
- **Un constat candidat a été réfuté et retiré** avant émission (garde-fous
  prétendument ajoutés par le rédacteur, cf. section 6 point 8) ; un autre a
  été volontairement non émis (la délégation de rédaction à Cursor, déjà
  tranchée — cf. P2-3).
- **Aucun P0**, et cet audit ne demande aucune remise en cause de la
  décision propriétaire elle-même : Hermes comme console du propriétaire
  est un choix qui appartient au propriétaire. Les constats portent sur ce
  que l'ADR affirme sans le mesurer, non sur ce qu'il décide.
