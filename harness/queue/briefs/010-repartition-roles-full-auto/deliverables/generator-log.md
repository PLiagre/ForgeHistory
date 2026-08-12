**Author**: forge-generateur

# Journal du Générateur — Brief 010, Lot 010a uniquement

Périmètre respecté : uniquement le lot 010a (SC1, SC2, SC3, SC3b, SC4, SC5,
SC6). Aucun fichier des lots 010b/010c, aucun fichier du brief 009, aucun
workflow sous `.github/workflows/`, ni `VISION.md`, n'a été touché — vérifié
en fin de session (voir « Périmètre réellement touché » plus bas).

## Ce que j'ai construit

Deux angles morts fermés ensemble dans
`harness/verdict_audit.py::check_verdict_not_self_authored` (le contrôle
`verdict_is_not_self_authored`) :

1. **Acteur, pas rôle** (SC3/SC4). L'ancien code comparait deux chaînes de
   rôle (`gen != ver`) : `forge-generateur-codex` != `forge-evaluateur-codex`
   en tant que chaînes, alors que c'est le même acteur (Codex) des deux
   côtés. Le correctif dérive l'acteur du suffixe de backend porté par
   chaque rôle (`_actor_suffix`), génériquement — aucune liste de backends
   connus n'est codée en dur, donc un acteur jamais vu ailleurs dans le
   dépôt (`gemini`, SC4) est traité exactement pareil que `codex` ou
   `cursor`.

2. **Tous les couples, pas seulement le premier** (SC3b). `read_field`
   s'appuie sur `re.search`, qui ne rend jamais que la première occurrence
   d'un champ `**Author**:`. Sur un brief multi-lots (chaque lot ajoute sa
   propre section signée dans `generator-log.md` et dans `verdict.md`),
   tout couple auteur au-delà du premier était invisible — auto-jugé ou non,
   il n'était tout simplement jamais regardé. Ajout de `read_all_fields`
   (`re.finditer`), qui rend toutes les occurrences, dans l'ordre du
   document.

**Règle d'appariement retenue**, une fois tous les auteurs collectés :
`generator-log.md` et `verdict.md` sont alimentés indépendamment et n'ont
pas nécessairement le même nombre d'entrées (`verdict.md` peut porter
plusieurs passes d'évaluation sur le même lot — c'est exactement le cas du
brief 009 réel : un REJECT initial du lot 009a, puis une réévaluation du
*même* lot 009a). Les `k` dernières entrées de chaque fichier sont
appariées position à position, `k = min(len(auteurs générateur),
len(auteurs évaluateur))` — les passes d'évaluation les plus anciennes,
supplantées par une passe plus récente, sortent par l'avant plutôt que
d'être appariées de force au mauvais lot. Vérifié sur le brief 009 réel
(SC6, détail plus bas) : cette règle apparie le générateur du lot 009a
(`forge-generateur`) avec sa propre réévaluation ultérieure
(`forge-evaluateur-codex`, acteur différent, jugement croisé légitime) et le
générateur du lot 009b (`forge-generateur-codex`) avec sa propre évaluation
(`forge-evaluateur`, également différent) — exactement les deux lots
réellement présents, aucun faux auto-jugement, aucun lot laissé sans examen.

