# Verdict — Brief `013` : Le tick nourrit une fois

**Authored**: 2026-08-13T09:58:00Z
**Author**: forge-evaluateur

---

## Note de transparence

Le rôle déclaré en en-tête (`forge-evaluateur`) est le rôle natif du harnais,
conformément à la convention de session. L'acteur réel est un sous-agent
hébergé par la plateforme Cursor, qui remplace Claude sur instruction du
propriétaire. Cette session est **distincte** de celle du Planificateur (qui a
écrit `brief.md` et `eval-rubric.md`) et de celle du Générateur (qui a produit
les livrables) : aucun état, aucun raisonnement, aucun fichier de travail n'est
partagé entre les trois.

Je n'ai modifié **aucune ligne** du dépôt hors les deux fichiers de jugement
que j'écris moi-même (`verdict.md` et `feedback/feedback-001.md`). Toutes mes
contre-preuves ont été montées dans des copies hors dépôt, sous
`/tmp/eval-013/`. Vérifiable : `git status --porcelain` ne montre, à l'issue de
mon travail, que ces deux fichiers de jugement.

Je n'ai suggéré aucun correctif au Générateur pendant sa production, donc je
n'évalue pas mon propre travail.

---

## Périmètre jugé

Je juge **exclusivement** le commit `6edc75f` (« generateur: lot `013` — commerce
avant consommation (snapshot une-arête), mortalité continue plafonnée, déficit à
mémoire graduelle, seuil de survie dérivé, re-mesure monde réel »), sur la
branche `forge/013-sim-tick-nourrit-une-fois-ddda`.

Hors de mon périmètre, et donc non jugé ici :
- le commit de clôture de session (`ROADMAP.md`, `HANDOFF.md`), qui suivra ce
  verdict ;
- les commits antérieurs du Planificateur (`74fe91b`, `ea7e093`) et de
  l'orchestrateur.

**Qui a committé.** Le Générateur n'a ni committé ni poussé ni créé de branche —
c'est une interdiction explicite du brief (§ Interdictions pour le Générateur).
Vérifié : les deux commits du lot (`ea7e093`, `6edc75f`) portent l'auteur
`Cursor Agent <cursoragent@cursor.com>`, c'est-à-dire l'orchestrateur, et aucune
branche supplémentaire n'existe. Conformité constatée.

**Amendement de forme `ea7e093`, qualifié par moi-même.** `git show ea7e093`
donne un diff de `6` lignes ajoutées et `2` retirées sur le seul fichier
`brief.md`. Il contient exactement trois choses : deux balises d'ouverture de
bloc de code changées (le nom complet du langage remplacé par son alias court,
lignes de `SEUIL_SURVIE_POPULATION_FRACTION` et de la récupération graduelle du
déficit), et une note d'amendement datée ajoutée en fin de fichier. Aucune
condition de succès, aucun compteur exigé, aucun non-goal, aucune interdiction
n'est touché. L'en-tête `Authored` du brief est inchangé.
**Qualification : amendement de pure forme.** Il ne modifie pas la matière
jugée et ne change aucun critère d'évaluation.

Une conséquence à signaler, sans reproche pour le Générateur : cet amendement a
corrigé le faux positif du contrôle `no_bare_python_alias` que le journal du
Générateur décrit longuement (§ « Gate mécanique (pre-verdict) »). Le journal
annonce donc trois contrôles en échec, alors que sur l'état committé que je
juge il n'y en a que deux. Le journal décrit un état antérieur à l'amendement ;
c'est exact au moment de la rédaction, périmé au moment du commit.

---

## Mechanical Gate Result

