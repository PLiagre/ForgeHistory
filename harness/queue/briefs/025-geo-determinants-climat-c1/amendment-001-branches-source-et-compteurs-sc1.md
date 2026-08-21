# Amendement 001 — correction de F1 et F2 avant fusion du lot 025

**Authored**: 2026-08-21T12:20:00Z
**Author**: forge-planificateur
**Amende**: `harness/queue/briefs/025-geo-determinants-climat-c1/brief.md` et
`harness/queue/briefs/025-geo-determinants-climat-c1/eval-rubric.md`

> **Note de transparence (contrat du Planificateur) :** le rôle signataire est
> le rôle natif du harnais `forge-planificateur`. L'acteur réel est Claude
> Code (CTO), en session interactive, saisi par Hermes après la décision du
> propriétaire. Cette session n'a écrit que sous
> `harness/queue/briefs/025-geo-determinants-climat-c1/` : ni `hermes/**`, ni
> `docs/**`, ni `pipeline/**`, ni `ROADMAP.md`, ni `VISION.md`. Elle n'a rien
> committé, rien poussé, rien fusionné, n'a lancé ni Cursor ni ForgePilot, et
> n'a touché ni au code produit, ni aux artefacts, ni aux preuves, ni à la
> PR #123. Le Générateur n'a pas écrit une ligne de ce fichier.

**Ce fichier n'est pas une instruction.** Il dit ce qui a été décidé et
pourquoi. Ce qu'un agent doit faire reste écrit dans `brief.md`, et ce que
l'Évaluateur doit vérifier reste écrit dans `eval-rubric.md` (`CLAUDE.md` ›
Single Source of Instruction). Les passages amendés de ces deux fichiers
portent la marque `[A1]`.

---

## 1. La décision du propriétaire, et comment elle est parvenue

**Objet de la décision :** corriger les constats **F1** et **F2** de la
relecture du lot 025 **avant** la fusion de la PR #123 (draft, ouverte,
branche `agent/025-geo-determinants-climat-c1`).

**Source technique de ces constats :**
`.forgepilot/runs/20260821T110938Z-reviewer/result.json` du worktree
`025-geo-determinants-climat-c1` — relecture en lecture seule, verdict
d'ensemble `PASS`, avec `SC5` marqué `PARTIAL` et quatre points renvoyés à
une décision humaine.

**Comment la décision est arrivée :** en session interactive, par Hermes,
pilote du projet. **Il faut le dire franchement : aucun fichier
`hermes/requests/DEMANDE-*.md` ne porte cette décision au moment où ces lignes
sont écrites** — contrairement à l'amendement 001 du lot 026, qui cite
`hermes/requests/DEMANDE-20260821-arbitrage-gisements-026.md`. Écrire cette
trace durable est une écriture d'Hermes, hors du périmètre de cette session,
qui n'écrit que dans le répertoire de ce brief. Si le dépôt veut une trace
citable de cette décision — et il devrait —, c'est à Hermes de la déposer.

---

## 2. F1 — le dénominateur des branches `--source` était factuellement faux

### Le fait, mesuré

Le brief exigeait `branches_source_preexistantes_identiques` à `8` sur `8`.
L'instantané pré-édition committé n'en porte que **sept** :

```
grep -c 'if args.source == ' \
  harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/pre-edit/pipeline.py.orig
```

Sept branches explicites — `natural_earth_1400`, `cells`, `adjacency`,
`rivers`, `navigability`, `relief`, `natural_earth` — et une huitième valeur,
`fixture`, qui est la valeur par défaut et qui n'a **pas** de branche : elle
est servie par le **chemin de repli** en fin de `main()`, après la dernière
branche. Huit valeurs de `--source`, sept branches, un repli.

Le défaut est dans le brief, pas dans le code. Le Générateur a publié `7/7`,
ouvert un waiver avec sa commande et son message, et consigné l'écart dans
`generator-log.md` : c'est exactement la conduite que le Non-Goal 13 du brief
prescrit — une escalade, pas une réinterprétation tacite.

### La correction

- `branches_source_preexistantes_identiques` : dénominateur `7`, recompté sur
  l'instantané et jamais lu dans un document.