J'ai considéré une piste plus simple — comparer les *ensembles* d'acteurs
des deux fichiers entiers — et je l'ai rejetée : sur le brief 009 réel,
l'acteur `codex` apparaît à la fois côté générateur (lot 009b) et côté
évaluateur (réévaluation du lot 009a), sans que ce soit un auto-jugement (ce
n'est pas le même lot). Un simple test d'intersection d'ensembles aurait
donc refusé à tort le brief 009 (violant SC5/SC6). J'ai aussi écarté « lire
la dernière occurrence au lieu de la première » — la rubrique nomme
explicitement ce raccourci comme un déplacement de l'angle mort, pas une
fermeture : il laisserait de nouveau invisible tout couple sauf le dernier.

`docs/adr/0008-codex-as-evaluateur-under-credit-cap.md` (nouveau, `Status:
accepted`) enregistre les quatre points exigés par SC1 : (a) Codex peut
tenir le rôle d'Évaluateur ; (b) uniquement dans une session distincte
déclenchée par un tiers (CI ou le propriétaire), jamais par la session
Générateur elle-même ; (c) l'option « sous-agent d'évaluation engendré par
le Générateur » est explicitement écartée, avec sa raison (le producteur
cadre son propre juge : instructions, preuves montrées, consolidation de la
réponse) ; (d) le fait déclencheur est le plafond de crédit de Claude, pas
la commodité. `docs/adr/README.md` a gagné sa ligne.

`docs/rules/harness-roles.md` a été modifié pour porter cette même décision
(SC2) : la ligne « Évaluateur » du tableau renvoie désormais à l'exception,
et une nouvelle section « Évaluateur : Claude par défaut, Codex sous une
exception nommée » énonce les trois conditions (b, c, d ci-dessus) en
pointant vers l'ADR-0008, sans dupliquer son texte. Aucun autre fichier ne
paraphrase le brief ou l'ADR : `py -m pytest
harness/tests/test_single_source_of_instruction.py -q` reste vert (inclus
dans la suite complète ci-dessous).

## Preuve red-first (SC3, SC3b) — obligatoire, dans l'ordre exigé

Méthode : le code AVANT correctif a été extrait via
`git show HEAD:harness/verdict_audit.py` vers une copie jetable hors du
dépôt (`.../scratchpad/verdict_audit_PREFIX.py`, avec sa dépendance
`bare_python.py` copiée à côté), puis exécuté contre des fixtures elles
aussi jetables, construites hors du dépôt. Aucune sabotage n'a eu lieu dans
l'arbre de travail du dépôt à aucun moment — le défaut testé est déjà réel
dans le code non modifié du dépôt, il n'y avait rien à simuler.

### SC3 — même acteur, rôles différents (`forge-generateur-codex` /
`forge-evaluateur-codex`)

Sortie rouge réelle, contre le code du dépôt tel qu'il était avant ce lot
(copie recopiée intégralement, fichier joint :
`deliverables/proofs/proof-sc3-red.txt`) :

```
# verdict_audit report for .../scratchpad/fx_sc3
[PASS] verdict_is_not_self_authored: generator=forge-generateur-codex, evaluator=forge-evaluateur-codex
...
VERDICT: ACCEPT
exit=0
```

Sortie verte réelle après correctif
(`deliverables/proofs/proof-sc3-green.txt`) :

```
# verdict_audit report for .../scratchpad/fx_sc3
[FAIL] verdict_is_not_self_authored: same actor on 1/1 examined pair(s): forge-generateur-codex==forge-evaluateur-codex (examined: forge-generateur-codex<->forge-evaluateur-codex)
...
VERDICT: REJECT
exit=1
```

### SC3b — couple auto-jugé en seconde position (brief à deux lots,
`forge-generateur` puis `forge-generateur-codex` dans le journal,
`forge-evaluateur` puis `forge-evaluateur-codex` dans le verdict)

Sortie rouge réelle (`deliverables/proofs/proof-sc3b-red.txt`) — seul le
premier couple (Claude/Claude, honnête) est visible, le second (Codex
générateur, Codex évaluateur du même lot) passe inaperçu et le verdict
global reste ACCEPT :

```
# verdict_audit report for .../scratchpad/fx_sc3b
[PASS] verdict_is_not_self_authored: generator=forge-generateur, evaluator=forge-evaluateur
...
VERDICT: ACCEPT
exit=0
```

Sortie verte réelle après correctif
(`deliverables/proofs/proof-sc3b-green.txt`) — les deux couples sont
maintenant examinés, et le second (auto-jugé) est refusé :

```
# verdict_audit report for .../scratchpad/fx_sc3b
[FAIL] verdict_is_not_self_authored: same actor on 1/2 examined pair(s): forge-generateur-codex==forge-evaluateur-codex (examined: forge-generateur<->forge-evaluateur; forge-generateur-codex<->forge-evaluateur-codex)
...
VERDICT: REJECT
exit=1
```

Les fixtures elles-mêmes sont jointes (`deliverables/proofs/fixtures/`) pour
reproductibilité — ce sont des brief-dirs jetables construits pour la
preuve, pas des briefs réels du dépôt.

## SC4 — généralisation sans modifier le contrôle (acteur `gemini`, absent du
dépôt)

Vérifié que `gemini` n'apparaît nulle part dans `harness/verdict_audit.py`
avant l'exécution (test `test_unseen_actor_name_is_refused_without_naming_it_in_the_control`,
qui lit le code source du contrôle lui-même et échoue si le nom y figure
déjà). Une seule exécution après correctif, sans aucune modification
supplémentaire du contrôle entre le correctif SC3/SC3b et ce cas
(`deliverables/proofs/proof-sc4-green.txt`) :

```
[FAIL] verdict_is_not_self_authored: same actor on 1/1 examined pair(s): forge-generateur-gemini==forge-evaluateur-gemini (examined: forge-generateur-gemini<->forge-evaluateur-gemini)
VERDICT: REJECT
```

(La sortie rouge correspondante, obtenue avec la même méthode que SC3/SC3b,
est conservée à titre de complément dans
`deliverables/proofs/proof-sc4-red.txt`, bien que SC4 n'exige qu'une seule
exécution post-correctif.)

## SC5 — aucune invalidation rétroactive, sur tous les répertoires de brief

Gate exécuté sur les 11 répertoires de brief réels du dépôt (tous les
sous-répertoires de `harness/queue/briefs/` hors `.gitkeep`), une fois avant
le correctif (`deliverables/proofs/sc5-gate-before-all-briefs.txt`,
généré avec le code du dépôt tel qu'il était avant ce lot, extrait via `git
show HEAD:harness/verdict_audit.py`) et une fois après
(`deliverables/proofs/sc5-gate-after-all-briefs.txt`, avec le code corrigé).

Comparaison automatisée, ligne par ligne, du statut de
`verdict_is_not_self_authored` pour chacun des 11 répertoires
(`deliverables/proofs/sc5_regression_check.py`, sortie réelle dans
`deliverables/proofs/sc5-regression-check-output.txt`) :

```
briefs compared: 11
PASS->FAIL regressions: 0 []
  001-spatial-primary-key-adr: PASS -> PASS
  002-geo-pipeline-coastline-1400: PASS -> PASS
  003-port-unity-game: PASS -> PASS
  004-polish-visuel: PASS -> PASS
  005-refonte-visuelle-carte: PASS -> PASS
  006-full-auto-agent-pipeline: PASS -> PASS
  007-geo-pipeline-cells-adjacency: PASS -> PASS
  008-contexte-opus5-right-sizing: FAIL -> FAIL
  008-full-auto-automation-gaps: PASS -> PASS
  009-full-auto-agent-invocation: PASS -> PASS
  010-repartition-roles-full-auto: FAIL -> FAIL
```

11 répertoires comparés, zéro régression PASS→FAIL. Les deux répertoires
déjà en FAIL avant correctif (`008-contexte-opus5-right-sizing` et
`010-repartition-roles-full-auto` lui-même, tous deux faute de
`verdict.md`/`generator-log.md` complets à ce stade) restent en FAIL pour la
même raison (« Author frontmatter missing »), pas une nouvelle.

## SC6 — le jugement croisé légitime du brief 009 continue de passer

Exécution réelle du gate corrigé sur
`harness/queue/briefs/009-full-auto-agent-invocation`
(`deliverables/proofs/sc6-brief009-after.txt`) :

```
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur-codex; forge-generateur-codex<->forge-evaluateur
...
VERDICT: ACCEPT
```

Les deux couples réellement présents dans le brief 009 sont désormais
examinés (contre un seul avant correctif, cf. la ligne « PASS » identique
mais à l'évidence plus pauvre dans `sc5-gate-before-all-briefs.txt`) : le
lot 009a (généré par `forge-generateur`) est apparié à sa réévaluation par
`forge-evaluateur-codex` — c'est précisément le couple que SC6 nomme — et le
lot 009b (généré par `forge-generateur-codex`) à son évaluation par
`forge-evaluateur`. Aucun des deux n'est un auto-jugement ; le contrôle ne
les refuse donc pas.

## Tests ajoutés

`harness/tests/test_verdict_audit_actor_identity.py` (nouveau fichier, 6
fonctions de test, exécutées contre le vrai binaire `py
harness/verdict_audit.py <fixture>` en sous-processus — pas d'import
interne simulé) :

```
harness/tests/test_verdict_audit_actor_identity.py::test_same_actor_different_role_string_is_refused PASSED
harness/tests/test_verdict_audit_actor_identity.py::test_self_judged_pair_in_second_lot_is_no_longer_invisible PASSED
harness/tests/test_verdict_audit_actor_identity.py::test_two_honest_lots_both_examined_and_both_pass PASSED
harness/tests/test_verdict_audit_actor_identity.py::test_unseen_actor_name_is_refused_without_naming_it_in_the_control PASSED
harness/tests/test_verdict_audit_actor_identity.py::test_cross_actor_judgment_still_passes PASSED
harness/tests/test_verdict_audit_actor_identity.py::test_read_all_fields_returns_every_occurrence_in_order PASSED
```

Trois d'entre elles prouvent le refus d'un couple `<role>-<acteur>`
identique (`self_authored_multibackend_refused_test_count` = 3/3) :
`test_same_actor_different_role_string_is_refused` (SC3),
`test_self_judged_pair_in_second_lot_is_no_longer_invisible` (SC3b),
`test_unseen_actor_name_is_refused_without_naming_it_in_the_control` (SC4).
Les trois autres sont des contrôles positifs (le contrôle ne doit pas
devenir un refus-tout) : deux lots honnêtes tous deux examinés et tous deux
acceptés, jugement croisé façon brief 009 accepté, et un test unitaire
direct sur `read_all_fields`.

## Suite complète du dépôt

```
$ py -m pytest harness/tests/ -q
........................................................................ [ 23%]
........................................................................ [ 47%]
........................................................................ [ 71%]
........................................................................ [ 95%]
..............                                                           [100%]
302 passed in 23.98s
```

(296 tests au départ de ce lot + 6 nouveaux dans
`test_verdict_audit_actor_identity.py` = 302. Aucun test existant modifié,
aucune assertion affaiblie.)

## Gate mécanique sur ce lot lui-même

Sortie réelle, intégrale, non retouchée :

```
$ py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto
# verdict_audit report for harness\queue\briefs\010-repartition-roles-full-auto
# generated_at: 2026-08-11T10:42:23.275072
[PASS] files_declared_exist: all declared files present
[FAIL] mtime_after_brief: predate brief.md: [...19 chemins déclarés...]
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[FAIL] verdict_numbers_traceable: verdict.md missing
[FAIL] no_bare_python_alias: bare `python` found in: ['brief.md']
[FAIL] verdict_is_not_self_authored: Author frontmatter missing on generator-log.md or verdict.md
[FAIL] rubric_predates_deliverables: rubric (2026-08-11 11:15:00) written after earliest deliverable (2026-08-11 10:29:10.036211)
[PASS] declared_files_are_tracked: all 14 in-brief declared files are tracked; 5 declared outside the brief dir, not checked: [...]

VERDICT: REJECT
```

Sortie intégrale, sans troncature, conservée dans
`deliverables/proofs/gate-010a-self-check-final.txt`. Ce `REJECT` n'est **pas
recevable comme un ACCEPT de ma part** — je ne prononce jamais ma propre
recevabilité — mais je dois expliquer honnêtement pourquoi il ne peut pas en
être autrement à ce stade, plutôt que de le laisser sans commentaire :

- `verdict_numbers_traceable` et `verdict_is_not_self_authored` échouent
  parce que **`verdict.md` n'existe pas encore**. L'Execution Contract de ce
  brief est explicite : ce lot modifie `harness/verdict_audit.py`, il est
  donc produit par Claude et son verdict est écrit par Codex, **en session
  distincte** — pas par moi. Aucun Générateur ne peut faire passer ce
  contrôle à PASS sans écrire lui-même le verdict, ce qui serait
  précisément l'auto-jugement que ce lot corrige. J'ai vérifié que ce même
  répertoire, avant tout mon travail, échouait déjà pour la même raison
  (aucun `verdict.md`) — voir `deliverables/proofs/sc5-gate-before-all-briefs.txt`,
  entrée `010-repartition-roles-full-auto`.
- `no_bare_python_alias` échoue sur `brief.md` — un fichier que je n'ai pas
  écrit et n'ai pas le droit de modifier (il porte l'instruction unique du
  Planificateur). En le rejouant avec le code du dépôt strictement *avant*
  mon correctif (`git show HEAD:harness/verdict_audit.py`, copie jetable),
  le même échec apparaît déjà, mot pour mot, sur ce même fichier — donc
  préexistant, indépendant de mon changement. La cause : le scanner du
  contrôle lit tous les fichiers `.md` du répertoire de brief, brief.md
  inclus, et brief.md contient un bloc de code balisé pour le langage
  concerné (trois apostrophes inverses suivies du nom du langage, sans
  espace) ; cette clôture touche l'alternative « commande entre apostrophes
  inverses » du motif positionnel du contrôle. Hors périmètre du lot 010a
  (aucune condition de succès ne porte sur ce contrôle) et hors de ma
  compétence (je ne modifie pas brief.md).
- `mtime_after_brief` et `rubric_predates_deliverables` échouent pour un
  **artefact de fuseau horaire, mesuré, pas une vraie péremption**. Le champ
  `**Authored**` de `brief.md` porte `2026-08-11T09:10:00Z` (UTC) ; celui de
  `eval-rubric.md` porte `2026-08-11T09:15:00Z`. `read_ts` les normalise en
  heure locale (`astimezone()`), et cette machine est en UTC+2 : la borne
  devient `11:10:00`/`11:15:00` heure locale. Mes fichiers ont été créés
  entre `10:2x` et `10:4x` heure locale — avant cette borne, donc « avant le
  brief » aux yeux du contrôle — alors qu'en UTC ils datent de `08:2x`
  à `08:4x`, eux aussi avant `09:10`/`09:15` UTC : l'horodatage `Authored`
  du brief est simplement dans le futur par rapport à l'horloge système
  réelle au moment où j'écris ce journal (`py -c "import datetime;
  print(datetime.datetime.utcnow())"` → `2026-08-11 08:40:23` au moment de
  la mesure). Je n'ai ni le droit ni la possibilité de changer
  `**Authored**` sur `brief.md`/`eval-rubric.md` (instruction du
  Planificateur), ni de fabriquer un horodatage de fichier pour contourner
  ceci (interdit explicitement). Cet écart se résorbera de lui-même, sans
  aucune action, dès que l'horloge réelle dépassera `09:15:00Z` — ce qui
  sera trivialement le cas à la prochaine session (Évaluateur Codex).