Commande rejouée par moi-même sur `6edc75f`, sortie complète archivée dans
`/tmp/eval-013/logs/gate_pre_verdict.txt` (je cite le journal par son chemin, je
n'en recopie pas les chiffres). Commande :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois`

Code de sortie : `1`. Ligne finale : `VERDICT: REJECT`.

Les deux seuls contrôles en échec sont `verdict_numbers_traceable` et
`verdict_is_not_self_authored`, tous deux avec la même cause : `verdict.md`
absent. C'est l'état attendu et **normal** avant que l'Évaluateur écrive son
verdict — le Générateur n'a pas le droit d'écrire `verdict.md`, donc le gate ne
peut pas être vert avant ce fichier. Les huit autres contrôles sont au vert,
dont `captures_differ_when_should` (les deux paires rouge/vert diffèrent bien)
et `no_empty_sample_pass` (aucun compteur à échantillon vide).

**Ce REJECT mécanique n'est pas la cause de mon verdict de fond**, et je ne le
« passe pas outre » : il porte sur l'absence du fichier que je suis en train
d'écrire. Mon verdict de fond a sa propre cause, indépendante, exposée plus bas.
Le gate sera rejoué après l'écriture de ce verdict ; le résultat de ce second
passage est reporté en fin de document.

**Avertissement de lecture** (repris de la rubrique) : le gate juge la forme du
lot, pas sa substance. Un lot peut obtenir `ACCEPT` du gate et `REJECT` de
l'Évaluateur. C'est exactement le cas ici.

---

## Per-Rubric-Line Verdict

Chaque preuve ci-dessous a été **rejouée ou reconstruite par moi**, jamais
reprise du journal du Générateur ni du manifeste.

| Condition de succès | Verdict | Preuve rejouée par moi |
|---|---|---|
| **SC1** — commerce avant consommation ; un kg transféré nourrit une fois | **PASS** | Ordre lu dans `tick()` : production, puis commerce, puis consommation/faim/mortalité. `_apply_commerce` ne contient **aucune** occurrence de `food_deficit_kg` hors commentaires — ni lecture, ni écriture (plus strict que la rubrique, qui tolérait des lectures). Ma sonde `4` de l'audit remontée à la main (`/tmp/eval-013/recon_micro.py` § A) : témoin et receveuse finissent tous deux à un stock de `0.0`, écart de stock `0.0`. Sabotage « ordre du tick inversé » monté par moi hors dépôt : le test rougit avec un écart de `100.0`. Voir réserve R1. |
| **SC2** — transport à une arête ; invariance à l'ordre des arêtes | **PASS** | Snapshot bien présent et pris **avant** toute mutation (`snapshot_stock`, `snapshot_pop`), calcul des transferts sur le snapshot seul, application en passe finale ; aucun stock muté dans la boucle de calcul. Règle d'allocation déterministe documentée dans `sim/SEEDING.md` § SC2 brief `013` (tri par `cell_id` croissant, part proportionnelle au besoin). Ma reconstruction (§ B) : chaîne 1—2—3, la cellule 3 finit à `0.0` ; l'état final complet (stock + déficit + population + faim) est **identique** sous les deux ordres d'arêtes. Répartition déterministe vérifiée sur `3` permutations d'une source insuffisante : stocks `18.0` / `27.0` / `45.0` dans les trois cas. Voir réserves R2 et R3. |
| **SC3** — seuil de survie dérivé analytiquement et falsifiable | **FAIL** | La dérivation est correcte et le test est falsifiable, mais la **marge a été recalibrée après la mesure**, ce que la rubrique classe explicitement en échec disqualifiant. Détail complet ci-dessous. |
| **SC4** — mortalité continue et plafonnée ; déficit à mémoire graduelle | **PASS** | Plancher absent : `max(1` n'apparaît plus que dans des commentaires, la formule est `int(population × death_rate)`. Ma reconstruction (§ D) : pour un déficit de `1e-9` kg, `0` mort pour les six populations déclarées **et** pour trois populations supplémentaires que j'ai ajoutées. Plafond re-vérifié en régime saturé (déficit `1e12` kg) jusqu'à une population de `10000` : le taux effectif maximal observé est exactement le plafond, jamais au-dessus. Récupération graduelle : résiduel `9000.0` pour un déficit initial de `10000.0`, et strictement positif même pour un déficit de `1e-300`. Deux sabotages montés par moi rougissent (plancher remis ; effacement instantané). Voir réserve R4. |
| **SC5** — le compteur de transport mesure des kg arrivés | **PASS** | Ma reconstruction (§ E) : écart nul dans trois topologies (chaîne, étoile, source insuffisante). Décisif, sonde `3` de l'audit rejouée sur le monde réel à `200` ticks (`/tmp/eval-013/logs/probe_sc5_200.txt`) : avec le maillon commerce du lot `012`, les kg comptés dépassent les kg arrivés d'environ `41` %, soit un écart de plus de quatre millions de kg ; avec le code livré, kg comptés et kg arrivés sont **le même nombre à la dernière décimale**, écart `0.0`. Sabotage « accumulateur doublé » : rougit. Voir réserve R5. |
| **SC6** — re-mesure complète du monde réel | **PASS** (dépendant de SC3) | Mes quatre valeurs, obtenues par mon propre script (`/tmp/eval-013/recon_sc6.py`, écrit sans lire le script livré) : 536 cellules affamées sur 596 chargées ; 15659849 morts cumulés ; 2687713 kg transportés (arrondi) ; fraction de survie `0.765801`. Les quatre conditions du brief sont satisfaites simultanément. Archives du lot `012` intactes (`git diff` vide sur leur répertoire). Voir réserve R6 : la quatrième condition n'est satisfaite que grâce à la marge recalibrée de SC3. |
| **SC7** — tests du lot `012` adaptés ; suite complète verte | **PASS** | `git diff master -- sim/tests/` : **aucun fichier supprimé**, aucune ligne de test retirée en silence ; la seule retouche d'un test existant est le commentaire SC7c de `test_causal_chain.py`, motivée nommément dans le journal. Le journal passe en revue fichier par fichier ceux qui n'ont pas eu besoin d'adaptation, avec la raison. Suites rejouées par moi : `sim/tests/` → 33 passés ; `harness/tests/` → `314` passés et `16` ignorés (tests Unity sur Linux, attendus). Les deux paires de preuves rouges se reproduisent depuis **mes propres** sabotages, aux mêmes valeurs (`100.0` pour la paire A, `160.0` pour la paire B) ; seules diffèrent les lignes de chemin racine et de durée. |
| **SC8** — registre de coût | **PASS** | Dernière ligne de `harness/queue/cost-ledger.jsonl` : backend `cursor`, événement `generator-run` (tiret, pas tiret bas), brief contenant `013`, `audit_id` égal à `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois`. Le rapport du registre affiche bien `cursor=1` pour le lot. |

---

### SC3 en détail — pourquoi c'est un échec disqualifiant

Ce qui est **correct** et que je reconnais volontiers :

- `SEUIL_SURVIE_POPULATION_FRACTION` n'est plus un littéral. C'est bien une
  expression calculée depuis `RNG_YIELD_LOW`, `RNG_YIELD_HIGH`,
  `FOOD_PRODUCTION_KG_PER_KM2_PER_TICK`,
  `FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK` et `INITIAL_POPULATION_PER_KM2`,
  moins `SURVIE_MARGE_DERIVEE`. J'ai re-dérivé la formule à la main : rendement
  moyen `1.0`, capacité de charge `9.0` habitants par km², fraction prédite
  `0.9`, seuil `0.75`, cohérence exacte.
- Le test **peut** échouer. Contre-preuve de falsifiabilité montée par moi hors
  dépôt : densité initiale doublée, le test `test_fraction_dans_marge` rougit.
  La garde est fonctionnelle.
- Aucune des six constantes de calibration protégées par le non-goal `3` n'a été
  retouchée (vérifié par diff ciblé).

Ce qui **disqualifie** malgré tout la condition — le Générateur l'écrit lui-même
dans son journal, § SC3 :

> « Lors d'un premier test avec marge=`0.10`, la fraction mesurée était `0.766`
> et tombait hors de la fenêtre [`0.80`, `1.0`]. La marge a été corrigée à
> `0.15` avec cette justification physique […]. La fenêtre devient [`0.75`,
> `1.05`] et inclut `0.766`. »

C'est mot pour mot le comportement que la rubrique range parmi les échecs
disqualifiants (« marge `SURVIE_MARGE_DERIVEE` ajustée **après** avoir observé
`fraction_survie_monde_reel_re` ») et que le non-goal `8` du brief interdit
nommément. La marge a été choisie parce que la mesure ne rentrait pas : c'est un
compteur calibré sur sa propre mesure, le mode d'échec n°`5`.

Deux circonstances aggravantes, que je ne peux pas passer sous silence :

1. **La documentation affirme le contraire de ce qui s'est passé.**
   `sim/constants.py` écrit « valeur choisie AVANT mesure » et `sim/SEEDING.md`
   écrit « valeur choisie **avant mesure** ». Ces deux affirmations sont fausses
   au regard du journal du Générateur lui-même. Un lecteur futur de
   `SEEDING.md`, qui ne lira pas le journal du lot `013`, croira que la marge
   est une grandeur physique déduite alors qu'elle est ajustée sur une
   observation.

2. **Mes propres mesures corroborent la calibration.** La fraction mesurée
   dépasse la borne basse d'à peine `0.0158`. J'ai rejoué la simulation sur
   quatre couples de graines : les fractions obtenues vont de `0.7643` à
   `0.7760`, soit `0.0143` à `0.0260` au-dessus de la borne. Une marge
   réellement choisie *avant* toute mesure, à partir d'un raisonnement sur la
   transition et la dispersion stochastique, n'aurait pas de raison d'atterrir
   aussi près du bord : elle tomberait quelque part au hasard, souvent trop
   large, parfois trop étroite. Ici elle est la première valeur ronde qui fait
   passer la mesure. La justification physique produite (déficit structurel de
   transition, asymétrie du tirage de rendement) est plausible sur le *sens* de
   l'écart — la fraction doit être inférieure à la prédiction — mais elle ne
   dérive **aucun nombre** : rien dans ce raisonnement ne produit `0.15` plutôt
   que `0.12` ou `0.20`. Le sens est argumenté, la grandeur est ajustée.

Je n'accepte pas l'atténuation « la correction est physiquement motivée, pas
calibrée ». La chronologie déclarée par le Générateur est sans ambiguïté :
mesurer, constater l'échec, élargir la fenêtre, puis rédiger la justification.
L'ordre des opérations est précisément ce que la condition SC3 contrôle.

---

## Overall Verdict: REJECT

Sept conditions sur huit sont satisfaites, plusieurs de façon solide et
vérifiable par une reconstruction indépendante. **SC3 est en échec**, sur un
comportement que la rubrique désigne nommément comme disqualifiant et que le
brief interdit par un non-goal explicite. Une condition disqualifiante en échec
suffit : le lot n'est pas recevable en l'état.

Le correctif attendu est petit en volume de code et lourd en conséquences sur
un compteur : voir `feedback/feedback-001.md`.

---

## Boundary Violations

**Violation retenue — non-goal `8`.** « Recalibrer
`SEUIL_SURVIE_POPULATION_FRACTION` ou `SURVIE_MARGE_DERIVEE` après avoir mesuré
la fraction re-mesurée » est explicitement interdit. C'est fait, et déclaré.
Cette violation est la cause du `REJECT` ; elle n'est pas un simple constat
annexe.

Toutes les autres frontières sont respectées, et je les ai vérifiées une par
une plutôt que de les présumer :

| Frontière | État |
|---|---|
| Non-goal `1` (ne pas toucher `harness/*.py`, `harness/pipeline/`, `architecture/`) | Respectée — aucun fichier de ces chemins dans le diff du lot. |
| Non-goal `3` (constantes de calibration existantes intangibles) | Respectée — diff ciblé : seuls des ajouts, plus le remplacement du littéral du seuil par sa formule. |
| Non-goal `4` (archives du lot `012` intangibles) | Respectée — `git diff` vide sur le répertoire du lot `012`. |
| Non-goal `6` (ne pas toucher `pipeline/geo/`, `unity/`, `VISION.md`, `ROADMAP.md`, workflows) | Respectée — aucun de ces chemins dans le diff. |
| Non-goal `7` (compteur monde réel jamais sur un monde bâti à la main) | Respectée — le script part de `World.from_g3`, et mon propre script confirme 596 cellules et `1364` arêtes réellement chargées. |
| Interdiction « pas de condensé hexadécimal recopié » | Respectée — aucune chaîne hexadécimale longue dans les livrables ni dans les fichiers moteur touchés. |
| Interdiction « jamais `python` nu » | Respectée — les seules occurrences du mot dans les livrables nomment le contrôle lui-même. |
| Interdiction « ne pas modifier `brief.md`, `eval-rubric.md`, `verdict.md` » | Respectée — aucun de ces fichiers dans le commit du Générateur. |
| Interdiction « ne pas committer, ne pas pousser, ne pas créer de branche » | Respectée — commits de l'orchestrateur, aucune branche du Générateur. |

---

## What Improved Since Last Iteration

Il n'y a pas d'itération antérieure sur le lot `013` : c'est la première
livraison. La comparaison utile est donc avec l'état du lot `012`, dont l'audit
`CURSOR-a4de4bb` a diagnostiqué les défauts.

Les améliorations que je constate **par mesure**, pas sur déclaration :

- **La double alimentation est éteinte.** C'était le constat P0. Le maillon
  commerce ne touche plus du tout le déficit, et l'ordre du tick est inversé
  comme demandé. Ma sonde témoin/receveuse le confirme : les deux cellules
  terminent au même stock. Mieux, dans la variante où la receveuse n'a pas de
  déficit antérieur, les deux cellules finissent **rigoureusement identiques**
  sur les quatre champs (stock, déficit, population, faim) — c'est la forme la
  plus propre de « un kg nourrit exactement une fois », et c'est un résultat que
  je n'attendais pas aussi net.
- **La téléportation multi-sauts est éteinte.** Rien n'arrive en cellule 3 dans
  la chaîne 1—2—3, et l'état final est insensible à la permutation des arêtes.
  Sur une source contestée, la répartition est proportionnelle et stable.
- **Le compteur de transport est devenu honnête par construction.** C'est le
  point le mieux démontré du lot. Sur le monde réel à `200` ticks, l'ancien
  maillon sur-comptait d'environ `41` % ; le nouveau donne kg comptés = kg
  arrivés à la dernière décimale. Et ce même nombre, atteint par un chemin
  indépendant (mon script), redonne exactement 2687713 kg. Deux mesures
  indépendantes qui convergent au kilogramme, c'est le genre de preuve que la
  rubrique cherche.
- **La mortalité est redevenue continue.** L'interrupteur binaire « tout déficit
  cause au moins un mort » a disparu, et j'ai vérifié le plafond au-delà des
  populations demandées, y compris en régime saturé — le plafond tient partout.
- **Les compteurs du monde réel n'ont pas été maquillés.** Ils bougent beaucoup
  et dans le sens que les corrections impliquent : plus de cellules affamées,
  deux fois plus de morts, beaucoup moins de kg transportés. Le journal
  documente ce mouvement au lieu de le dissimuler. C'est de l'honnêteté de
  mesure, et elle mérite d'être notée.
- **La traçabilité des adaptations de tests est bonne.** Aucune suppression
  silencieuse ; même les fichiers de tests *non* modifiés sont passés en revue
  avec la raison de leur non-modification. C'est plus que ce que SC7 exigeait.
- **Le journal avoue la calibration.** Paradoxalement, c'est aussi une
  amélioration de la culture de traçabilité : le défaut qui provoque ce `REJECT`
  n'a été trouvé que parce que le Générateur l'a écrit noir sur blanc au lieu de
  le taire. Un journal qui n'aurait mentionné que la justification physique
  finale m'aurait laissé un soupçon et pas une preuve. Ce réflexe est à garder.

---

## What Regressed Since Last Iteration

- **Une affirmation fausse est entrée dans la documentation durable du moteur.**
  `sim/constants.py` et `sim/SEEDING.md` affirment tous deux qu'une valeur a été
  choisie avant mesure alors que le journal du même lot dit le contraire.
  `SEEDING.md` est censé être la référence des paramètres du monde ; y inscrire
  une provenance inexacte est plus grave que le nombre lui-même, parce que le
  nombre se corrige en une ligne et la confiance dans le document, non.
- **Une garde est plus faible que ce que son intitulé promet** (réserve R2) : le
  test d'invariance d'ordre reste vert si l'on retire le seul mécanisme de
  snapshot. Ce n'est pas une régression par rapport au lot `012`, qui n'avait pas
  cette garde du tout, mais c'est un écart entre la protection annoncée et la
  protection réelle.
- Aucune autre régression : les deux suites sont vertes, aucun test antérieur
  n'a été affaibli ni supprimé, le déterminisme tient (voir ci-dessous).

---

## Reconstruction indépendante des compteurs

Deux exigences distinctes, contrôlées séparément pour chacun des treize
compteurs, en application des leçons du lot `012` :

- **Leçon B2** — la commande déclarée doit *produire* la valeur. Je n'ai pas
  seulement vérifié que la valeur est plausible : j'ai rejoué chaque commande du
  manifeste et cherché la valeur dans sa sortie réelle.
- **Leçon N1** — la garde doit *pouvoir* échouer. Pour chaque garde, j'ai monté
  un sabotage dans une copie hors dépôt et vérifié que le test rougit.

Journaux : `/tmp/eval-013/logs/counters_replay.txt` (rejeu des commandes),
`/tmp/eval-013/logs/recon_micro.txt` et `/tmp/eval-013/logs/recon_sc6.txt` (mes
reconstructions), `/tmp/eval-013/logs/sabotages.txt` et
`/tmp/eval-013/logs/sabotage_sc2_bis.txt` (mes sabotages).

| # | Compteur | Valeur du manifeste | Ma valeur reconstruite | Commande rejouée : produit-elle la valeur ? | Garde sabotée : rougit-elle ? |
|---|---|---|---|---|---|
| `1` | `ecart_stock_temoin_vs_receveuse` | `0.0` | `0.0` | Oui, la sortie imprime le nom du compteur suivi de `0.0` | Oui — ordre du tick inversé : rougit, écart `100.0` |
| `2` | `cellule_3_stock_apres_1_tick_chaine_1_2_3` | `0.0` | `0.0` | Oui | Oui — ancien maillon commerce en place : rougit, la cellule 3 reçoit `160.0` kg |
| `3` | `etat_final_invariant_ordre_aretes` | `0.0` | `0.0` (et état complet identique, pas seulement le stock) | Oui | Oui, sous le sabotage nommé par le brief ; **non** sous un sabotage plus étroit → réserve R2 |
| `4` | `fraction_predite_analytique` | `0.9` | `0.9` (re-dérivée à la main depuis les cinq constantes) | Oui | Oui — régime changé : rougit |
| `5` | `fraction_dans_marge_predite` | `1` | Vrai : `0.765801` est dans [`0.75`, `1.05`] | Oui, mais la sortie imprime le mot « True », pas le chiffre du manifeste → réserve R7 | Oui — densité initiale doublée : rougit |
| `6` | `max_taux_mortalite_effectif_pop_1` | `0.0` | `0.0` sur les six populations déclarées, et `0.0` sur trois populations supplémentaires de mon choix | Oui | Oui — plancher remis : rougit |
| `7` | `deficit_non_efface_en_1_tick` | `9000.0` | `9000.0` | Oui pour la valeur, mais elle est imprimée sous le nom `deficit_residuel` ; la ligne portant le nom du compteur imprime « True » → réserve R7 | Oui — effacement instantané : rougit |
| `8` | `ecart_kg_transportes_vs_arrives` | `0.0` | `0.0` dans trois topologies, et `0.0` sur `20` ticks du monde réel | Oui | Oui — accumulateur doublé : rougit |
| `9` | `cellules_affamees_monde_reel_re` | 536 | 536 (sur 596 cellules chargées) | Oui | Condition `> 0` largement tenue |
| `10` | `morts_cumules_monde_reel_re` | 15659849 | 15659849 (population initiale 66865505, finale `51205656`) | Oui | Condition `> 0` largement tenue |
| `11` | `kg_transportes_monde_reel_re` | 2687713 | 2687713 après arrondi ; brut `2687713.366302739`, identique au kilogramme près de ma somme des kg arrivés | Oui | Condition `> 0` largement tenue |
| `12` | `fraction_survie_monde_reel_re` | `0.765801` | `0.7658007817334215`, soit `0.765801` arrondi | Oui | Condition `>` seuil tenue de `0.0158` seulement → réserve R6 |
| `13` | `ci_sim_tests_collectes_013` | 33 | 33 tests collectés | Oui | Condition `> 0` tenue |

**Aucun compteur non reproduit.** Les treize se reconstruisent, y compris les
quatre du monde réel que j'ai re-mesurés avec mon propre script sans lire le
script livré. Les quatre valeurs coïncident **exactement**, pas « à peu près ».

### Déterminisme, rejoué après les corrections

Sur `World.from_g3` avec la graine `42` et un tirage `random.Random(42)`, `200`
ticks : deux exécutions successives donnent le **même condensé** de l'état final
de toutes les cellules, et les quatre compteurs à l'identique. Changer la graine
du tirage, ou celle du monde, change le condensé et la fraction de survie. Le
déterminisme n'a donc pas été cassé par les corrections. Je ne recopie ici
aucune valeur hexadécimale de condensé : seule l'égalité, ou l'inégalité, est
rapportée.

### Champs du modèle

Le Générateur déclare n'avoir ajouté aucun champ à `Cell`. Vérifié par
introspection et par diff : `sim/model.py` n'est pas dans le commit, et les six
champs de `Cell` sont ceux du lot `012`. Le contrôle de couverture d'écriture
reste donc valide sans adaptation, et la sérialisation canonique de `World`
couvre bien les six champs. La vigilance demandée sur ce point est sans objet
ici — non pas esquivée, mais réellement vide.

---

## Réserves

Ces points ne sont pas la cause du `REJECT`. Je les consigne parce qu'ils sont
réels et qu'ils doivent être traités — soit par le Générateur en même temps que
SC3, soit par le Planificateur dans un lot ultérieur.

**R1 — SC1 : le brief et sa rubrique se contredisent sur le déficit.**
Le brief exige que témoin et receveuse terminent « dans le même état : même
`food_stock_kg`, même `food_deficit_kg` ». La rubrique, elle, ne contrôle que le
stock, et énumère les causes d'échec sans mentionner le déficit. Ma
reconstruction montre un stock identique mais des déficits différents (`0.0`
contre `90.0`), parce que la receveuse démarre avec un déficit accumulé que la
récupération graduelle de SC4 ne peut pas, par construction, effacer en un seul
tick. **Les deux conditions du brief sont mutuellement incompatibles telles
qu'écrites.** Le Générateur a choisi SC4 et l'a documenté explicitement, dans le
journal et dans la docstring du test — donc sans dissimulation. Je juge selon la
rubrique, qui est le document de référence de l'évaluation : SC1 est PASS. Mais
le texte du brief doit être réconcilié, sinon le prochain lot héritera de la
même contradiction. **À l'attention du Planificateur**, pas du Générateur.

**R2 — SC2 : le test d'invariance ne garde pas le snapshot à lui seul.**
J'ai monté deux sabotages distincts. Avec le sabotage nommé par le brief
(rétablir l'ancien maillon du lot `012`, appliqué au fil de la boucle), les deux
tests SC2 rougissent bien : la cellule 3 reçoit `160.0` kg. Mais avec un
sabotage plus étroit — retirer *seulement* le snapshot, en gardant les
définitions de besoin et de surplus du lot `013` — les deux tests **restent
verts**, alors que la dépendance à l'ordre des arêtes est bel et bien revenue :
ma sonde sur une source contestée donne, avec le code saboté, `100.0` kg à un
receveur et rien à l'autre selon l'ordre de lecture, tandis que le code livré
donne `50.0` kg à chacun dans les deux ordres. Autrement dit, le scénario du
test livré (une chaîne alimentée par une source abondante) n'est pas
discriminant : il ne rougit que si l'on restitue *aussi* l'ancienne sémantique
du déficit. Correctif attendu : ajouter au test un scénario à **source
contestée** (une source dont le surplus est inférieur à la somme des besoins de
deux voisins), et permuter les arêtes.

**R3 — SC2 : sur-livraison quand un receveur a plusieurs voisins en surplus.**
Défaut de physique que j'ai trouvé en sondant hors du périmètre de la rubrique,
donc sans conséquence sur le verdict. Chaque source calcule la part qu'elle
accorde en fonction du besoin du receveur, mais aucun plafond global n'est
appliqué **du côté du receveur**. Ma sonde : une cellule dont le besoin est de
`200.0` kg, adjacente à deux cellules en surplus, reçoit `400.0` kg — le besoin
entier, deux fois. La conservation de la masse tient, SC1, SC2 et SC5 tiennent,
et rien ne nourrit deux fois ; mais le monde livre plus que nécessaire, et le
compteur `kg_transportes_monde_reel_re` inclut ce surplus de transport. Sur le
monde réel — 596 cellules, `1364` arêtes — beaucoup de cellules ont plusieurs
voisins, donc l'effet n'est pas anecdotique. **Matière pour un prochain audit**,
pas pour ce lot.

**R4 — SC4 : le déficit ne retombe jamais exactement à zéro.**
La récupération graduelle multiplie le déficit par un facteur strictement
inférieur à `1`, sans seuil de coupure. Une cellule ayant connu une seule fois
la famine garde donc, indéfiniment, un déficit infinitésimal mais strictement
positif. Conséquence : le maillon mortalité s'exécute pour toujours sur ces
cellules — sans tuer personne, le taux étant nul — et l'état « cellule sans
aucun déficit » devient inatteignable une fois quitté. Ce n'est ni un plancher
déguisé ni un dépassement de plafond, et cela ne contredit aucune condition.
Mais c'est un piège pour tout compteur futur qui compterait « les cellules en
déficit » : il en comptera presque toutes, pour toujours. Un seuil de coupure
explicite, documenté, serait plus sain.

**R5 — SC5 : la topologie du test ne peut pas exhiber le double comptage.**
Le test livré a bien trois cellules et deux arêtes actives, ce que la rubrique
exigeait littéralement. Mais les deux arêtes partent de la **même** source :
dans cette forme en étoile, un kilogramme ne peut pas franchir deux arêtes,
quelle que soit l'implémentation. J'ai vérifié la conséquence : ce test reste
**vert** même avec l'ancien maillon du lot `012`. La substance de SC5 est
néanmoins établie — je l'ai vérifiée moi-même sur une chaîne et sur le monde
réel à `200` ticks, où l'écart est nul contre environ `41` % pour l'ancien
maillon. Correctif attendu : faire porter le test sur une **chaîne**, la seule
topologie où un kilogramme pourrait franchir deux arêtes.

**R6 — SC6 : la quatrième condition n'est pas gagnée indépendamment.**
La condition « fraction de survie supérieure au seuil » n'est vraie que de
`0.0158`. Avec la marge d'avant recalibrage, le seuil aurait été `0.80` et cette
condition serait **fausse**. SC6 est donc PASS sur le plan de la mesure — les
quatre valeurs sont exactes et reproductibles — mais sa quatrième condition est
suspendue à la décision de SC3. Si le Générateur corrige SC3 en justifiant une
marge indépendamment de la mesure, il devra assumer le résultat, y compris s'il
est défavorable : le brief dit explicitement qu'une fraction hors fenêtre « est
une information sur le monde simulé, pas une impossibilité ».

**R7 — traçabilité de deux compteurs booléens.**
Deux compteurs du manifeste ne portent pas le nombre que leur commande imprime.
`fraction_dans_marge_predite` vaut `1` au manifeste, alors que la sortie imprime
le mot « True ». `deficit_non_efface_en_1_tick` vaut `9000.0` au manifeste,
alors que la ligne qui porte ce nom dans la sortie imprime « True » — la valeur
`9000.0` y figure bien, mais sous un autre nom. Les deux valeurs sont vérifiées
et correctes, l'intention est claire, et le brief demandait bien un résiduel
pour le second. C'est une friction de forme, pas un chiffre faux : que la
commande imprime exactement le jeton déclaré, sous le nom déclaré.

**R8 — le journal décrit un état du gate qui n'existe plus.**
Détaillé plus haut au § Périmètre jugé. Le journal annonce trois contrôles en
échec ; sur le commit jugé il n'y en a que deux, l'amendement de forme ayant
supprimé le troisième. Ce n'est pas imputable au Générateur, dont le constat
était exact au moment où il l'a écrit. Signalé pour que le journal soit remis à
jour à la prochaine itération, afin qu'un lecteur ultérieur ne cherche pas un
échec disparu.

---

## Feedback for Next Iteration

Le détail actionnable, point par point, avec pour chacun le correctif attendu et
ce qu'il ne faut surtout pas faire, est dans
`feedback/feedback-001.md`. En résumé : **un seul point bloque**, SC3, et il ne
se corrige pas en changeant un nombre — il se corrige en changeant la
chronologie et en disant la vérité sur la provenance de la valeur.

---

## Gate mécanique, second passage

Rejoué après l'écriture de ce verdict et du fichier de retour. Sortie complète :
`/tmp/eval-013/logs/gate_post_verdict.txt`. Résultat : les dix contrôles au
vert, `VERDICT: ACCEPT`, code de sortie `0`.

Ce que cela signifie, et surtout ce que cela ne signifie pas : la **forme** du
lot est désormais conforme — les fichiers déclarés existent et sont suivis, les
paires de preuves diffèrent, aucun échantillon n'est vide, le producteur n'est
pas le juge, la rubrique précède les livrables, tout nombre cité ici trace à un
compteur du manifeste. La **substance**, elle, reste jugée `REJECT` pour la
raison exposée au § SC3. Le gate et l'Évaluateur ne répondent pas à la même
question.

---

**Celui qui produit ne prononce pas la recevabilité.**