- Compteur **nouveau** `chemin_repli_fixture_identique`, `1` sur `1` : le
  chemin de repli `fixture` est comparé octet à octet entre l'instantané
  pré-édition et le fichier publié. Ses bornes textuelles sont fixées en `D8`
  du brief : de la ligne `if args.stage != "all":` au dernier `return 0` de
  `main()` inclus.
- `valeurs_source_preexistantes_conservees` **ne change pas** : `8` sur `8`
  est juste, `choices` porte bien huit valeurs. C'était le nombre de
  branches qui était faux, pas le nombre de valeurs.

Commande de la nouvelle mesure, depuis la racine du worktree du lot — les deux
empreintes doivent être égales :

```
for f in harness/queue/briefs/025-geo-determinants-climat-c1/deliverables/pre-edit/pipeline.py.orig \
         pipeline/geo/pipeline.py ; do
  sed -n '/^    if args.stage != "all":/,/^if __name__/p' "$f" | sed '$d' | sha256sum
done
```

**Exécutée à l'écriture de cet amendement : les deux empreintes sont
égales**, sur un bloc de vingt lignes. Le repli `fixture` est donc intact, et
le fichier publié porte huit branches explicites — les sept d'origine plus
`climate_drivers`. Les empreintes elles-mêmes ne sont pas recopiées ici
(règle n° 12) : elles se comparent à l'exécution.

### Pourquoi ce n'est pas un assouplissement d'après coup

C'est la seule question qui vaille quand on touche à une barre après avoir vu
le résultat. La réponse est mécanique : **la barre corrigée mesure
strictement plus que la barre d'origine.**

- L'énoncé d'origine demandait huit branches byte-identiques. Il n'y en a que
  sept : la condition était **insatisfaisable**, donc elle ne mesurait rien —
  soit on la bloquait pour toujours, soit on inventait une huitième branche.
- L'énoncé corrigé demande sept branches **plus** le chemin de repli. Il
  couvre les **huit** chemins de code que `--source` peut emprunter. Le repli
  `fixture`, lui, n'était protégé par **aucune** mesure avant cet
  amendement — c'est-à-dire précisément le chemin de la valeur par défaut, le
  plus emprunté de tous.

Passer de « une mesure impossible et un trou » à « deux mesures qui ferment
le trou » n'abaisse aucune exigence.

---

## 3. F2 — où vivent les cinq compteurs de SC1

### La question

La rubrique demandait de lire cinq compteurs — `inversions_insolation_latitude`,
`egalites_insolation_hors_tolerance`, `paires_consecutives_au_dessus_du_seuil`,
`cellules_jour_ete_non_superieur_hiver`, `inversions_amplitude_jour_latitude` —
dans `artifacts/stats_c1.json`. Ils n'y sont pas, et la liste de champs de
`D7` ne les y prévoyait d'ailleurs pas. Ils existent bien, ailleurs :
dans `deliverables/manifest.json` › `counters[]` avec valeur, `sample_size` et
commande, dans les chaînes `detail` de `C1-B` et `C1-C` de
`logs/v1_080_qa.json`, et re-dérivés par `measure_c1_025.py`. La rubrique
échouait donc au premier endroit où elle disait de regarder.

### La recommandation de l'orchestrateur, et sa vérification

La recommandation était : faire lire ces compteurs par la rubrique là où ils
existent déjà — `logs/v1_080_qa.json` et `deliverables/manifest.json` — plutôt
que d'élargir `stats_c1.json` après production.

**Vérifiée contre le brief, elle est la bonne solution, et elle est adoptée.**
Quatre raisons, dans l'ordre de leur poids :

1. **La source unique.** `stats_c1.json` décrit ce que le monde **est** :
   comptes, distributions, écrêtages réellement rencontrés. Les cinq
   compteurs disent ce qu'un **contrôle a trouvé** en balayant des paires
   triées. Les recopier dans l'artefact donnerait deux domiciles à la même
   valeur — le défaut « la même chose dans deux copies qui divergeront » que
   `D9` du brief refuse explicitement ailleurs, et une entorse au principe
   n° 1.
2. **Le domicile autoritaire existe déjà, et il est gardé.** Le brief exige
   de tout compteur qu'il figure dans `deliverables/manifest.json` avec sa
   valeur, sa `sample_size` et sa commande. Le gate mécanique lit cet
   endroit-là (`no_empty_sample_pass` refuse une `sample_size` nulle ou non
   calculée). Un champ nu ajouté à `stats_c1.json` ne serait lu par aucun
   garde-fou : la solution recommandée est mieux tenue, pas seulement moins
   coûteuse.