## Périmètre réellement touché

```
$ git status --short docs/adr/ docs/rules/ harness/verdict_audit.py harness/tests/ harness/queue/briefs/010-repartition-roles-full-auto/
A  docs/adr/0008-codex-as-evaluateur-under-credit-cap.md
M  docs/adr/README.md
M  docs/rules/harness-roles.md
M  harness/verdict_audit.py
A  harness/tests/test_verdict_audit_actor_identity.py
A  harness/queue/briefs/010-repartition-roles-full-auto/deliverables/...
```

Aucun fichier sous `.github/workflows/`, aucun fichier de
`harness/queue/briefs/009-full-auto-agent-invocation/` (brief, lots,
verdicts, feedbacks), `VISION.md` non touché.

## Ce que je n'ai pas fait

- Je n'ai pas écrit `verdict.md` : ce n'est pas mon rôle, et l'Execution
  Contract du brief l'interdit explicitement pour ce lot.
- Je n'ai pas commité : `git add` seulement (convention établie au lot 009b,
  justifiée dans son propre journal), pour que `declared_files_are_tracked`
  puisse être vérifié sans qu'un commit soit nécessaire de ma part.
- Je n'ai pas touché aux lots 010b/010c (pas de `run_codex_generator.sh`, pas
  d'ADR 0009, pas de test sur `merge-bot.yml`).

