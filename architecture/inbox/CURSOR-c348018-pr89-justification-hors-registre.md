---
audit_id:                CURSOR-c348018-pr89-justification-hors-registre
auditor:                 cursor-cloud
target_branch:           forge/conversions-briefs-015-016-e180
target_commit:           c34801859355895f6c79495dc2d81582efd98bff
created_at:              2026-08-13T13:09:25Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #89 — conversions des audits moteur de la PR #69

Audit de la PR [#89](https://github.com/PLiagre/ForgeHistory/pull/89)
(21 fichiers, +3594 / −20 tels que GitHub les présente, base
`forge/cloture-audit-a4de4bb-e180` = `f978cc7`, tête `c348018`, branche
`forge/conversions-briefs-015-016-e180`).

Méthode : `architecture/review-guidelines.md` — six lentilles, sévérités
P0–P3, une preuve citée par constat. Rôle : auditeur en **lecture seule**.
Cet audit **n'instruit rien** et ne vaut pas décision
(`architecture/README.md`) : il propose, la boucle tranche.

Toutes les mesures ont été rejouées dans un arbre de travail séparé
(`git worktree add /tmp/pr89 pr89`), sans aucune écriture dans le dépôt
audité. Les sorties sont collées telles quelles au § 8.

## 0. Synthèse

**La tenue de registre est propre. Ce qui l'accompagne — la justification du
report de six audits, et la trace de la décision qui l'ordonne — n'existe
nulle part dans le dépôt.**

J'ai commencé par essayer de faire tomber ce que la PR affirme de
vérifiable. Deux affirmations sur trois tiennent, et elles tiennent bien :

- la résolution du conflit d'append de `architecture/audit-ledger.jsonl` est
  une **union sans perte** : aucune ligne de la base ni de `master` n'est
  perdue, aucun doublon, et la zone fusionnée n'a **aucune inversion
  chronologique** (§ 8.B) ;
- les quatre chemins de provenance cités par les deux graines de briefs
  (audit source + décision, pour chacune) **existent tous** au SHA audité
  (§ 8.A) ;
- la suite de tests du harnais est verte au SHA audité (314 passés,
  16 sautés) et la CI du commit `c348018` est verte sur tous les jobs qui
  concluent (§ 2).

Ce qui ne tient pas est dans les deux phrases de la description qui décident
quelque chose plutôt que de le constater.

La PR laisse **six audits approuvés sans conversion** au motif que « leur
substance recoupe le **lot 014 livré** ([PR #83](https://github.com/PLiagre/ForgeHistory/pull/83)) ».
Or la PR #83 est **ouverte, non fusionnée**, et le brief 014 tel qu'il est
présent dans l'arbre audité est une **graine vide** : six marqueurs
`<<TODO (planificateur)>>` et un répertoire `deliverables/` qui ne contient
que `.gitkeep` (§ 8.C). Pire : `master` porte déjà un audit de cette même
PR #83 (`CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre`,
`PROPOSED`, créé **quatre minutes avant** le commit audité) qui relève
**deux P0**, dont une injection de commande shell dans un workflow. Le motif
qui bloque six audits repose donc sur une livraison que le dépôt ne contient
pas et qu'un audit indépendant tient pour bloquée.

Et la décision qui prononce ce report est annoncée « tracée là-bas », au
HANDOFF. `HANDOFF.md` au SHA audité ne mentionne **aucun** des six SHA
cités, ni `015-`, ni `016-` : zéro occurrence sur huit motifs cherchés
(§ 8.D). La décision existe dans le message de commit et dans la
description de la PR — deux endroits que la fusion ne rend pas
interrogeables. C'est exactement le motif que l'audit
`CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre`, **approuvé** ce matin
avec 19 points retenus, nomme dans son titre ; ici l'artefact concerné est
différent (une décision de report, pas une clôture) et le lieu de trace
annoncé est vérifiablement vide, donc je le compte comme une instance neuve.

Deux constats P1, trois P2, trois P3. **Aucun P0** : rien ici ne casse un
comportement produit, et je ne recommande pas de bloquer la fusion — les
deux P1 se corrigent en éditant du texte (§ 3).

## 1. Intention avant diff (lentille 1)

L'intention est lisible et la description l'énonce d'emblée : « Objet (un
seul) : conversions en graines de briefs des deux audits **moteur**
approuvés ». C'est une PR de tenue de registre, pas de code.

Le diff **authentiquement écrit** par cette PR correspond à cette intention :
le commit `c348018` touche 7 fichiers pour 98 lignes ajoutées, zéro
supprimée — deux lignes de registre et les deux graines de briefs
(§ 8.E). Aucun code, aucun test, aucun workflow, aucun brief existant.

L'écart est entre cette intention et ce que GitHub présente au relecteur :
21 fichiers et +3594 lignes, dont **3496 arrivent par la fusion de `master`**
dans une branche empilée sur une PR non fusionnée (§ 8.E). Le relecteur voit
donc un diff qui traverse quatre domaines de propriété — `inbox/`,
`reviews/`, `decisions/`, `harness/queue/briefs/` — alors que
`architecture/README.md` fonde justement sa garantie sur le fait qu'« on peut
prouver mécaniquement **qui** a écrit **quoi** ». Cette preuve n'est pas
lisible depuis ce diff. Voir P3-2.

La description est par ailleurs honnête sur trois points qui pourraient
passer pour des omissions et n'en sont pas : elle annonce l'empilage, elle
demande de ne pas fusionner en squash, et elle signale le défaut d'acteur
du registre. Le reciblage automatique qu'elle promet est bien le
comportement GitHub réel pour une PR empilée dont la base est fusionnée puis
supprimée [S6].

## 2. Portes mécaniques d'abord (lentille 3) — classification de la CI

CI du SHA audité `c348018` : **verte**. Tous les jobs qui concluent
réussissent ; aucun échec.

| job | résultat |
|---|---|
| `tests` (×2 runs) | success |
| `sim-tests` (×2) | success |
| `f0-demo` (×2) | success |
| `schema` (×2) | success |
| `gitleaks` (×2) | success |
| `actionlint` (×2) | success |
| `invoke-cursor-auditor` | success |
| `cursor-scope` (×2) | skipped (branche non `cursor/*` — comportement attendu, `audit-guard.yml:30`) |
| `check-and-automerge` | skipped |
| `Reconcile local Hermes state` | queued au moment de la mesure |

Rejeu local au SHA audité : `314 passed, 16 skipped` (§ 8.F). Le gate
`verdict_audit.py` sur la graine 015 rend `VERDICT: REJECT`, mais les deux
seuls `[FAIL]` sont `verdict_numbers_traceable: verdict.md missing` et
`verdict_is_not_self_authored` — c'est-à-dire « rien n'a encore été
généré ». C'est le résultat **attendu** pour une graine ; ce n'est pas un
défaut de cette PR (§ 8.G).

Conséquence pour la suite de la critique : les portes mécaniques ne laissent
rien à récupérer côté code. Les cinq constats qui suivent portent tous sur
des **affirmations en langue naturelle** que ces portes ne lisent pas.

## 3. Constats

### P1-1 — Six audits approuvés sont bloqués au motif d'un lot « livré » qui n'est ni fusionné ni présent dans l'arbre

La description écrit : « Les six audits pipeline/registre approuvés
aujourd'hui (`16ff5ac`, `4c45718`, `9e35764`, `ab0e7f0`, `827d54e`,
`f978cc7`) restent `AUDIT_APPROVED` **sans conversion** : leur substance
recoupe **le lot 014 livré** ([PR #83]) et les candidats de briefs différés
consignés au HANDOFF ».

Le comptage est exact : le registre au SHA audité montre bien ces six
audits, et exactement eux, dont le dernier évènement du jour est
`AUDIT_APPROVED` (§ 8.H). Ce n'est pas ce que je conteste.

Ce que je conteste est le mot « livré ». Mesures (§ 8.C) :

- `gh pr view 83` → `#83 OPEN merged=non`. La PR n'est pas fusionnée.
- `grep -c '<<TODO' harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md`
  → `6`. Le brief 014 présent dans l'arbre audité n'a ni titre, ni
  *World-Terms Requirement*, ni *Success Conditions*, ni compteurs.
- `find harness/queue/briefs/014-pipeline-contre-audit-porte -type f` → trois
  fichiers : `brief.md`, `eval-rubric.md`, `deliverables/.gitkeep`. Aucun
  livrable, aucun `verdict.md`.

Autrement dit : au SHA audité, la seule chose que le dépôt sait du « lot
014 » est une graine vide. La substance qui justifie le report de six audits
approuvés vit dans une PR ouverte, susceptible d'être refusée ou modifiée.
Si la #83 ne passe pas, six audits restent bloqués sur un motif devenu faux,
et rien dans le dépôt ne permettra de s'en apercevoir : le registre ne porte
aucune ligne pour ce report (voir P2-3 pour l'absence d'état terminal).

**Et cette hypothèse n'est pas théorique.** `master` porte déjà, depuis
`2026-08-13T12:55:00Z` — soit **quatre minutes avant** le commit `c348018` —
un audit de cette même PR #83 :
`architecture/inbox/CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre.md`,
`status: PROPOSED`, qui relève **deux P0** et trois P1, dont « la PR
introduit une injection de commande shell dans un workflow » et « la porte
`audit-check` est verte exactement dans la fenêtre où elle devrait mordre »
(§ 8.C). Aucune ligne du registre ne concerne encore cet audit : il n'est ni
challengé ni tranché.

Le report de six audits est donc justifié par une livraison qu'un audit
indépendant, plus récent que la justification elle-même, tient pour bloquée
par deux P0. Cet audit n'est pas dans l'arbre de la PR #89 (il est postérieur
à sa fusion de `master`), ce qui explique que l'auteur ne l'ait pas vu ; cela
ne rend pas le motif publié moins faux au moment où le propriétaire le lira.

Ce constat relève de la lentille 2 (preuve d'exécution, pas d'affirmation) :
« livré » est une affirmation de succès non mesurée, forme la plus courante
du piège de la correction hallucinée [S2, S5].

**Ce qui suffirait à le lever** : remplacer « le lot 014 livré (PR #83) » par
« le lot 014 **en attente de fusion** (PR #83, ouverte) », et dire ce qui
arrive aux six audits si la #83 est refusée. C'est une édition de texte ;
d'où P1 et non P0.

### P1-2 — La décision de report est annoncée « tracée au HANDOFF » ; `HANDOFF.md` n'en porte aucune trace

Le message de commit `c348018` écrit : « les candidats différés consignés au
HANDOFF — **décision CTO tracée là-bas** ». La description de la PR reprend :
« les candidats de briefs différés consignés au HANDOFF ».

`HANDOFF.md` au SHA audité, huit motifs cherchés, huit fois zéro (§ 8.D) :

| motif | occurrences dans `HANDOFF.md` |
|---|---|
| `16ff5ac` | 0 |
| `4c45718` | 0 |
| `9e35764` | 0 |
| `ab0e7f0` | 0 |
| `827d54e` | 0 |
| `f978cc7` | 0 |
| `015-` | 0 |
| `016-` | 0 |

La section la plus récente de `HANDOFF.md` s'intitule « Session la plus
récente — 2026-08-13 (suite) : critique du brief 012 traitée, brief 013 (le
tick nourrit une fois) » : elle est antérieure à tout le travail de
l'après-midi que cette PR consigne. Le fichier n'a pas été touché par la PR
(21 fichiers modifiés, `HANDOFF.md` n'en fait pas partie).

La décision existe donc uniquement dans un message de commit et dans une
description de PR. Aucun des deux n'est un fichier du dépôt : après fusion,
`git log` gardera le message, mais rien ne répondra à « pourquoi ces six
audits sont-ils encore `AUDIT_APPROVED` ? » sans archéologie de PR. C'est la
définition d'une décision hors registre.

**Non-duplication.** L'audit `CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre`
(approuvé, 19 points retenus) a établi le motif « le succès est affirmé dans
la description, pas dans le dépôt » pour les évènements
`AUDIT_IMPLEMENTED` / `AUDIT_VERIFIED`. Je ne recompte pas ce point.
L'élément neuf est mesuré et différent : l'artefact ici est une **décision
de report**, le lieu de trace est **nommé explicitement** (`HANDOFF.md`), et
ce lieu est **vérifiablement vide**. Un lecteur qui préfère traiter les deux
comme un seul risque systémique est fondé à le faire ; il devra alors
constater que le motif a récidivé **après** son approbation.

### P2-1 — Le tableau de bord livré contredit le registre écrit par le même commit

`hermes/DASHBOARD.md` est le seul artefact que `CLAUDE.md` désigne comme
« the owner's readable status view ». La version livrée au SHA audité
demande encore au propriétaire de convertir les deux audits que le même
commit vient de marquer `AUDIT_CONVERTED`, et annonce un compteur périmé.

Rejeu de `.venv/bin/python hermes/dashboard.py` au SHA audité, puis
`git diff` (§ 8.I) — extraits significatifs :

```
-- **Audits en cours** : 31 — boucles closes : 8.
+- **Audits en cours** : 30 — boucles closes : 9.
...
-- Convertir l'audit retenu `CURSOR-29913c0-pr69-seuil-survie-non-borne` en brief (`/forge-audit-convert`).
-- Convertir l'audit retenu `CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite` en brief (`/forge-audit-convert`).
```

La régénération supprime exactement les deux lignes que la conversion a
rendues fausses et corrige le compteur 31/8 → 30/9. Le fichier livré est
donc en désaccord avec la ligne de registre livrée dans le même commit.

**Ce qui atténue** — et pourquoi ce n'est pas P1 : `.github/workflows/hermes-dashboard.yml`
régénère ce fichier à chaque `push` sur `master` (`paths-ignore:
hermes/DASHBOARD.md`) et toutes les 6 heures. La contradiction se répare
d'elle-même après fusion. Elle reste visible dans l'intervalle exact où le
propriétaire lit ce tableau pour décider s'il fusionne — c'est-à-dire au
seul moment où il s'en sert.

**Précision de mesure, à décharge** : le même `git diff` montre aussi la
disparition du tableau « Activité GitHub récente ». C'est un artefact de mon
rejeu local (le script n'avait pas les données GitHub, il écrit « Non
disponible dans cette génération »), **pas** un défaut de la PR. Je ne
compte que les lignes d'audits et le compteur.

### P2-2 — Les deux graines renvoient le Planificateur vers des numéros de points qui n'existent pas dans l'audit qu'elles citent

Les deux fichiers `brief.md` créés par cette PR ont une seule section de
contenu réel — *Provenance* — et cette section est fausse sur son point
central (§ 8.J).

| graine | points retenus déclarés | constats numérotés dans l'audit cité | lignes de verdict dans la revue Claude |
|---|---|---|---|
| 015 | 1 → 8 (8 points) | **5** (`Constat 1` … `Constat 5`) | 8 |
| 016 | 1 → 15 (15 points) | **8** (`P1-1`, `P1-2`, `P2-1`…`P2-3`, `P3-1`…`P3-3`) | 16 |

La graine 015 dit « Points retenus : 1, 2, 3, 4, 5, 6, 7, 8 » et désigne
comme source `architecture/inbox/CURSOR-0e98199-…md`, qui n'a que cinq
constats : les points 6, 7 et 8 n'existent pas dans le document nommé. La
graine 016 en annonce quinze pour un audit qui en a huit. Dans les deux cas
la numérotation est celle des **lignes de la revue Claude**, pas celle de
l'audit — et c'est l'audit que le fichier cite.

Effet concret : un Planificateur qui suit la *Provenance* de 016 pour écrire
les *Success Conditions* cherchera sept points qu'il ne trouvera pas, et
n'aura aucun moyen de savoir s'il lui manque quelque chose ou si la
référence est fausse.

**Non-duplication.** La cause racine est déjà relevée par
`CURSOR-a7d1c57-pr76-approbation-sans-conversion` § P1-2 (« les points
retenus sont des numéros de lignes de la revue, mais la décision et la
graine de brief citent l'audit, qui numérote autrement »), audit encore
`PROPOSED`. Je ne recompte pas le défaut de l'outil. Ce que j'enregistre
est que **les deux fichiers livrés par cette PR portent la référence
cassée**, avec l'écart chiffré (8 vs 5, 15 vs 8) — c'est pourquoi je le
maintiens en P2 et non en P1 : la correction appartient à
`harness/audit_convert.py` / à la génération de décision, déjà signalés.

### P2-3 — Il n'existe aucun état de registre pour « approuvé, non converti » : le report que cette PR décide est invisible à la machine

Le cycle de vie de `architecture/README.md` propose deux sorties depuis
`AUDIT_APPROVED` : `CONVERTED → IMPLEMENTED → VERIFIED → ARCHIVED`, ou
`REJECTED → ARCHIVED`. « Approuvé et volontairement non converti » n'est pas
un état. Conséquence mesurée au SHA audité (§ 8.H) : **neuf** audits ont
`AUDIT_APPROVED` pour dernier évènement, et trois sont restés à
`AUDIT_CHALLENGED` — douze audits dans un état non terminal, dont six que
cette PR décide explicitement d'y laisser.

Le tableau de bord, lui, continue de les compter comme « retenu — à
convertir en brief » (§ 8.I) : la décision de report et la vue du
propriétaire disent le contraire l'une de l'autre, sans qu'aucun code puisse
les réconcilier.

Un point voisin est déjà relevé et **non recompté** : `CURSOR-a7d1c57` § P1-1
montre qu'aucun chemin automatique n'émet `audit_approved`. Ce que j'ajoute
est l'autre bout du même segment : même émis, `AUDIT_APPROVED` n'a pas de
sortie déclarable autre que la conversion. Les bonnes pratiques 2026 de
handoff entre agents demandent précisément qu'une transition porte ses
chemins de sortie explicites, y compris le refus et le report, dans un
artefact typé plutôt que dans une prose de PR [S8, S7].

### P3-1 — « 66 lignes JSON valides » décrit le commit de fusion, pas la tête livrée (68) ; le reste de l'affirmation tient

La description annonce le résultat de la résolution de conflit : « les deux
blocs conservés en ordre chronologique, `66` lignes JSON valides ».

Mesures (§ 8.B) : la tête livrée `c348018` porte **68** lignes, toutes du
JSON valide. 66 est le compte au commit de fusion `e23e79a`, avant que
`c348018` n'ajoute les deux lignes `AUDIT_CONVERTED` — c'est-à-dire l'objet
même de la PR. Le chiffre publié décrit un état intermédiaire.

Tout le reste de l'affirmation tient, et je l'ai vérifié plutôt que de le
croire : ancêtre 55, base 58, `master` 63, fusion 66 = union exacte ; zéro
ligne perdue ; zéro doublon ; zéro inversion chronologique dans la zone
fusionnée (lignes 56 à 66).

**À décharge, et non porté au débit de cette PR** : le fichier contient une
inversion chronologique aux lignes 29/30 (`12:01:05Z` avant `11:55:18Z`).
Elle est **déjà présente sur `master`** (§ 8.B) : elle est héritée, pas
introduite ici. Je la mentionne seulement parce qu'elle montre que « ordre
chronologique » n'est pas un invariant vérifié par une machine dans ce
dépôt — c'est une propriété qu'on affirme fichier par fichier.

### P3-2 — 97 % du diff présenté est hérité ; l'empilage manuel prive la relecture de la vue par couche que GitHub fournit désormais

Décomposition (§ 8.E) : sur les +3594 lignes que GitHub affiche, `c348018`
en écrit **98** (7 fichiers) et la fusion de `master` en apporte **3496**
(15 fichiers). La base de la PR, `f978cc7`, n'est **pas** un ancêtre de
`master` : c'est la tête de la PR #77, ouverte.

Trois conséquences pratiques :

1. Le seuil de la lentille 5 (~5 fichiers, quelques centaines de lignes) est
   dépassé d'un ordre de grandeur par du contenu que personne n'a écrit ici.
2. Fusionner la #89 avant la #77 fait entrer transitivement le contenu de la
   #77 — dont l'audit `CURSOR-f978cc7`, **approuvé avec 19 points retenus**,
   n'est pas encore traité. La description demande bien de fusionner après
   la #77 ; c'est une consigne humaine, pas une garde.
3. La séparation « un seul rôle écrit dans chaque dossier » que
   `architecture/README.md` dit **prouvable mécaniquement** n'est pas
   vérifiable depuis ce diff, qui mélange `inbox/`, `reviews/`,
   `decisions/` et `briefs/`.

L'empilage lui-même n'est pas le problème — c'est la bonne réponse à un
travail dépendant. Le problème est qu'il est fait à la main (base pointée
sur une branche + `git merge master`), là où les *stacked pull requests*
GitHub, en aperçu public depuis le 2026-07-30, présentent à chaque couche
**son seul diff** et recalculent les checks contre la base de la pile
[S6, S7]. La documentation de la fonctionnalité vise explicitement les gros
diffs produits par des agents.

**Non recompté** : `CURSOR-29913c0` § P2-3 (« le diff dépasse ce qu'une
relecture honnête connecte à l'intention, et la base de la PR n'est pas
celle qui atterrira ») et `CURSOR-f978cc7` § Constat 5 (« 780 lignes dont
777 sont des copies ») ont déjà porté ce motif. Je le maintiens en P3
*information* pour cette raison ; le seul élément neuf est le chaînage sur
une base non fusionnée dont l'audit approuvé n'est pas traité (point 2
ci-dessus).

### P3-3 — La file contient maintenant six briefs sans instruction ; cette PR en ajoute deux

Comptage des marqueurs `<<TODO` dans les seize briefs de la file, au SHA
audité (§ 8.K) : dix briefs sont remplis, **six** ne le sont pas —
`008-contexte-opus5-right-sizing` (1), `008-full-auto-automation-gaps` (4),
`009-full-auto-agent-invocation` (2), `014-pipeline-contre-audit-porte` (6),
`015` (6), `016` (6). Le plus ancien date du 2026-08-08.

`CLAUDE.md` › *Single Source of Instruction* fait de `brief.md` le seul
document qui dit à un agent quoi faire. Six entrées sur seize ne disent
rien.

**Ce qui est correct par conception, et que je ne reproche pas.** Les
marqueurs sont **voulus et testés** : `harness/tests/test_audit_convert.py:94`
affirme `assert "<<TODO (planificateur)" in text  # spec NOT fabricated`.
L'outil de conversion a raison de ne pas inventer la spécification.

**Non recompté** : le maillon « graine → brief exploitable » est déjà
confirmé comme fait technique par ARCH-004 du contre-audit
`CURSOR-5633ee7-automation-completeness`, **archivé** — avec la mention
explicite que « *comment* le traiter est un arbitrage » laissé au
propriétaire. Je ne rouvre pas l'arbitrage.

L'élément neuf est un chiffre : depuis cet archivage (2026-08-12 08:42), la
file est passée de deux à six graines non remplies, dont deux par cette PR,
et `harness/pipeline/orchestrator.py:202-212` confirme que
`handle_brief_seed_created` ne fait **rien** — il retourne un texte
consultatif, sans transition de registre ni file d'attente. Aucun workflow
n'émet d'ailleurs cet évènement (§ 8.K). L'arbitrage différé ne stagne pas :
il s'accumule à raison de deux entrées par tour de boucle.

## 4. Ce qui tient (cadrage adverse — résultats négatifs)

J'ai cherché à faire tomber ces affirmations et je n'y suis pas parvenu.
Elles sont à mettre au crédit de la PR :

1. **La résolution du conflit de registre est exacte et sans perte.**
   Ancêtre 55 / base 58 / `master` 63 / fusion 66 = union parfaite, aucune
   ligne perdue, aucun doublon, JSON valide partout, aucune inversion dans
   la zone fusionnée (§ 8.B). C'est la partie la plus risquée du travail et
   elle est juste. Le propriétaire n'aura effectivement pas à la refaire.
2. **Les quatre chemins de provenance existent.** Les deux graines citent un
   audit et une décision chacune : les quatre fichiers sont présents au SHA
   audité (§ 8.A). Aucun lien mort.
3. **Le périmètre du commit écrit est irréprochable.** `c348018` ne touche
   ni code, ni test, ni workflow, ni brief existant : deux lignes de
   registre et deux graines neuves (§ 8.E).
4. **Le comptage des six audits est exact.** Les six SHA cités sont bien, et
   exactement, ceux dont le dernier évènement du jour est `AUDIT_APPROVED`
   (§ 8.H). L'erreur est sur le motif, pas sur l'inventaire.
5. **Les portes mécaniques sont vertes et le rejeu local le confirme**
   (§ 2, § 8.F).
6. **« Ne pas fusionner en squash » et « se reciblera automatiquement » sont
   exacts** : c'est le comportement GitHub documenté pour une pile dont la
   couche basse est fusionnée puis supprimée [S6].
7. **La graine vide n'est pas un défaut de fabrication** : c'est une
   propriété testée de l'outil (§ P3-3).

## 5. Déjà retenu ailleurs — non recompté

| motif | où il est déjà porté | état |
|---|---|---|
| `actor: "owner"` codé en dur pour une action machine (`harness/audit_convert.py:206`) | `CURSOR-f978cc7` § Constat 3 | APPROVED — le message de commit de cette PR le signale lui-même |
| Aucun chemin automatique n'émet `audit_approved` | `CURSOR-a7d1c57` § P1-1 | PROPOSED |
| `retained_points` = numéros de la revue, pas de l'audit (cause racine) | `CURSOR-a7d1c57` § P1-2 | PROPOSED — seul l'effet sur les deux fichiers livrés ici est enregistré (P2-2) |
| Graine `<<TODO>>` sans câblage vers le Planificateur | ARCH-004, contre-audit `CURSOR-5633ee7`, archivé | ARCHIVED — seul le comptage est neuf (P3-3) |
| Diff hérité plus gros que le diff écrit | `CURSOR-29913c0` § P2-3, `CURSOR-f978cc7` § Constat 5 | PROPOSED / APPROVED — d'où P3 et non P2 |
| Budget de jetons d'un Générateur Cursor non mesuré | `CURSOR-29913c0` § P3-3 | APPROVED |

Sur ce dernier point je n'ajoute qu'une remarque de sourçage, sans nouveau
constat : la pratique 2026 fait du **run** l'unité de facturation à plafonner
(et non le mois ni la clé d'API), avec réservation avant appel et
réconciliation après [S10, S11] ; et l'indicateur utile n'est pas le nombre
de jetons mais le coût par tâche aboutie [S12]. Appliqué à cette boucle, le
denominateur pertinent serait « audits menés jusqu'à un état terminal », que
`hermes/DASHBOARD.md` sait déjà afficher (« boucles closes : 9 »).

## 6. Limite de cet audit (à lire avant de s'en servir)

- Je n'ai pas jugé la **pertinence technique** des deux audits convertis
  (seuil de survie, `HUNGER_DEATH_SCALE`) : leurs contre-audits sont
  fusionnés, ce n'est pas mon objet ici.
- Je n'ai pas relu les 3496 lignes héritées de `master` : elles ont leurs
  propres audits (§ 5) et les recompter serait du bruit.
- `Reconcile local Hermes state` était `queued` au moment de la mesure : je
  classe la CI verte sur les jobs qui concluent, pas sur celui-là.
- Le rejeu de `hermes/dashboard.py` s'est fait sans données GitHub ; je n'ai
  retenu du diff que les lignes indépendantes de cette limite (§ P2-1).

## 7. Briefs atomiques proposés (3 au maximum — propositions, pas instructions)

Ce sont des **propositions**. Un audit n'instruit rien ; seul le
propriétaire, par conversion explicite, peut en faire un brief.

1. **Une porte qui refuse une PR de registre dont les vues contredisent le
   registre qu'elle écrit.** Vérifier mécaniquement, sur une PR touchant
   `architecture/audit-ledger.jsonl`, que `hermes/dashboard.py` régénéré ne
   produit aucun diff sur les lignes d'audits et le compteur de boucles
   (P2-1). Condition de succès rejouable : un cas rouge (PR qui convertit
   sans régénérer) et un cas vert.
2. **Un état terminal déclarable « approuvé, non converti », avec motif
   obligatoire et référence vérifiable.** Fermer la sortie manquante du
   cycle de vie pour que le report de six audits cesse de vivre dans une
   description de PR (P1-2, P2-3). La référence citée en motif devrait être
   un chemin de dépôt existant, ou un numéro de PR **fusionnée** — ce qui
   aurait fait échouer le motif « lot 014 livré » (P1-1).
3. **Une garde de cohérence des `retained_points` à la conversion.** Refuser
   d'écrire une graine dont les points retenus ne correspondent pas à une
   numérotation présente dans le document que la graine cite, ou nommer dans
   la graine le document réellement numéroté (la revue) (P2-2).

## 8. Commandes rejouées (sorties collées)

Arbre de travail séparé, aucune écriture dans le dépôt audité :

```
$ git worktree add /tmp/pr89 pr89
HEAD is now at c348018 boucle d'audit : conversions des deux audits moteur de la PR #69 — graines de briefs 015 et 016
```

### 8.A — Les quatre chemins de provenance cités par les graines existent

```
$ for f in architecture/inbox/CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md \
           architecture/decisions/DECISION-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md \
           architecture/inbox/CURSOR-29913c0-pr69-seuil-survie-non-borne.md \
           architecture/decisions/DECISION-CURSOR-29913c0-pr69-seuil-survie-non-borne.md; do
      [ -f "$f" ] && echo "OK   $f" || echo "MANQUANT  $f"; done
OK   architecture/inbox/CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md
OK   architecture/decisions/DECISION-CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite.md
OK   architecture/inbox/CURSOR-29913c0-pr69-seuil-survie-non-borne.md
OK   architecture/decisions/DECISION-CURSOR-29913c0-pr69-seuil-survie-non-borne.md
```

### 8.B — Le registre : comptage, validité JSON, union de la fusion, chronologie

```
$ wc -l architecture/audit-ledger.jsonl
68 architecture/audit-ledger.jsonl

$ .venv/bin/python  # validité + chronologie sur la tête auditée
lignes non vides: 68
lignes JSON invalides: aucune
inversions chronologiques: 1
   L29 2026-08-12T12:01:05Z > L30 2026-08-12T11:55:18Z
```

Comptages par référence, et l'inversion est-elle héritée ?

```
$ git show e23e79a:architecture/audit-ledger.jsonl | grep -c .   # commit de fusion
66
$ git show origin/master:architecture/audit-ledger.jsonl | grep -c .
63
$ git show f978cc7:architecture/audit-ledger.jsonl | grep -c .   # base de la PR
58
$ git show origin/master:architecture/audit-ledger.jsonl | .venv/bin/python -c ...
inversions master: [((29, '2026-08-12T12:01:05Z'), (30, '2026-08-12T11:55:18Z'))]
```

→ l'inversion L29/L30 est **déjà sur `master`** : héritée, pas introduite ici.

Vérification que la fusion est une union sans perte :

```
$ git merge-base f978cc7 origin/master
da536505c804e3ecc937bab16e3747e09c81968f
$ git show da53650:architecture/audit-ledger.jsonl | grep -c .
55
$ .venv/bin/python  # union, pertes, doublons, ordre de la zone fusionnée
ancêtre=55 base=58 master=63 fusion=66 union_attendue=66
lignes perdues (présentes dans base ou master, absentes de la fusion): []
doublons dans la fusion: []
inversions dans la zone fusionnée (lignes 56..66): aucune
```

### 8.C — Le « lot 014 livré » : PR ouverte, graine vide

```
$ gh pr view 83 --json number,state,mergedAt,title --jq ...
#83 OPEN merged=non — Brief 014 : le contre-audit comme porte observable, le refus fournisseur comme état explicite avec repli (pipeline)
#77 OPEN merged=non — Tenue de registre : clôture de l'audit CURSOR-a4de4bb après fusion du lot 013

$ grep -n '<<TODO' harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md
1:# Brief 014: <<TODO (planificateur): titre>> (issu de l'audit CURSOR-a600532-fusion-sans-contre-audit)
19:<<TODO (planificateur): énoncer le besoin en world-terms, causalement — pas
26:<<TODO (planificateur): conditions de succès numérotées, chacune vérifiable —
31:<<TODO (planificateur): ce que ce brief ne doit explicitement PAS faire.>>
35:<<TODO (planificateur): table des compteurs (name / sample source /
40:<<TODO (planificateur): table (claim / required command / required error), ou

$ find harness/queue/briefs/014-pipeline-contre-audit-porte -type f | sort
harness/queue/briefs/014-pipeline-contre-audit-porte/brief.md
harness/queue/briefs/014-pipeline-contre-audit-porte/deliverables/.gitkeep
harness/queue/briefs/014-pipeline-contre-audit-porte/eval-rubric.md
```

L'audit de la PR #83 déjà présent sur `master`, et son état :

```
$ head -8 architecture/inbox/CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre.md
audit_id:                CURSOR-bd34ded-pr83-porte-verte-quand-elle-devrait-mordre
target_branch:           master
target_commit:           bd34dedbb713863d7f9bfa8f9341975aa01291d6
created_at:              2026-08-13T12:55:00Z
status:                  PROPOSED

$ grep -nE '^## [0-9]+\. P[0-9]-' architecture/inbox/CURSOR-bd34ded-...md | sed 's/ —.*//'
148:## 3. P0-1
216:## 4. P0-2
330:## 5. P1-1
419:## 6. P1-2
503:## 7. P1-3

$ grep -c 'bd34ded' architecture/audit-ledger.jsonl   # (registre de master)
0

$ git -C /tmp/pr89 ls-files --error-unmatch architecture/inbox/CURSOR-bd34ded-...md
Did you forget to 'git add'?      # → absent de l'arbre de la PR #89
```

### 8.D — `HANDOFF.md` ne porte aucune trace de la décision annoncée

```
$ for s in 16ff5ac 4c45718 9e35764 ab0e7f0 827d54e f978cc7 015- 016-; do
      printf '%-10s %s\n' "$s" "$(grep -c -- "$s" HANDOFF.md)"; done
16ff5ac    0
4c45718    0
9e35764    0
ab0e7f0    0
827d54e    0
f978cc7    0
015-       0
016-       0

$ head -3 HANDOFF.md
# HANDOFF.md

## Session la plus récente — 2026-08-13 (suite) : critique du brief 012 traitée, brief 013 (le tick nourrit une fois)
```

`HANDOFF.md` ne figure pas parmi les 21 fichiers modifiés par la PR.

### 8.E — Décomposition du diff : ce qui est écrit vs ce qui est hérité

```
$ git show --stat c348018   # le seul commit écrit par cette PR
 architecture/audit-ledger.jsonl                    |  2 ++
 .../015-.../brief.md                               | 41 ++++++++++++++++++++++
 .../015-.../deliverables/.gitkeep                  |  0
 .../015-.../eval-rubric.md                         |  7 ++++
 .../016-.../brief.md                               | 41 ++++++++++++++++++++++
 .../016-.../deliverables/.gitkeep                  |  0
 .../016-.../eval-rubric.md                         |  7 ++++
 7 files changed, 98 insertions(+)

$ git show --stat e23e79a   # la fusion de master : tout le reste
 15 files changed, 3496 insertions(+), 20 deletions(-)

$ git merge-base --is-ancestor f978cc7 origin/master && echo YES || echo NO
NO
```

### 8.F — Suite de tests du harnais au SHA audité

```
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 16.79s
```

### 8.G — Le gate sur la graine 015 : rejet attendu, rien d'autre

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/015-pr69-seuil-survie-ignore-mortalite
[PASS] files_declared_exist / mtime_after_brief / captures_differ_when_should /
       waivers_have_command_and_error / no_empty_sample_pass /
       no_bare_python_alias / declared_files_are_tracked
[FAIL] verdict_numbers_traceable: verdict.md missing
[FAIL] verdict_is_not_self_authored: Author frontmatter missing on generator-log.md or verdict.md
[N/A]  rubric_predates_deliverables: no deliverables to compare against
VERDICT: REJECT
```

### 8.H — Dernier évènement par audit : douze audits non terminaux

```
$ .venv/bin/python  # dernier évènement par audit_id sur le registre livré
...
AUDIT_CONVERTED      2026-08-13T12:59:23Z  actor=owner        CURSOR-29913c0-pr69-seuil-survie-non-borne
AUDIT_CONVERTED      2026-08-13T12:59:23Z  actor=owner        CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
AUDIT_APPROVED       2026-08-13T11:00:16Z  actor=policy:auto  CURSOR-16ff5ac-contre-audit-perdu-a-la-publication
AUDIT_APPROVED       2026-08-13T11:01:51Z  actor=policy:auto  CURSOR-4c45718-pr65-ledger-recupere-a-la-main
AUDIT_APPROVED       2026-08-13T11:03:12Z  actor=policy:auto  CURSOR-9e35764-pr63-contre-audit-jamais-enregistre
AUDIT_APPROVED       2026-08-13T11:04:50Z  actor=policy:auto  CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion
AUDIT_APPROVED       2026-08-13T12:51:53Z  actor=policy:auto  CURSOR-827d54e-contre-audit-paye-jamais-publie
AUDIT_APPROVED       2026-08-13T12:55:16Z  actor=policy:auto  CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre

répartition des états terminaux : {'AUDIT_ARCHIVED': 9, 'AUDIT_APPROVED': 9, 'AUDIT_CHALLENGED': 3, 'AUDIT_CONVERTED': 3}
```

Les six SHA cités par la description sont bien, et exactement, les six
`AUDIT_APPROVED` du jour. Trois autres (`cdc683f`, `e849633`, `0269d8e`,
approuvés la veille) portent le total à neuf.

### 8.I — Le tableau de bord régénéré contredit le fichier livré

```
$ .venv/bin/python hermes/dashboard.py
OK: /tmp/pr89/hermes/DASHBOARD.md
$ git diff --stat -- hermes/DASHBOARD.md
 hermes/DASHBOARD.md | 35 ++++++++--------------------------- 1 file changed, 8 insertions(+), 27 deletions(-)
$ git diff -- hermes/DASHBOARD.md
-- **Audits en cours** : 31 — boucles closes : 8.
+- **Audits en cours** : 30 — boucles closes : 9.
-- Convertir l'audit retenu `CURSOR-29913c0-pr69-seuil-survie-non-borne` en brief (`/forge-audit-convert`).
-- Convertir l'audit retenu `CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite` en brief (`/forge-audit-convert`).
(+ disparition du tableau « Activité GitHub récente » : artefact du rejeu local, non compté)
```

Régénération automatique après fusion : `.github/workflows/hermes-dashboard.yml`,
`on: push: branches: [master], paths-ignore: hermes/DASHBOARD.md` +
`schedule: cron '17 */6 * * *'`.

### 8.J — Numérotation des points retenus vs constats de l'audit cité

```
$ grep -nE '^#{3,4} *(Constat|P[0-3])' architecture/inbox/CURSOR-0e98199-...md
82:### Constat 1 — P1 — Le seuil qui certifie « le monde vit » n'est pas un modèle de la survie
176:### Constat 2 — P2 — La troncature `int()` remplace le plancher par une immunité
221:### Constat 3 — P3 — Le plafond de mortalité ne protège rien aujourd'hui
239:### Constat 4 — P3 — « Cellule affamée » désigne un garde-manger vide, pas un manque
258:### Constat 5 — P3 — Classification CI du commit audité
$ grep -cE '^\| *[0-9]+ *\|' architecture/reviews/CLAUDE-CURSOR-0e98199-...md
8
$ grep -n 'retained_points' architecture/decisions/DECISION-CURSOR-0e98199-...md
5:retained_points: [1, 2, 3, 4, 5, 6, 7, 8]
```

→ graine 015 : points 1→8 pour un audit à **5** constats (8 = lignes de la revue).

```
$ grep -nE '^#{3,4} *(Constat|P[0-3])' architecture/inbox/CURSOR-29913c0-...md
225:### P1-1 …  300:### P1-2 …  339:### P2-1 …  364:### P2-2 …  385:### P2-3 …
405:### P3-1 …  421:### P3-2 …  431:### P3-3 …
$ grep -cE '^\| *[0-9]+ *\|' architecture/reviews/CLAUDE-CURSOR-29913c0-...md
16
$ grep -n 'retained_points' architecture/decisions/DECISION-CURSOR-29913c0-...md
5:retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
```

→ graine 016 : points 1→15 pour un audit à **8** constats.

### 8.K — Six briefs sans instruction ; `brief_seed_created` n'a aucun émetteur

```
$ for d in harness/queue/briefs/*/; do printf '%-52s TODO=%s\n' "$(basename $d)" "$(grep -c '<<TODO' $d/brief.md)"; done
001…007, 010…013                                     TODO=0
008-contexte-opus5-right-sizing                      TODO=1
008-full-auto-automation-gaps                        TODO=4
009-full-auto-agent-invocation                       TODO=2
014-pipeline-contre-audit-porte                      TODO=6
015-pr69-seuil-survie-ignore-mortalite               TODO=6
016-pr69-seuil-survie-non-borne                      TODO=6

$ for d in 008-contexte-opus5-right-sizing 008-full-auto-automation-gaps 009-... 014-... 015-... 016-...; do
      git log -1 --format=%ad --date=short -- harness/queue/briefs/$d; done
2026-08-08 / 2026-08-10 / 2026-08-12 / 2026-08-13 / 2026-08-13 / 2026-08-13

$ grep -rn 'brief_seed_created' --include='*.yml' .github/workflows/
(aucune sortie)

$ grep -rn 'TODO' harness/pipeline/forge_run_preflight.py
(aucune sortie — le préflight n'examine pas les marqueurs)
```

`harness/pipeline/orchestrator.py:202-212` : `handle_brief_seed_created`
retourne `{"action": "enqueue_planificateur", "reason": …}` et **rien
d'autre** — commentaire du code : « No ledger transition here on purpose ».

## 9. Risques par sévérité

| sévérité | risque | preuve |
|---|---|---|
| **P0** | *aucun* | — |
| **P1** | Six audits approuvés restent bloqués sur un motif (« lot 014 livré ») que le dépôt contredit : PR #83 ouverte, brief 014 = graine vide, et un audit `PROPOSED` de cette même PR relève deux P0 quatre minutes avant la justification | § 8.C |
| **P1** | La décision de report n'est traçable que dans un message de commit et une description de PR ; le `HANDOFF.md` annoncé comme lieu de trace est vide sur les huit motifs cherchés | § 8.D |
| **P2** | La vue que lit le propriétaire (`hermes/DASHBOARD.md`) contredit le registre écrit par le même commit au moment exact où il décide de fusionner (se répare après fusion) | § 8.I |
| **P2** | Les deux graines renvoient le Planificateur vers des points inexistants dans l'audit qu'elles citent (8 vs 5 ; 15 vs 8) | § 8.J |
| **P2** | `AUDIT_APPROVED` n'a pas de sortie autre que la conversion : neuf audits y stagnent, trois à `AUDIT_CHALLENGED` — douze états non terminaux | § 8.H |
| **P3** | Le chiffre publié « 66 lignes » décrit le commit de fusion, pas la tête livrée (68) | § 8.B |
| **P3** | 97 % du diff présenté est hérité d'une base non fusionnée dont l'audit approuvé n'est pas traité ; la revue par couche n'est pas disponible | § 8.E, [S6] |
| **P3** | Six briefs sur seize n'instruisent rien ; le report d'arbitrage archivé (ARCH-004) s'accumule à deux entrées par tour | § 8.K |

## 10. Sources externes

Les sources S1–S5 sont celles du référentiel `architecture/review-guidelines.md`
(consultées le 2026-08-12) et fondent la méthode des six lentilles. Les
suivantes ont été consultées **le 2026-08-13** pour cet audit.

| # | source | date de publication | consulté le |
|---|---|---|---|
| S6 | GitHub Changelog — *Stacked pull requests are now in public preview* — <https://github.blog/changelog/2026-07-30-stacked-pull-requests-are-now-in-public-preview/> | 2026-07-30 | 2026-08-13 |
| S7 | The GitHub Blog — *Turn one giant AI-generated pull request to a reviewable stack* — <https://github.blog/engineering/turn-one-giant-ai-generated-pull-request-to-a-reviewable-stack/> | 2026 (aperçu public) | 2026-08-13 |
| S8 | AEEF Standards — *AI Agent SDLC Orchestration* (artefact de passation obligatoire, PRD-STD-009 REQ-009-06) — <https://aeef.ai/transformation/agent-sdlc-orchestration/> | 2026 | 2026-08-13 |
| S9 | MLflow — *Building Production-Ready AI Agents in 2026* (sondes d'évaluation, journaux d'audit lisibles par machine) — <https://mlflow.org/articles/building-production-ready-ai-agents-in-2026/> | 2026 | 2026-08-13 |
| S10 | waxell.ai — *AI Agent Token Budget Enforcement [2026]* (plafond par session, arrêt avant l'appel suivant) — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026 | 2026-08-13 |
| S11 | RFC *Agent Budget Protocol* — « l'unité de dommage pour un agent est le **run** » — <https://github.com/iamapsrajput/agent-budget-protocol/blob/main/RFC.md> | 2026 | 2026-08-13 |
| S12 | Cockroach Labs — *Managing Agentic AI Costs at Scale* (Gartner, mars 2026 : 5 à 30× plus de jetons par tâche ; mesurer la tâche aboutie, pas la consommation) — <https://www.cockroachlabs.com/blog/agentic-ai-costs-at-scale/> | 2026 | 2026-08-13 |

Usage fait de ces sources : S6–S7 étayent P3-2 (revue par couche d'une pile,
et le fait que les gros diffs d'agents sont le problème visé) ; S8–S9
étayent P2-3 (une transition de passation porte ses sorties explicites dans
un artefact typé, pas dans une prose) ; S10–S12 n'étayent aucun constat
neuf — elles servent uniquement la remarque de sourçage du § 5 sur un point
déjà retenu ailleurs.

## 11. Ce que cet audit ne fait pas

Il ne décide rien, n'autorise rien, n'ordonne rien. Les trois flags
`*_authorized` du frontmatter sont `false`. Les trois briefs du § 7 sont des
**propositions** : seul le propriétaire peut les convertir, et le brief
issu de la conversion redevient alors la source unique d'instruction
(`CLAUDE.md` › *Single Source of Instruction*).