3. **Le périmètre.** Élargir `D7` après production imposerait de régénérer
   l'artefact, donc son empreinte, donc le bloc `determinism.sha256`, le
   `MANIFEST_c1.json` et la preuve entière — c'est-à-dire rouvrir le code et
   la PR pour déplacer un nombre qui est déjà publié et déjà vérifiable. Le
   propriétaire a exclu tout changement de code, d'artefact, de preuve ou de
   PR ; et même sans cette consigne, refaire une preuve pour l'emplacement
   d'un nombre serait un mauvais échange.
4. **Le garde-fou de l'échantillon vide reste entier.** Le disqualifiant
   transversal ne bouge pas : `paires_consecutives_au_dessus_du_seuil` doit
   être publié et strictement positif, et l'Évaluateur doit le reconstruire
   lui-même depuis `cells_g3.json` et `C1_MONOTONE_DLAT_DEG`. Seul l'endroit
   où on le lit change ; ce qui est exigé de lui, non.

### La décision écrite

- Emplacement **autoritaire** des cinq compteurs :
  `deliverables/manifest.json` › `counters[]`.
- **Corroboration** : les chaînes `detail` de `C1-B` et `C1-C` dans
  `logs/v1_080_qa.json`, écrites par le contrôle lui-même à l'exécution. Les
  deux lectures doivent concorder ; un écart est un fait à consigner.
- `artifacts/stats_c1.json` **ne les porte pas et ne doit pas les porter** :
  sa liste de champs reste close. Le brief le dit maintenant noir sur blanc,
  pour qu'un lot futur ne les y ajoute pas « pour aider ».
- Les faits du monde — `ecretages_polaires_total`,
  `coastal_cell_count_derive`, les distributions, les médianes par classe de
  sauts — restent dans `stats_c1.json`, où ils sont à leur place.

Cette règle vaut au-delà du lot 025 : un artefact du monde ne porte pas le
verdict des contrôles qui l'examinent. Si le dépôt veut en faire une règle
générale, c'est une écriture sous `docs/rules/`, hors du périmètre de cette
session.

---

## 4. Ce que cet amendement ne fait pas

- **Il ne modifie aucun code produit**, aucun artefact, aucune preuve, aucun
  registre, aucun journal, aucun `deliverables/**`, et ne touche pas à la
  PR #123.
- **Il n'ordonne aucune ré-exécution.** Les deux mesures corrigées se font
  sur des fichiers déjà committés : `chemin_repli_fixture_identique` est une
  comparaison de texte, les cinq compteurs de SC1 sont déjà publiés.
- **Il ne prononce pas la recevabilité du lot.** Le Planificateur n'évalue
  pas ; l'Évaluateur applique la rubrique amendée, et le propriétaire décide
  de la fusion.
- **Il n'abaisse aucune exigence** — voir le raisonnement du §2, qui est la
  raison pour laquelle une rubrique peut être amendée après production sans
  se corrompre.
- **Il ne retire pas le waiver** ouvert par le Générateur sur le dénominateur
  `8`. Ce waiver est résolu par cet amendement, mais il reste au dossier
  comme trace honnête d'une escalade correctement conduite.
- **Il n'écrit rien dans `hermes/**`, `docs/**` ni `ROADMAP.md`.**

---

## 5. F3 à F7 : non corrigés ici, renvoyés comme dette

Le propriétaire a demandé de corriger F1 et F2, et **eux seuls**. Les cinq
constats faibles restent tels quels dans le lot 025 ; aucun n'est
disqualifiant, aucun ne bloque la fusion. Ils sont recensés ici pour qu'ils ne
se perdent pas, sans que le brief ni la rubrique en portent une ligne — les
inscrire dans le brief après production changerait la barre du lot pour des
points que le propriétaire a écartés.