---

## Itération 2 — correctif D1/D2 (le contrôle était devenu plus permissif)

**Author**: forge-generateur

**Date**: 2026-08-11T14:05:00

### Ce que le feedback a trouvé

`feedback/feedback-010a.md` a rejeté l'itération 1 sur un seul défaut
bloquant, D1 : la règle d'appariement « fenêtre des k derniers auteurs »
introduite pour SC3/SC3b avait rendu `verdict_is_not_self_authored`
**plus permissif** que le code d'avant sur une classe de cas — exactement
ce que le non-objectif 7 du brief et la grille interdisent en toutes
lettres. D2 en découlait : SC3b annonce examiner « chaque couple », mais un
couple auto-jugé placé hors de la fenêtre `k` restait invisible.

Le feedback donnait les deux cas D1 à reproduire tels quels, la cause
précise (troncature `gen_authors[:-k]` jetée sans être comparée), et le
correctif attendu en deux ajouts, **sans toucher à la règle d'appariement
existante** (confirmée juste par le feedback lui-même).

### Preuve rouge, d'abord — les deux cas D1

Copie jetable, hors dépôt, du code tel qu'il était **avant cette
itération** (`harness/verdict_audit.py` au commit `b054b66`, capturé dans
`C:\Users\...\scratchpad\prefix-copy\verdict_audit.py`, committée dans le
dépôt sous `deliverables/proofs/pre-fix/verdict_audit.py.iter1-pre-d1fix.orig`
pour que la preuve reste vérifiable après coup) :

**D1, cas 1** — journal : Lot 1 par `forge-generateur`, Lot 2 (pas encore
jugé) par `forge-generateur-korrigan` ; verdict : `forge-generateur` (le
producteur du Lot 1 signe son propre verdict de son propre nom de rôle).

```
$ py <copie-pre-fix>/verdict_audit.py <fixture d1-case1>
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur-korrigan<->forge-generateur
VERDICT: ACCEPT
exit=0
```

