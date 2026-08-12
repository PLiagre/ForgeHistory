---
audit_id: CURSOR-3ce7947-pr36-hermes-skill-versionnee
auditor: cursor-cloud
target_branch: forge/hermes-skill-versionnee-c2dd
target_commit: 3ce79475ea7f23bf02074c010ddeab645e8c790c
created_at: 2026-08-12T13:05:00Z
audit_type: pull-request-critique
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# Audit de la PR #36 — « hermes: la skill de suivi ForgeHistory entre dans le dépôt (hermes/skills/) »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006). Les trois flags `*_authorized` sont à `false` : rien ici n'est
pré-autorisé.

## 1. Provenance et périmètre

| | |
|---|---|
| PR | <https://github.com/PLiagre/ForgeHistory/pull/36> |
| Auteur affiché | `PLiagre` ; commit signé `Cursor Agent <cursoragent@cursor.com>` |
| Branche | `forge/hermes-skill-versionnee-c2dd` |
| Tête auditée | `3ce79475ea7f23bf02074c010ddeab645e8c790c` |
| Base | `master` (`MERGEABLE`, `mergeStateStatus: UNSTABLE`) |
| Diff | 2 fichiers, +90 / −0 |
| État | `OPEN`, non brouillon, ouverte le 2026-08-12T12:33:54Z |

Contenu du diff :

```
 hermes/README.md                             |  4 +
 hermes/skills/forgehistory-suivi/SKILL.md    | 86 +++++++++++++++++++++
 2 files changed, 90 insertions(+)
```

Intention déclarée (description de la PR) : « lier son installation Hermes
locale au dépôt, pour que la skill de suivi soit maintenable par PR et
récupérée par un simple `git pull` ». L'intention est donc **lisible et
sourcée** (demande propriétaire du 2026-08-12) — la lentille 1 est
formellement satisfaite ; c'est sur la **contrainte** qui encadre cette
intention que porte le constat P0 ci-dessous.

## 2. Classification de la CI du commit audité

Relevé sur `3ce7947` (`gh pr checks 36`, sortie collée en § 5) :

| Job (workflow) | Résultat |
|---|---|
| `tests`, `f0-demo` (harness-ci) | **vert** |
| `schema`, `cursor-scope` (audit-guard) | `schema` **vert** ; `cursor-scope` **ignoré** (branche non `cursor/*`) |
| `actionlint`, `gitleaks` (security) | **vert** |
| `invoke-cursor-auditor` (pipeline-audit) | **vert** (c'est l'appel qui a produit cet audit) |
| `check-and-automerge` (merge-bot) | **ignoré** (branche `forge/*` hors `bot_branches`) |
| `Reconcile local Hermes state` (hermes-observer) | **en attente (`QUEUED`) — jamais démarré** |

**Aucun job rouge.** Mais la CI n'est pas « verte » pour autant : un check
reste indéfiniment en attente, ce qui explique `mergeStateStatus: UNSTABLE`
(constat P2-9).

## 3. Constats

| # | Sévérité | Constat |
|---|---|---|
| P0-1 | **P0** | ADR-0011 dit que ce câblage entre dans le dépôt **par un brief** ; la PR l'y met sans brief et sans amender l'ADR. |
| P1-2 | **P1** | La commande « lancer un brief » de la skill utilise un paramètre qui n'existe pas (`brief=` au lieu de `brief_dir=`) : elle échoue à coup sûr. |
| P1-3 | **P1** | La skill recopie ADR-0011 et la porte de fusion en quatre preuves, alors que `hermes/README.md` déclare explicitement, pour ce même contenu, ne pas le paraphraser. |
| P2-4 | **P2** | La preuve d'exécution citée (309 tests verts) ne touche pas le diff : elle est identique avant et après. |
| P2-5 | **P2** | Aucune porte mécanique ne couvre `hermes/skills/**` : le nouveau chemin entre sans filet. |
| P2-6 | **P2** | Une dizaine de commandes livrées sans une seule preuve rejouée ; deux sont contredites par le dépôt lui-même. |
| P2-7 | **P2** | La skill crée une seconde lecture directe de l'API Cursor pour une donnée que le tableau de bord calcule déjà — contre sa propre règle et contre le principe « une seule source de vérité ». |
| P2-8 | **P2** | La commande d'installation n'existe que dans la description de la PR, pas dans le dépôt : l'objectif « maintenable par PR » n'est pas atteint pour l'étape la plus fragile. |
| P2-9 | **P2** | Un check de CI en attente permanente rend inapplicable telle quelle la règle « CI verte » que la skill impose à Hermes avant toute fusion. |
| P3-10 | **P3** | Traçabilité d'auteur affaiblie sur ce chemin : commit préfixé `hermes:` mais signé par l'agent Cursor, et fichier sans champ `author:`. |
| P3-11 | **P3** | La garde « Cursor ne développe pas » est indexée sur le **nom de branche**, pas sur l'identité de l'agent. |