| constat | ce qu'il dit | ce qu'il devient |
|---|---|---|
| **F3** — `C1-F` tronque les listes aux cinquante premiers éléments | Le contrôle reproduit fidèlement la troncature de `g5b_d_no_upstream_limit_encoded`, comme `D5` l'exigeait ; sa portée réelle est donc plus étroite que le « à quelque profondeur que ce soit » du brief | **Dette, lot dédié.** La limite est héritée : la corriger dans le seul `checks_c1.py` créerait deux contrôles jumeaux qui divergent. À traiter dans les deux à la fois, avec un cas rouge au-delà du cinquantième élément |
| **F4** — `code_sortie_run_proof_c1` est relu par défaut, non ré-exécuté | Le drapeau `--rerun-proof` de `measure_c1_025.py` fait la vraie mesure, mais la commande citée au manifeste ne l'emploie pas | **Dette légère.** L'Évaluateur ré-exécute la preuve de toute façon (Condition 6 de la rubrique) ; à corriger dans le patron de mesure d'un lot futur |
| **F5** — branche d'égalité morte dans le départage des zones de mer | `best_zone` initialisé à `-1` rend la comparaison d'égalité inatteignable ; le comportement exigé par `D4` est néanmoins celui obtenu, l'itération se faisant par `zone_id` croissant | **Dette de propreté.** Aucun effet sur le résultat ; à nettoyer quand ce module sera rouvert |
| **F6** — une formulation du `generator-log.md` | « conforme au contexte planificateur » rapproche un fait mesuré d'un nombre de contexte ; le code ne contient aucun littéral `21`, `372` ou `596`, donc la règle n° 2 n'est pas enfreinte | **Rien à corriger dans ce lot** — le journal du Générateur ne se réécrit pas après coup. Point de vigilance de rédaction pour les lots suivants |
| **F7** — cas rouge de `C1-D` à un seul sens | `D11` n'exige qu'un cas rouge par contrôle ; le second sens est la contre-preuve confiée à l'Évaluateur par la rubrique | **Rien à corriger.** Signalé pour que l'Évaluateur n'oublie pas de monter le second sens lui-même |

Trois points de la relecture appelaient une décision humaine et **ne sont pas
tranchés ici**, faute de mandat : la ré-exécution indépendante des preuves
(la relecture n'a exécuté aucune commande, et les deux captures n'ont été
regardées par personne — règle n° 11), la portée de `C1-F` (F3), et la
conduite à tenir sur le dépôt d'une trace durable de la décision du
propriétaire (§1).

---

## 6. Ce qui a été vérifié à l'écriture de cet amendement

| contrôle | résultat |
|---|---|
| branches `if args.source == ` dans l'instantané pré-édition | **7** ; dans le `pipeline.py` publié : **8** (les sept plus `climate_drivers`) |
| chemin de repli `fixture`, empreintes des deux versions | **égales** — bloc de vingt lignes, byte-identique |
| présence des cinq compteurs de SC1 dans `deliverables/manifest.json` | **présents**, chacun avec valeur et `sample_size` |
| présence des cinq compteurs dans `artifacts/stats_c1.json` | **absents** — conforme à `D7`, qui ne les y prévoit pas |
| `paires_consecutives_au_dessus_du_seuil` publié et strictement positif | **oui**, au manifeste et dans les `detail` de `C1-B` et `C1-C` |
| `harness/budget.py split-check` sur le brief amendé | exécuté — voir le compte rendu de session |
| `harness/tests/test_single_source_of_instruction.py` | exécuté — aucun titre de `brief.md` paraphrasé hors de `brief.md` |
| matcher `no_bare_python_alias` sur les trois fichiers de ce répertoire | exécuté — aucune invocation nue de l'interpréteur |

---

## Annexe — correspondance des noms, constatée le 2026-08-21

Aide de lecture, **non normative** : les noms de clés ci-dessous sont ceux que
le Générateur a employés dans son rapport, relevés sur les preuves committées.
La rubrique ne les impose pas ; ce qu'elle impose, c'est que le compteur soit
lisible au manifeste et corroboré par le rapport, et que l'Évaluateur
reconstruise lui-même la valeur.

| compteur du brief | dans `logs/v1_080_qa.json` |
|---|---|
| `inversions_insolation_latitude` | `detail` de `C1-B`, champ `inversions` |
| `egalites_insolation_hors_tolerance` | `detail` de `C1-B`, champ `equal_bad` |
| `paires_consecutives_au_dessus_du_seuil` | `detail` de `C1-B` et `C1-C`, champ `pairs_above_thresh` |
| `cellules_jour_ete_non_superieur_hiver` | `detail` de `C1-C`, champ `summer<=winter` |
| `inversions_amplitude_jour_latitude` | `detail` de `C1-C`, champ `amp_inversions` |