**D1, cas 2** (symétrique — l'acteur suffixé en tête) — journal : Lot 1 par
`forge-generateur-korrigan`, Lot 2 (pas encore jugé) par `forge-generateur` ;
verdict : `forge-generateur-korrigan`.

```
$ py <copie-pre-fix>/verdict_audit.py <fixture d1-case2>
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-generateur-korrigan
VERDICT: ACCEPT
exit=0
```

Les deux cas répondent `ACCEPT` avec le code d'avant, exactement comme
décrit dans le feedback. Sorties intégrales committées :
`deliverables/proofs/proof-d1-case1-red.txt`,
`deliverables/proofs/proof-d1-case2-red.txt`. Fixtures committées sous
`deliverables/proofs/fixtures/fx_d1_case1/` et `fx_d1_case2/`.

Reproduit aussi le cas D2 (couple auto-jugé hors fenêtre `k`, rôles
différents) : journal `forge-generateur-korrigan` (Lot 1) puis
`forge-generateur` (Lot 2) ; verdict `forge-evaluateur-korrigan` (Korrigan
juge son propre Lot 1) :

```
$ py <copie-pre-fix>/verdict_audit.py <fixture d2>
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur-korrigan
VERDICT: ACCEPT
exit=0
```

Sortie committée : `deliverables/proofs/proof-d2-red.txt` ; fixture sous
`deliverables/proofs/fixtures/fx_d2/`.

### Le correctif — deux ajouts dans `check_verdict_not_self_authored`, rien d'autre

Sans toucher à la règle des `k` derniers auteurs par position (confirmée
correcte par le feedback pour SC3/SC3b/SC6) :

1. **Invariant d'ensemble, toute position.** Si une même chaîne d'auteur
   figure à la fois dans `generator-log.md` et dans `verdict.md`
   (`set(gen_authors) & set(ver_authors)`), refus immédiat — c'est
   l'invariant que l'ancien contrôle assurait déjà (comparaison des deux
   premières entrées), rétabli sur les listes entières plutôt que sur la
   seule fenêtre `k`. Il ne peut produire aucun faux refus : la même
   signature des deux côtés est par construction la même personne.

2. **Confrontation des entrées écartées par la troncature.** Le côté le
   plus long a des entrées hors fenêtre (`gen_authors[:-k]` ou
   `ver_authors[:-k]`, selon lequel des deux fichiers est le plus long).
   Chacune de ces entrées écartées est confrontée à **tous** les auteurs de
   l'autre fichier (pas seulement son vis-à-vis positionnel) via
   `_same_actor` — inchangée, ré-employée telle quelle. Ferme D2 : un couple
   auto-jugé dont les deux rôles diffèrent (`forge-generateur-korrigan` /
   `forge-evaluateur-korrigan`) mais qui partage le même acteur est trouvé
   même hors fenêtre.

### Preuve verte — les mêmes trois fixtures, code corrigé du dépôt

```
$ py harness/verdict_audit.py <fixture d1-case1>
[FAIL] verdict_is_not_self_authored: identical author string appears in both generator-log.md and verdict.md: forge-generateur
VERDICT: REJECT
exit=1

$ py harness/verdict_audit.py <fixture d1-case2>
[FAIL] verdict_is_not_self_authored: identical author string appears in both generator-log.md and verdict.md: forge-generateur-korrigan
VERDICT: REJECT
exit=1

$ py harness/verdict_audit.py <fixture d2>
[FAIL] verdict_is_not_self_authored: same actor on 1 dropped-entry pair(s) outside the k-window: forge-generateur-korrigan==forge-evaluateur-korrigan (positionally examined: forge-generateur<->forge-evaluateur-korrigan)
VERDICT: REJECT
exit=1
```

Sorties intégrales committées : `deliverables/proofs/proof-d1-case1-green.txt`,
`deliverables/proofs/proof-d1-case2-green.txt`, `deliverables/proofs/proof-d2-green.txt`.

### Le brief 009 reste ACCEPT

Vérifié par exécution réelle du gate (pas seulement relu, comme le feedback
l'avait fait par avance) :

```
$ py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[PASS] verdict_numbers_traceable: all cited numbers trace to manifest.json
[PASS] no_bare_python_alias: no bare `python` invocations found
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur-codex; forge-generateur-codex<->forge-evaluateur
[PASS] rubric_predates_deliverables: rubric (2026-08-10 11:00:00) predates earliest deliverable (2026-08-10 22:52:36.524133)
[PASS] declared_files_are_tracked: all 14 in-brief declared files are tracked; 10 declared outside the brief dir, not checked: [...]

VERDICT: ACCEPT
```

Sortie intégrale committée : `deliverables/proofs/sc6-brief009-after-d1fix.txt`.
Les deux ajouts sont inertes sur ce brief exactement pour les raisons que le
feedback avait anticipées : l'intersection brute des chaînes d'auteur y est
vide (journal = `forge-generateur`, `forge-generateur-codex` ; verdict =
`forge-evaluateur`, `forge-evaluateur-codex`, `forge-evaluateur`, aucun
recouvrement), et la seule entrée écartée par la troncature (le premier
`forge-evaluateur` du verdict — le REJECT initial du Lot 009a) ne partage
son acteur avec aucun auteur du journal.

### SC5 rejoué sur les 11 répertoires, dans les deux sens

`sc5_bidirectional_regression_check.py` (nouveau, complète
`sc5_regression_check.py` de l'itération 1 qui ne regardait que le sens
PASS→FAIL — exactement le sens que D1 n'aurait pas révélé, puisque D1 est
un FAIL→PASS) :

```
$ py deliverables/proofs/sc5_bidirectional_regression_check.py deliverables/proofs/sc5-gate-before-d1fix-all-briefs.txt deliverables/proofs/sc5-gate-after-d1fix-all-briefs.txt
briefs compared: 11
PASS->FAIL regressions (non-goal 7): 0 []
FAIL->PASS regressions (D1 direction): 0 []
  001-spatial-primary-key-adr: PASS -> PASS
  002-geo-pipeline-coastline-1400: PASS -> PASS
  003-port-unity-game: PASS -> PASS
  004-polish-visuel: PASS -> PASS
  005-refonte-visuelle-carte: PASS -> PASS
  006-full-auto-agent-pipeline: PASS -> PASS
  007-geo-pipeline-cells-adjacency: PASS -> PASS
  008-contexte-opus5-right-sizing: FAIL -> FAIL
  008-full-auto-automation-gaps: PASS -> PASS
  009-full-auto-agent-invocation: PASS -> PASS
  010-repartition-roles-full-auto: PASS -> PASS
```

11 répertoires comparés (tous ceux que compte
`harness/queue/briefs/*/` à ce jour), aucun PASS→FAIL, aucun FAIL→PASS.
`008-contexte-opus5-right-sizing` reste `FAIL` des deux côtés (pour une
autre raison — Author manquant — sans rapport avec ce correctif). Sortie
intégrale committée :
`deliverables/proofs/sc5-bidirectional-regression-check-output.txt` ;
rapports complets avant/après :
`deliverables/proofs/sc5-gate-before-d1fix-all-briefs.txt` et
`deliverables/proofs/sc5-gate-after-d1fix-all-briefs.txt`.

### Les deux cas deviennent des tests committés

Ajoutés à `harness/tests/test_verdict_audit_actor_identity.py` (fichier
existant de l'itération 1, rien retiré) :
`test_self_signed_verdict_masked_by_unjudged_later_lot_is_refused` (D1 cas
1), `test_self_signed_verdict_masked_by_unjudged_later_lot_is_refused_symmetric`
(D1 cas 2), `test_self_judged_pair_dropped_by_k_window_is_refused` (D2).

### D3 (mineur) — l'acteur du test SC4 apparaissait déjà ailleurs dans le dépôt

`test_unseen_actor_name_is_refused_without_naming_it_in_the_control`
utilisait `gemini`, qui figure dans
`docs/adr/0002-pluggable-generator-backend.md` — SC4 demande un nom absent
de **tout le dépôt**, pas seulement du contrôle. Corrigé : acteur renommé
`ptarmigana` (inventé), et l'assertion d'absence porte maintenant sur
`git grep -il ptarmigana` exécuté sur tout le dépôt depuis le test
lui-même, pas seulement sur le texte de `verdict_audit.py` :

```
$ git grep -il ptarmigana
(aucune sortie avant l'écriture du test — confirmé séparément par
 `git grep -il "ptarmigana" -- .` avant l'édition, exit=0 sans sortie)
```

Le test exclut de son propre scan uniquement les deux fichiers qui doivent
légitimement nommer le mot (lui-même, et cette section du journal) ; toute
autre occurrence ferait échouer le test.

### D4 (mineur) — le compteur `author_pairs_examined_per_brief` rendu honnête

Le dénominateur exigé par le brief est « le nombre réel de couples présents
dans ces deux fichiers », pas seulement ceux appariés par la fenêtre `k`.
Mesuré à la source sur le brief 009 réel :

```
$ py -c "import sys; sys.path.insert(0,'harness'); import verdict_audit as va; from pathlib import Path; bd=Path('harness/queue/briefs/009-full-auto-agent-invocation'); gen=va.read_all_fields(bd/'deliverables'/'generator-log.md','Author'); ver=va.read_all_fields(bd/'verdict.md','Author'); k=min(len(gen),len(ver)); print(len((gen[:-k] if k<len(gen) else []))+len((ver[:-k] if k<len(ver) else [])))"
1
```

Un nouveau compteur `author_pairs_unpaired_signatures_count` = 1 (la
signature `forge-evaluateur` du REJECT initial du Lot 009a, écartée par la
fenêtre positionnelle mais désormais confrontée par le point 2 du correctif
D1 ci-dessus) rend explicite ce que `author_pairs_examined_per_brief` = 2 ne
disait pas : une signature d'évaluateur sur trois n'est jamais appariée
positionnellement — sans changer la valeur du compteur existant, comme
demandé.

### D5 — non traité, hors périmètre (rappel)

Le cas du backend natif (`forge-generateur` / `forge-evaluateur`, rôles nus,
sans suffixe d'acteur, indétectable comme auto-jugement même quand c'est le
même acteur des deux côtés) n'a pas été touché. Ce n'est un échec d'aucune
Success Condition de ce lot ; le feedback le nomme explicitement hors
périmètre de cette itération et lui réserve un brief à soi.

### Gate mécanique final et suite de tests, sorties réelles recopiées

```
$ py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[PASS] verdict_numbers_traceable: all cited numbers trace to manifest.json
[PASS] no_bare_python_alias: no bare `python` invocations found
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur<->forge-evaluateur
[PASS] rubric_predates_deliverables: rubric (2026-08-11 10:16:38) predates earliest deliverable (2026-08-11 13:51:47.812779)
[PASS] declared_files_are_tracked: all 43 in-brief declared files are tracked; 5 declared outside the brief dir, not checked: [...]

VERDICT: ACCEPT
```

```
$ py -m pytest harness/tests/ -q
........................................................................ [ 23%]
........................................................................ [ 47%]
........................................................................ [ 70%]
........................................................................ [ 94%]
.................                                                        [100%]
305 passed in 24.36s
```

302 tests au départ de cette itération (vérifié par `git stash` +
`py -m pytest harness/tests/ -q` avant tout changement), 305 à la fin — les
3 nouveaux tests de refus D1/D2, aucun test retiré ni affaibli.

### Ce que je n'ai pas fait (itération 2)

- Je n'ai pas touché à la règle d'appariement par les `k` derniers auteurs :
  le feedback la déclare correcte pour SC3/SC3b/SC6, et les deux règles
  alternatives qu'il écarte (par ensembles d'acteurs, par l'avant)
  refuseraient le brief 009 à tort.
- Je n'ai pas touché à D5 (backend natif) : hors périmètre explicite de
  cette itération.
- Je n'ai pas commité : `git add` seulement, comme à l'itération 1.
- Je n'ai pas modifié `verdict.md` ni `feedback/feedback-010a.md`.

## Production — lot 010b (2026-08-11)

**Author**: forge-generateur-codex

### Budget et périmètre

Le split-check initial a estimé le lot à 100 unités (`SIZE_OK`). Le statut
budget était `UNMEASURABLE` dans ce worktree, faute de transcript Claude local
associé ; aucun chiffre n'a été inventé. Le lot ne touche ni les workflows, ni
`VISION.md`, ni le brief 009, et ne rédige aucun verdict.

### SC7 — wrapper Codex réel, même interface

`harness/backends/run_codex_generator.sh` expose exactement la même signature
que le wrapper Cursor :

```
harness/backends/run_codex_generator.sh: <brief_dir> [extra_dirs_colon_separated]
harness/backends/run_cursor_generator.sh: <brief_dir> [extra_dirs_colon_separated]
```

Le wrapper utilise l'interface non interactive stable `codex exec`, le prompt
sur stdin, `--cd`, `--sandbox workspace-write` et `--json`, conformément à la
référence officielle : https://developers.openai.com/codex/cli/reference/.
Il signe `forge-generateur-codex`, vérifie les livrables, écrit un état explicite
et ne touche jamais à `verdict.md`.

Deux appels réels ont été tentés sur le fixture inter-acteurs
`fx_010b_cross_actor`. Le premier a révélé que le bundle Desktop expose d'abord
un ELF Linux sans extension ; le wrapper préfère désormais le PE `codex.exe`
sur Windows. Le second a franchi le préflight puis l'ACL AppX a refusé
l'exécution du PE :

```
PREFLIGHT OK: generator/evaluator actors differ on all 1 examined pair(s): forge-generateur-codex<->forge-evaluateur-korrigan
harness/backends/run_codex_generator.sh: line 104: /c/Program Files/WindowsApps/OpenAI.Codex_26.803.5235.0_x64__2p2nqsd0c76g0/app/resources/codex.exe: Permission denied
wrapper exit: 1 ; codex exit: 126
```

La sortie complète est recopiée dans
`deliverables/proofs/wrapper-cross-actor-output.txt`; le JSONL vide, stderr et
fichier d'état produits par le wrapper sont conservés dans le fixture.

### SC8 — trois emplacements exacts dans forge-run

Commande réelle : `rg -n "codex" .claude/commands/forge-run.md`.

```
3:argument-hint: <brief-slug-or-path> [--backend claude|cursor|codex] [--max-iterations N]
19:- `--backend claude|cursor|codex` (default `claude`) — which Générateur backend
78:    elif backend == "codex": run bash harness/backends/run_codex_generator.sh <BRIEF_DIR>
```

Compteur : `forge_run_backend_mentions_count = 3`, échantillon 3.

### SC9 — ledger mesuré, jetons non inventés

`py harness/backends/ledger.py report` rend :

```
By backend:
    28  claude
     4  cursor
     2  codex
```

Compteur : `codex_invocations_in_ledger = 2`, sur les 3 lignes backend du
rapport. Les deux appels ont échoué avant le démarrage d'un modèle ; leur
`codex-run.jsonl` est vide. La dérogation du manifest cite la commande de
lecture, la sortie vide et l'erreur littérale, plutôt que de compter zéro
jeton sans preuve.

### SC10 — décision enregistrée

`docs/adr/0009-codex-as-official-generator-backend.md` porte `Status:
accepted`, enregistre l'interface, la mesure et le préflight, et
`docs/adr/README.md` contient sa ligne.

### SC11 — refus inter-acteurs réutilisé, pas réimplémenté

`harness/backends/codex_preflight.py` importe et appelle directement
`verdict_audit.check_verdict_not_self_authored`. L'appel réel du wrapper sur
`fx_sc3` a rendu 2 avant tout fichier de sortie :

```
REFUSING TO RUN: a forge-generateur-codex section would be self-authored against the existing verdict (same actor on 1/1 examined pair(s): forge-generateur-codex==forge-evaluateur-codex (examined: forge-generateur-codex<->forge-evaluateur-codex); plus 1 dropped-entry self-judgment(s): forge-generateur-codex==forge-evaluateur-codex).
```

La commande, Bash 5.2.37, la sortie et le code sont recopiés dans
`deliverables/proofs/wrapper-same-actor-output.txt`.

### Tests

Le test red-first a d'abord rendu `6 failed in 0.22s`. Après implémentation :

```
$ py -m pytest harness/tests/test_codex_backend.py -q
6 passed in 0.26s

$ py -m pytest harness/tests/ -q
311 passed in 22.09s
```

Le gate mécanique final a rendu 10/10 PASS et `VERDICT: ACCEPT`; sa sortie est
recopiée dans `deliverables/proofs/gate-010b-self-check.txt`. Il s'agit du
contrôle mécanique, pas d'un verdict écrit par le Générateur.

Le premier passage complet avait exposé une régression de libellé Cursor
(`NOT observable`) : corrigée puis rejouée sur la suite entière. Les sorties
rouge, verte et intermédiaire sont conservées dans les preuves. Aucun commit
n'a été créé et ce Générateur n'a écrit ni verdict ni jugement d'acceptation.

---

## Lot 010c — le verrou de fusion mesuré et spécifié, sans activation

**Author**: forge-generateur-codex
**Date**: 2026-08-11

Cette section concerne uniquement 010c. Elle n'ajoute aucun verdict et ne
prononce pas la recevabilité du lot ; Claude doit reconstruire les compteurs
dans une session distincte.

### Première action et budget

Commande :

```text
py harness/budget.py split-check --brief harness/queue/briefs/010-repartition-roles-full-auto --estimated-calls 80
```

Sortie réelle :

```text
advisory   : SIZE_OK   (advisory -- the Planificateur decides)
brief      : 010-repartition-roles-full-auto
estimated  : 80
```

Le suivi automatique de la session est ambigu et n'a pas été attribué à un
ancien journal arbitraire :

```text
py harness/budget.py status --brief harness/queue/briefs/010-repartition-roles-full-auto
status     : AMBIGUOUS
reason     : 2 transcripts name 010-repartition-roles-full-auto: agent-ab7ddd9fc8234c57a.jsonl (37 tool calls), agent-a3e4b7b0460596b89.jsonl (101 tool calls). Disambiguate with --agent <substring>.
Nothing is being enforced. This is not OK -- it is unmeasured.
```

### SC12 — le test lit la politique réellement exécutée

`harness/merge_bot_policy.py` lit directement
`.github/workflows/merge-bot.yml`. Il refuse un fichier absent, illisible,
vide ou tronqué et extrait trois éléments : les préfixes dans le `if:` du job,
la regex de denylist et la regex d'allowlist. Il ne contient aucune copie des
deux listes comme source de décision.

Le test a été écrit avant le module. Première exécution réelle :

```text
py -m pytest harness/tests/test_merge_bot_policy.py -q
E   ModuleNotFoundError: No module named 'harness.merge_bot_policy'
ERROR harness/tests/test_merge_bot_policy.py
1 error in 0.15s
```

Après création du module :

```text
py -m pytest harness/tests/test_merge_bot_policy.py -q
......                                                                   [100%]
6 passed in 0.04s
```

Deux tests construisent des copies temporaires élargies : l'une ajoute le
préfixe `codex/`, l'autre le chemin `docs/`. Dans les deux cas, l'assertion de
frontière devient rouge. Deux autres cas refusent un workflow vide ou tronqué,
afin de ne pas reproduire le défaut C3 du lot 009a.

Compteurs reconstruits depuis le workflow lui-même :

```text
mergebot_allowed_prefixes_count = 2
prefixes = ['cursor/', 'forge-bot/']
mergebot_allowed_paths_count = 3
paths = ['architecture/inbox/', 'architecture/reviews/', 'harness/queue/briefs/*/feedback/']
```

### SC13 et SC15 — chaîne réelle et porte conditionnelle inactive

Le document court `docs/rules/conditional-merge-gate.md` nomme l'étape
humaine exacte : pour une PR de code, le propriétaire clique aujourd'hui
« Merge pull request » dans GitHub, ou lance lui-même `gh pr merge`. Le job
actuel est ignoré dès le `if:` pour une branche `codex/` ou `forge/`.

La porte future est spécifiée par quatre lectures au même SHA :

1. contrôles GitHub présents et tous dans le compartiment `pass`, dont les
   deux jobs `harness-ci` ;
2. gate du brief exécuté sur un checkout propre, code 0, dix `[PASS]` et
   dernière ligne `VERDICT: ACCEPT` ;
3. verdict du lot explicitement ACCEPT, avec acteurs producteur/juge
   identifiables et différents ;
4. audit `cursor-cloud` suivi sous `architecture/inbox/`, dont
   `target_commit` égale exactement le SHA de tête et dont le schéma passe.

Le document impose de reconstruire les quatre preuves si le SHA change. Il
n'appelle aucune commande de fusion et dit dès sa première ligne qu'aucun
workflow ne le lit.

Mesure du périmètre workflow depuis le commit de départ du lot :

```text
git diff --numstat 3822c68 -- .github/workflows
(aucune sortie)
workflows_diff_bytes = 0
```

Les trois chaînes `TODO(operator` de `pipeline-audit.yml`,
`pipeline-challenge.yml` et `pipeline-forge-run.yml` sont toujours présentes ;
leur sortie complète est dans `deliverables/proofs/workflows-diff-010c.txt`.

### SC14 — cohorte réelle des PR fusionnées

Commande réellement exécutée contre GitHub, chaque PR étant relue avec
`gh pr view <numéro> --json files` puis comparée aux regex extraites du
workflow :

```text
py harness/queue/briefs/010-repartition-roles-full-auto/deliverables/proofs/measure_recent_prs_automergeable.py --limit 20
requested=20
returned=18
branch_prefixes=["cursor/", "forge-bot/"]
allowed_path_prefixes=["architecture/inbox/", "architecture/reviews/", "harness/queue/briefs/*/feedback/"]
{"automergeable": false, "changed_paths": 74, "head": "forge/010a-iteration-2", "number": 21, "reasons": ["préfixe de branche refusé", "denylist (1 chemin(s))", "hors allowlist (73 chemin(s))"]}
{"automergeable": false, "changed_paths": 46, "head": "forge/010a-contrat-roles", "number": 20, "reasons": ["préfixe de branche refusé", "denylist (1 chemin(s))", "hors allowlist (45 chemin(s))"]}
{"automergeable": false, "changed_paths": 10, "head": "forge/roles-full-auto", "number": 19, "reasons": ["préfixe de branche refusé", "hors allowlist (9 chemin(s))"]}
{"automergeable": false, "changed_paths": 1, "head": "codex/full-auto-session-handoff", "number": 18, "reasons": ["préfixe de branche refusé", "hors allowlist (1 chemin(s))"]}
{"automergeable": false, "changed_paths": 12, "head": "codex/009b-ci-budget-guard", "number": 17, "reasons": ["préfixe de branche refusé", "hors allowlist (12 chemin(s))"]}
{"automergeable": false, "changed_paths": 4, "head": "codex/009a-reevaluation", "number": 16, "reasons": ["préfixe de branche refusé", "hors allowlist (3 chemin(s))"]}
{"automergeable": true, "changed_paths": 1, "head": "cursor/codex-handoff-full-auto-79aa", "number": 15, "reasons": []}
{"automergeable": false, "changed_paths": 48, "head": "forge/cursor-audit-loop", "number": 14, "reasons": ["préfixe de branche refusé", "denylist (2 chemin(s))", "hors allowlist (45 chemin(s))"]}
{"automergeable": false, "changed_paths": 2, "head": "codex/hermes-observer-setup", "number": 13, "reasons": ["préfixe de branche refusé", "denylist (1 chemin(s))", "hors allowlist (2 chemin(s))"]}
{"automergeable": false, "changed_paths": 7, "head": "cursor/opus5-context-audit-brief-bd25", "number": 11, "reasons": ["hors allowlist (6 chemin(s))"]}
{"automergeable": false, "changed_paths": 100, "head": "forge/cursor-audit-loop", "number": 10, "reasons": ["préfixe de branche refusé", "hors allowlist (98 chemin(s))"]}
{"automergeable": true, "changed_paths": 1, "head": "cursor/audit-5633ee7-automation-gaps-73c6", "number": 9, "reasons": []}
{"automergeable": false, "changed_paths": 64, "head": "forge/cursor-audit-loop", "number": 8, "reasons": ["préfixe de branche refusé", "denylist (6 chemin(s))", "hors allowlist (62 chemin(s))"]}
{"automergeable": false, "changed_paths": 3, "head": "cursor/full-auto-pipeline-brief-342b", "number": 6, "reasons": ["hors allowlist (3 chemin(s))"]}
{"automergeable": true, "changed_paths": 1, "head": "cursor/postmerge-audit-42cb054-342b", "number": 5, "reasons": []}
{"automergeable": false, "changed_paths": 33, "head": "forge/cursor-audit-loop", "number": 4, "reasons": ["préfixe de branche refusé", "denylist (3 chemin(s))", "hors allowlist (32 chemin(s))"]}
{"automergeable": true, "changed_paths": 1, "head": "cursor/audit-bbe6da5-bare-python-matcher-3f31", "number": 3, "reasons": []}
{"automergeable": true, "changed_paths": 1, "head": "cursor/audit-6231186-execution-budgets-3f31", "number": 2, "reasons": []}
recent_prs_automergeable_count=5
sample_size=18
cohort_note=GitHub ne contient que 18 PR fusionnées; le dénominateur demandé 20 n'existe pas encore.
```

Le résultat observable est **5 sur 18**. Le dépôt ne possède que 18 PR
fusionnées : les numéros absents ou les PR fermées sans fusion ne sont pas des
observations légitimes. Le dénominateur 20 exigé par le brief n'est donc pas
atteignable aujourd'hui. La commande a réussi, il n'existe aucun corps
d'erreur permettant d'invoquer la dérogation prévue ; le manifest conserve
honnêtement `sample_size: 18`. Ce manque devra être jugé comme tel, jamais
masqué par deux lignes inventées.

### Suite complète

```text
py -m pytest harness/tests/ -q
........................................................................ [ 23%]
........................................................................ [ 46%]
........................................................................ [ 69%]
........................................................................ [ 92%]
.......................                                                  [100%]
311 passed in 24.46s
```

### Périmètre

Le lot ajoute un module de lecture, son test, un document de spécification et
des preuves sous les livrables du brief 010. Il ne modifie aucun workflow,
`VISION.md`, le brief 009, ses livrables, ses verdicts ou ses feedbacks.

### Gate mécanique

```text
py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto
[PASS] files_declared_exist: all declared files present
[PASS] mtime_after_brief: all deliverables postdate the brief
[PASS] captures_differ_when_should: all declared pairs differ
[PASS] waivers_have_command_and_error: all waivers carry a command and an error
[PASS] no_empty_sample_pass: every counter has a real sample_size
[PASS] verdict_numbers_traceable: all cited numbers trace to manifest.json
[PASS] no_bare_python_alias: no bare `python` invocations found
[PASS] verdict_is_not_self_authored: generator/evaluator actors differ on all 2 examined pair(s): forge-generateur<->forge-evaluateur; forge-generateur-codex<->forge-evaluateur
[PASS] rubric_predates_deliverables: rubric (2026-08-11 10:16:38) predates earliest deliverable (2026-08-11 22:58:04.418904)
[PASS] declared_files_are_tracked: all 49 in-brief declared files are tracked; 8 declared outside the brief dir, not checked

VERDICT: ACCEPT
```

Ce résultat est mécanique seulement. Il révèle aussi une limite à ne pas
masquer : en l'absence d'un verdict 010c, le contrôle positionnel associe la
nouvelle signature `forge-generateur-codex` à la dernière signature
`forge-evaluateur` déjà présente pour 010a. La passe mécanique ne prouve donc
pas qu'un Évaluateur a jugé 010c ; seul un verdict Claude ajouté ensuite peut
le faire. Ce lot ne modifie pas le contrôle accepté en 010a et consigne ce
constat pour une correction future.