### P0-1 — ADR-0011 exige un brief pour exactement ce contenu

ADR-0011 (accepté, daté du même jour que la PR) écrit, lignes 69–71 :

> « Le câblage concret (skill locale, outils, jeton) est de la configuration
> de l'installation locale — **hors dépôt**. Si un jour une partie de ce
> câblage doit entrer dans le dépôt, elle passera **par un brief, comme tout
> code**. »

La PR ajoute précisément « la skill locale », sans brief sous
`harness/queue/briefs/`, sans passage par le gate (`harness/verdict_audit.py`)
ni par un Évaluateur, et **sans modifier ADR-0011** (les deux seuls fichiers
touchés sont `hermes/README.md` et le nouveau `SKILL.md`).

Conséquence concrète : si la PR est fusionnée telle quelle, le dépôt contient
en même temps une décision enregistrée qui dit « hors dépôt, sinon par un
brief » et le fichier qui la contredit. Ce n'est pas une question de goût :
c'est le mécanisme de gouvernance du projet (une décision écrite, une seule
source d'instruction) qui devient faux dans son propre dépôt. La littérature
2026 sur les pipelines d'agents nomme exactement ce risque : une autonomie
bornée ne tient que si les invariants sont **appliqués de l'extérieur** et non
réinterprétés au moment de l'exécution [E1] ; la gouvernance doit être un
opérateur du pipeline, pas un commentaire *a posteriori* [E2].

Deux sorties possibles, aucune n'appartient à cet audit : (a) amender ADR-0011
(ou déposer une décision propriétaire dans `architecture/decisions/`) pour
enregistrer la dérogation, (b) faire passer le contenu par un brief comme
l'ADR le prévoit. Ce que l'audit demande, c'est que **le dépôt ne se
contredise pas** après fusion.

**Preuve** : `docs/adr/0011-hermes-console-du-proprietaire.md:69-71` ;
liste des fichiers modifiés (§ 1) ; `git log` de la branche (§ 5) ne montre
qu'un commit, aucun brief.

### P1-2 — la commande « lancer un brief » ne peut pas fonctionner

`hermes/skills/forgehistory-suivi/SKILL.md:69` :

```
- lancer un brief : `gh workflow run pipeline-forge-run.yml -R PLiagre/ForgeHistory -f brief=<dossier du brief>`.
```

Or le workflow déclare un seul paramètre, nommé `brief_dir`, et **obligatoire**
(`.github/workflows/pipeline-forge-run.yml:32-35`, sortie collée en § 5).
Un `workflow_dispatch` appelé avec une clé inconnue et sans la clé requise est
rejeté par l'API : la troisième des quatre actions de pilotage d'ADR-0011 est
donc inopérante dès la première utilisation.

Je n'ai **pas** exécuté la commande pour le démontrer : déclencher un workflow
est une écriture, interdite à ce rôle (`architecture/agents/cursor-auditor.md`
› Identité). La preuve est la définition du workflow, qui est sans ambiguïté.

C'est le prototype du piège « code généré par IA » de la lentille 6 : une
commande plausible, jamais exécutée, qui décrit un paramètre que le dépôt
n'a pas.

**Preuve** : `SKILL.md:69` vs `.github/workflows/pipeline-forge-run.yml:32-35`.

### P1-3 — la skill paraphrase ce que le README refuse de paraphraser

`hermes/README.md:47-56` (déjà dans `master`) renvoie à ADR-0011 pour le
périmètre et les garde-fous, et se termine par : « voir l'ADR — ce fichier ne
les paraphrase pas » (ligne 56). La skill ajoutée fait l'inverse : elle redonne les
quatre actions, les garde-fous (« reformuler l'action et attendre un “oui” »),
et surtout **la porte de fusion en quatre preuves** (« CI verte, gate ACCEPT,
verdict d'un acteur ≠ producteur, audit Cursor », `SKILL.md:65-67`), copie de
`docs/adr/0011-hermes-console-du-proprietaire.md:30-33`.

Le problème n'est pas la redondance en soi, c'est que ces quatre preuves sont
la **porte de fusion du projet** : le jour où elle évolue, la copie dans la
skill reste vraie sur le PC du propriétaire et devient fausse dans le dépôt,
sans que rien ne le signale. C'est la dérive documentée entre fichiers
d'instructions d'agents, dont l'effet mesuré est négatif (baisse du taux de
réussite, coût d'inférence en hausse de plus de 20 % dans l'étude citée par
[E5]).

Nuance en faveur de la PR : un `SKILL.md` est chargé **à la demande**, pas à
chaque tour, donc son poids en tokens n'est pas une taxe permanente comme le
serait du texte ajouté à `CLAUDE.md` ou `AGENTS.md` [E5, E6]. Le format choisi
est le bon ; c'est le contenu dupliqué qui est en cause. Un renvoi (« les
conditions de fusion sont celles d'ADR-0011 § Decision, ne pas les recopier »)
tiendrait le même rôle sans créer de second exemplaire.

**Preuve** : `SKILL.md:59-75` vs `docs/adr/0011-...:28-56` et
`hermes/README.md:47-56`.

### P2-4 — la preuve d'exécution citée ne touche pas le diff

La description de la PR ne présente qu'une validation : `pytest harness/tests/`
→ « 309 passed, 16 skipped ». J'ai rejoué la suite deux fois (§ 5) :

- sur la tête de la PR `3ce7947` : `309 passed, 16 skipped`
- sur la base `dd16d76` (sans le diff) : `309 passed, 16 skipped`

Le chiffre est donc **honnête mais muet** : il est identique avec et sans le
changement, parce qu'aucun test n'exerce les fichiers ajoutés. Selon la
lentille 2, la forme forte d'une preuve est un test qui échoue avant et passe
après ; ici il n'y a rien à faire échouer. Ce n'est pas une faute grave pour de
la documentation — mais alors il faut le dire, et ne pas présenter 309 tests
verts comme la validation du diff.

**Preuve** : les deux sorties `pytest` de § 5 ; section « Validation » de la
description de la PR.

### P2-5 — le nouveau chemin n'est couvert par aucune porte mécanique

Quatre vérifications l'attestent, toutes reproduites en § 5 :

1. `harness/audit_schema.py` ne valide que `architecture/inbox/` ;
2. `harness/tests/test_single_source_of_instruction.py:13-16` ne cherche que
   deux titres littéraux (« Success Conditions » et « Non-Goals », précédés de
   deux dièses) : il ne peut pas voir une paraphrase par le sens, donc pas
   celle du P1-3. Vérifié en pratique et de la manière la plus directe : la
   première version de cet audit citait ces deux titres verbatim, et le test
   est passé au rouge sur **l'audit lui-même** (`1 failed, 308 passed`) avant
   que je reformule la citation. La garde reconnaît une chaîne de caractères,
   pas une intention ;
3. `.github/workflows/pipeline-audit.yml:67` classe toute poussée `hermes/`
   comme « documentaire » → **aucun audit après fusion** sur ce chemin ;
4. `.github/merge-bot.yaml:29-32` n'autorise pas `hermes/**` — ce qui est
   voulu (relecture humaine) et donc le **seul** filet restant.

Autrement dit, la relecture humaine est ici la totalité du contrôle, pour un
fichier qui décrit des actions privilégiées (fusionner, déclencher un
workflow). La lentille 3 demande l'inverse : laisser le mécanique couvrir ce
qu'il peut, et réserver le jugement à ce qu'il ne voit pas. Deux des constats
ci-dessus (P1-2 factuel, P2-6) sont précisément du ressort d'un test.

**Preuve** : les quatre fichiers et lignes cités.

### P2-6 — une dizaine de commandes, zéro preuve rejouée, deux contredites

La skill livre environ onze commandes (`gh api contents`, `gh pr list`,
`gh run list`, `curl` API Cursor, `gh pr merge`, `gh pr close`, pose/retrait de
label, `gh workflow run`). Aucune n'est accompagnée d'une sortie. Deux sont
contredites par le dépôt :

- `SKILL.md:69` : paramètre inexistant (constat P1-2) ;
- `SKILL.md:43` : `Authorization: Bearer $CURSOR_API_KEY`, alors que les
  **deux seuls appels vérifiés** du dépôt à cette API utilisent
  `Authorization: Basic base64("<clé>:")`
  (`pipeline-audit.yml:126`, `hermes-dashboard.yml:65`, sortie en § 5), et que
  `pipeline-audit.yml:12` précise « authentification Basic ». La forme Bearer
  n'est peut-être pas fausse côté fournisseur, mais elle diverge de la seule
  forme dont ce dépôt a la preuve qu'elle marche, sans dire pourquoi.

Deux défauts voisins, même origine :

- `SKILL.md:34` affirme « toutes les commandes utilisent
  `GH_TOKEN=$FORGEHISTORY_GH_TOKEN` », ce que `SKILL.md:43` démentit neuf
  lignes plus loin : cette ligne exige `CURSOR_API_KEY`, variable **absente
  de la section Prérequis** (`SKILL.md:19-30`). Sur une installation neuve,
  la question « quels agents tournent ? » reste donc sans réponse ;
- le retrait du label passe par `.../labels/pipeline%2Fpause` (`SKILL.md:68`) :
  un nom de label contenant une barre oblique encodée dans un segment de
  chemin. C'est peut-être correct, ce n'est pas prouvé, et c'est le
  coupe-circuit d'urgence — le pire endroit pour une commande non testée.

Sur le plan sécurité, la note E3/E4 est directe : pour un agent, on veut des
jetons courts, à portée minimale, et une approbation hors-bande pour les
actions à fort impact [E3] ; un jeton statique de longue durée posé en variable
d'environnement est explicitement le motif à éviter [E4]. La skill fait
l'inverse par construction (PAT longue durée, puis « remplacer par un PAT en
écriture »). Ce choix est peut-être inévitable pour un poste Windows
personnel — mais il mérite d'être écrit comme un arbitrage assumé, avec sa
procédure de révocation, pas comme une ligne de prérequis.

**Preuve** : `SKILL.md:19-30`, `:34`, `:43`, `:68`, `:69` ;
`pipeline-audit.yml:12,126` ; `hermes-dashboard.yml:65` ; [E3], [E4].

### P2-7 — une deuxième lecture directe pour une donnée déjà calculée

`SKILL.md:43` fait appeler l'API Cursor en direct pour lister les agents en
cours. Or `hermes/DASHBOARD.md:41` contient déjà la section « Agents lancés
récemment (Cursor Cloud) », alimentée par le même appel côté CI
(`hermes-dashboard.yml:56-70`). La skill se contredit d'ailleurs elle-même
quatorze lignes plus bas : « Ne jamais recalculer ce que le tableau donne
déjà ; citer la source » (`SKILL.md:57`).

L'effet architectural est celui que `CLAUDE.md` interdit en principe n°1 (« une
seule source de vérité — les vues ne deviennent jamais des bases parallèles ») :
deux chemins de lecture pour la même donnée, avec deux fenêtres temporelles
différentes, donc deux réponses possibles à la même question. Effet de bord
concret : la clé d'administration Cursor doit alors vivre aussi sur le PC du
propriétaire, alors qu'elle n'était jusqu'ici qu'un secret de CI.

**Preuve** : `SKILL.md:43` et `:57` ; `hermes/DASHBOARD.md:41` ;
`.github/workflows/hermes-dashboard.yml:56-70` ; `CLAUDE.md` § Non-Negotiable
Principles n°1.

### P2-8 — l'étape d'installation reste hors du dépôt

`SKILL.md:19-21` renvoie, pour créer la jonction Windows, à « la commande
donnée dans la PR qui a introduit ce fichier ». Après fusion, cette commande
n'existe que dans la description de la PR #36 : un texte non versionné,
modifiable, invisible depuis un `git pull`. Le but affiché de la PR
(« maintenable par PR et récupérée par un simple `git pull` ») n'est donc pas
atteint pour l'étape la plus fragile, celle qui décide si la skill est lue ou
pas.

**Preuve** : `SKILL.md:19-21` ; description de la PR § « Côté PC du
propriétaire ».

### P2-9 — la règle « CI verte » est inapplicable telle quelle

La skill impose à Hermes de refuser une fusion si une preuve manque, la
première étant « CI verte » (`SKILL.md:65-67`). Or le workflow
`hermes-observer.yml:32` tourne sur un runner auto-hébergé
(`[self-hosted, Windows, X64, hermes-observer]`) : le PC du propriétaire. Quand
ce PC est éteint, le check « Reconcile local Hermes state » reste `QUEUED`
indéfiniment. C'est le cas **sur cette PR même** (§ 2) et sur les huit derniers
runs du workflow, tous `queued` (§ 5) ; c'est aussi la cause du
`mergeStateStatus: UNSTABLE`.

Deux conséquences, dans les deux sens : appliquée à la lettre, la règle bloque
toute fusion tant que le PC est éteint ; appliquée avec souplesse, elle
s'érode — et l'érosion silencieuse de la porte est exactement ce que la
décision du 2026-08-11 cherchait à empêcher. Nuance : le cron n°2 de la skill
(`SKILL.md:82-86`) ne surveille que `conclusion: failure`, donc une file
bloquée en attente ne déclenche aucune alerte : la panne est invisible.

Ce constat n'est pas propre à la PR #36 — mais la PR est le premier document
qui **s'appuie** sur « CI verte » comme sur un fait mécanique, ce qui le rend
opposable ici.

**Preuve** : `gh pr checks 36` (§ 2 et § 5) ; `gh run list --workflow
hermes-observer.yml` (§ 5) ; `.github/workflows/hermes-observer.yml:30-32` ;
`SKILL.md:65-67` et `:82-86`.

### P3-10 — traçabilité d'auteur affaiblie sur ce chemin

`hermes/README.md:73-74` pose la règle : « L'auteur est toujours traçable :
`author: hermes` dans le frontmatter **et** un message de commit qui commence
par `hermes:` ». Le fichier ajouté n'a pas de champ `author:` (son frontmatter
est `name` + `description`), et le commit préfixé `hermes:` est signé
`Cursor Agent <cursoragent@cursor.com>` (§ 5). La nouvelle ligne du tableau
légitime cette exception de format sans la nommer comme telle : sur
`hermes/skills/**`, la règle de traçabilité perd son objet, et le préfixe de
commit `hermes:` désigne désormais un chemin plutôt qu'un auteur.

**Preuve** : `hermes/README.md:73-74` et la ligne ajoutée (`hermes/README.md:22`
à la tête de la PR) ; `git log` de § 5 ; frontmatter de `SKILL.md:1-8`.

### P3-11 — la garde « Cursor ne développe pas » dépend d'un nom de branche

Le job `cursor-scope` d'`audit-guard` ne s'active que si la branche commence
par `cursor/` (`audit-guard.yml:28-30`). La PR le dit sans détour dans sa
description (« branche `forge/*` […] le job `cursor-scope` réserve `cursor/*`
aux dépôts d'audits »), et le résultat est visible : `cursor-scope` **ignoré**
(§ 2), alors que le commit est signé par un agent Cursor. La garde vérifie donc
une convention de nommage, pas l'identité de l'acteur.

Deux précautions pour ne pas transformer ce constat en reproche mal placé.
D'abord, le propriétaire a déjà délégué de la rédaction à Cursor en dehors de
`inbox/` : ADR-0011 lui-même porte « rédaction déléguée à Cursor »
(`0011-...:8`). Le motif « Cursor ne doit pas écrire » est donc déjà tranché en
pratique, et le répéter serait du bruit
(`architecture/review-guidelines.md` § Forme imposée). Ensuite, le même angle
mort me concerne : cet audit est produit par un agent Cursor sur une PR produite
par un agent Cursor. La séparation producteur/critique de la lentille 4 est ici
tenue par deux exécutions distinctes, pas par deux outils distincts :
indépendance partielle, à garder en tête en lisant cet audit.

**Preuve** : `.github/workflows/audit-guard.yml:28-30` ; `gh pr checks 36`
(§ 2) ; `git log` (§ 5) ; `docs/adr/0011-...:8`.

## 4. Lecture par les six lentilles

| Lentille | Verdict | Renvoi |
|---|---|---|
| 1. Intention avant diff | Intention lisible et sourcée ; **contrainte violée** | P0-1 |
| 2. Preuve d'exécution | Preuve honnête mais sans lien avec le diff ; commandes non rejouées | P2-4, P2-6 |
| 3. Portes mécaniques d'abord | Le chemin n'a aucune porte ; le jugement humain fait tout | P2-5, P2-9 |
| 4. Cadrage adverse | Tenu par deux exécutions, pas deux outils — dit ouvertement | P3-11 |
| 5. Taille et découpage | **Conforme** : 2 fichiers, +90 / −0, loin des seuils (~5 fichiers, quelques centaines de lignes). Aucun découpage à demander. | — |
| 6. Pièges du code généré par IA | Commande hallucinée, forme d'auth divergente, secret non déclaré, duplication d'instruction | P1-2, P1-3, P2-6, P2-7 |

## 5. Commandes rejouées (sorties collées)

```
$ gh pr checks 36 -R PLiagre/ForgeHistory
check-and-automerge     skipping  0    .../runs/31597053734/job/94115021331
cursor-scope            skipping  0    .../runs/31597053742/job/94115021595
actionlint              pass      11s  .../runs/31597053750/job/94115020886
gitleaks                pass      16s  .../runs/31597053750/job/94115020642
schema                  pass      12s  .../runs/31597053742/job/94115020672
tests                   pass      24s  .../runs/31597053735/job/94115020928
f0-demo                 pass      14s  .../runs/31597053735/job/94115020790
invoke-cursor-auditor   pass      19s  .../runs/31597053754/job/94115020998
Reconcile local Hermes state  pending  0  .../runs/31597053851/job/94115022032
```

```
$ git log --format="%H | %an <%ae> | %s" origin/master..pr36
3ce79475ea7f23bf02074c010ddeab645e8c790c | Cursor Agent <cursoragent@cursor.com> | hermes: la skill de suivi ForgeHistory entre dans le dépôt (hermes/skills/)
```

```
$ .venv/bin/python -m pytest harness/tests/ -q      # tête de la PR (3ce7947)
309 passed, 16 skipped in 17.31s

$ .venv/bin/python -m pytest harness/tests/ -q      # base sans le diff (dd16d76)
309 passed, 16 skipped in 17.47s
```

```
$ grep -n -A4 "inputs:" .github/workflows/pipeline-forge-run.yml | head -8
32:    inputs:
33-      brief_dir:
34-        description: "harness/queue/briefs/NNN-<slug> to run"
35-        required: true
36-  issues:
```

```
$ grep -rn "Authorization: Basic" .github/workflows/
.github/workflows/pipeline-audit.yml:126:          auth_header="Authorization: Basic $(printf '%s:' "$CURSOR_API_KEY" | base64 -w0)"
.github/workflows/hermes-dashboard.yml:65:          auth_header="Authorization: Basic $(printf '%s:' "$CURSOR_API_KEY" | base64 -w0)"
```

```
$ grep -n -A4 "^allow_paths:" .github/merge-bot.yaml
29:allow_paths:
30-  - "architecture/inbox/**"
31-  - "architecture/reviews/**"
32-  - "harness/queue/briefs/**/feedback/**"
```

```
$ gh run list -R PLiagre/ForgeHistory --workflow hermes-observer.yml --limit 8 \
    --json status,conclusion,headBranch,event
# 8 runs sur 8 : "status": "queued", "conclusion": ""  (runner auto-hébergé absent)
```

```
$ python3 harness/audit_schema.py        # sur la tête de la PR
All 11 audit(s) valid.

$ python3 harness/audit_schema.py        # avec le présent audit ajouté
OK   CURSOR-3ce7947-pr36-hermes-skill-versionnee.md
All 12 audit(s) valid.

$ .venv/bin/python -m pytest harness/tests/ -q   # avec le présent audit ajouté
309 passed, 16 skipped in 17.31s
```

```
$ grep -n '^#\{1,3\} ' hermes/DASHBOARD.md
...
41:## Agents lancés récemment (Cursor Cloud)
...
```

## 6. Briefs atomiques proposés (3 maximum — 3 ici)

Ces briefs sont des **propositions**. Aucun n'est autorisé par cet audit ;
seul le propriétaire, via la boucle, peut en faire des briefs réels.

### Brief A — remettre le dépôt d'accord avec lui-même (P0-1, P1-3)

Objet : faire en sorte qu'après fusion, ADR-0011 et le contenu de
`hermes/skills/**` disent la même chose. Deux voies exclusives, au choix du
propriétaire : amender ADR-0011 (ou enregistrer une décision dans
`architecture/decisions/`) pour acter que le câblage local est versionné ; ou
retirer le fichier de cette PR et le faire entrer par un brief comme l'ADR le
prévoit. Dans les deux cas, remplacer dans la skill la copie des quatre
preuves de fusion et des garde-fous par un **renvoi** à ADR-0011.

### Brief B — prouver chaque commande de la skill (P1-2, P2-6, P2-8)

Objet : corriger `brief=` → `brief_dir=`, aligner la forme d'authentification
Cursor sur celle dont le dépôt a la preuve (ou justifier l'écart), déclarer
`CURSOR_API_KEY` dans les prérequis, vérifier le retrait du label
`pipeline/pause`, et rapatrier la commande de jonction Windows dans le fichier.
Livrable attendu : pour chaque commande, la sortie rejouée, comme l'exige la
lentille 2.

### Brief C — une porte mécanique pour `hermes/**` (P2-5)

Objet : un test qui lise les blocs de commandes de `hermes/**` et vérifie que
tout `gh workflow run <fichier> -f <clé>=` correspond à un input réellement
déclaré dans le workflow cité, et que tout `SKILL.md` de `hermes/skills/`
porte un frontmatter minimal. But : que les constats P1-2 et P2-6 ne puissent
plus atteindre `master` sans qu'une machine le dise d'abord.

Hors briefs, et volontairement : le constat P2-9 (check en attente permanente)
touche la CI et la définition de la porte de fusion. Un audit ne propose pas de
changement de CI (`ci_changes_authorized: false`) ; ce point relève d'une
décision du propriétaire.

## 7. Ce que cet audit ne fait pas

Il ne décide pas, n'autorise rien, n'ordonne aucune implémentation. Les trois
flags `*_authorized` valent `false`. Il ne modifie aucun audit existant
(`inbox/` est append-only : ce fichier est nouveau). Aucune écriture n'a été
faite en dehors de `architecture/inbox/**` — en particulier aucun `gh workflow
run`, aucun commentaire de PR, aucune fusion.

## Sources externes

Trois thèmes imposés par le contrat de rôle (`architecture/agents/cursor-auditor.md`
› Preuve de fin) : pipeline de développement autonome, orchestration d'agents en
CI, budget de tokens des agents.

| # | thème | source | date | consulté le |
|---|---|---|---|---|
| E1 | pipeline autonome | *Governance Models for Agentic Software Delivery* (v1), Preprints.org — « bounded autonomy » : invariants appliqués de l'extérieur, autorité humaine d'annulation inconditionnelle — <https://www.preprints.org/manuscript/202605.1737> | mai 2026 | 2026-08-12 |
| E2 | pipeline autonome | *Controlled Agentic AI Systems: A Governance-Driven Architecture for Auditable and Reproducible Decision Pipelines*, MDPI MAKE 8(5):125 — la gouvernance comme opérateur du pipeline, pas mécanisme *a posteriori* — <https://www.mdpi.com/2504-4990/8/5/125> | 2026 | 2026-08-12 |
| E3 | orchestration d'agents en CI | *AI Agent Security Checklist*, iternal.ai — identité par agent, jetons courts, approbation hors-bande pour les actions à fort impact — <https://iternal.ai/ai-agent-security-checklist> | non datée sur la page | 2026-08-12 |
| E4 | orchestration d'agents en CI | *Secrets Management for Agent-Driven Pipelines*, Augment Code — pourquoi un identifiant statique de longue durée est le motif à éviter dans un pipeline agentique — <https://www.augmentcode.com/guides/secrets-management-agent-pipelines> | non datée sur la page | 2026-08-12 |
| E5 | budget de tokens | *ctxweight*, dépôt GitHub — distinction « always-on » / « on-demand », et effet mesuré de la redondance entre fichiers d'instructions (étude ETH Zürich 2026 citée : réussite en baisse, coût en hausse > 20 %) — <https://github.com/GonzaloPeriane/ctxweight> | 2026 | 2026-08-12 |
| E6 | budget de tokens | *Context Engineering for Coding Agents 2026: What Works* — traiter la fenêtre de contexte comme un budget, garder les fichiers toujours chargés maigres — <https://www.heyuan110.com/posts/ai/2026-06-16-context-engineering-2026/> | 2026-06-16 | 2026-08-12 |

Référentiel interne appliqué : `architecture/review-guidelines.md` (six
lentilles, sources S1–S5) ; contrat de rôle
`architecture/agents/cursor-auditor.md` ; schéma de frontmatter
`architecture/README.md`.
