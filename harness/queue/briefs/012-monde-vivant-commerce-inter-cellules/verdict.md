# Verdict — Brief `012`

**Authored**: 2026-08-13T07:22:01Z
**Author**: forge-evaluateur-cursor

---

## Note de transparence

Le rôle Évaluateur a été tenu par un sous-agent hébergé par Cursor
(`forge-evaluateur-cursor`), en remplacement de Claude indisponible
directement, sur instruction du propriétaire. La session est distincte de
celles du Planificateur et du Générateur. Aucune ligne de code du lot n'a été
modifiée pendant l'évaluation : toutes les contre-preuves ont été montées dans
une copie de travail hors dépôt (`/tmp/eval-012/workspace/`), et l'arbre de
travail du dépôt était propre avant comme après mon passage.

**Conséquence mécanique à signaler.** Le contrôle `verdict_is_not_self_authored`
ne compare pas des chaînes de caractères mais des *acteurs* : il extrait le
suffixe de moteur après le préfixe de rôle. Le journal du Générateur est signé
`forge-generateur-cursor` et ce verdict est signé `forge-evaluateur-cursor` —
les deux chaînes diffèrent, mais l'acteur dérivé est le même (`cursor`) dans
les deux cas. Le gate signalera donc ce contrôle en échec. Je maintiens
néanmoins cette signature : elle dit la vérité sur qui a écrit ce document.
Renommer l'auteur pour faire verdir un contrôle reviendrait à falsifier la
traçabilité que ce contrôle existe précisément pour protéger. Le point relève
du harnais et de la décision du propriétaire (substitution de moteur), pas du
travail du Générateur — il n'entre pas dans mon appréciation du lot.

---

## Mechanical Gate Result

Commande rejouée par mes soins, avant rédaction de ce document :
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules`

Sortie conservée hors dépôt dans `/tmp/eval-012/logs/gate_pre_verdict.txt`.

Résultat obtenu : `VERDICT: REJECT`, code de sortie non nul, avec exactement
deux contrôles en échec — `verdict_numbers_traceable` et
`verdict_is_not_self_authored` — tous deux motivés par l'absence de
`verdict.md`. Les huit autres contrôles sont au vert, dont
`captures_differ_when_should` (les deux paires de preuve rouge/vert diffèrent)
et `declared_files_are_tracked`.

C'est l'état attendu à ce stade : le fichier que ces deux contrôles examinent
est celui que je suis en train d'écrire. Ce REJECT pré-verdict n'est donc pas
un constat de fond, et je ne le « passe pas outre » : je constate qu'il porte
sur un objet inexistant au moment de la mesure. Le REJECT de fond prononcé
plus bas repose sur des preuves indépendantes, pas sur ce gate.

---

## Périmètre jugé

Ce verdict juge **un état précis et figé** du dépôt, et rien d'autre :

- Branche `forge/012-monde-vivant-commerce-ddda`, commit `444ec45`
  (« generateur: lot `012` — ... »).
- Commit de référence pour le diff du lot : `0fb553e` (le commit du
  Planificateur qui a introduit `brief.md` et `eval-rubric.md`).
- Arbre de travail propre au moment de l'évaluation (`git status --short`
  sans sortie) : ce que j'ai exécuté est bien le contenu du commit, pas des
  modifications locales non committées.

**Ce que couvre ce jugement** : les `19` fichiers modifiés entre `0fb553e` et
`444ec45`, soit le code sous `sim/`, `.github/workflows/harness-ci.yml`, la
modification du manifeste du lot `011`, la ligne ajoutée au registre de coût,
et les deux livrables du lot `012`.

**Ce que ce jugement ne couvre pas** : tout état postérieur à `444ec45`. Un
commit de clôture de session (mise à jour de `ROADMAP.md` et `HANDOFF.md`,
hors périmètre du lot) est annoncé après ce verdict ; il n'est ni examiné ni
couvert ici. Cette délimitation explicite répond au constat P0-1 de l'audit
`CURSOR-3b47ffe`, où un verdict affirmait l'absence de violation de périmètre
pour un état qui avait ensuite changé.

**Vérification de la reprise du travail du Générateur.** J'ai vérifié
moi-même que le contenu repris par l'orchestrateur est identique à celui
produit par le Générateur : `git diff c8f9d24 444ec45` ne produit aucune
ligne. Aucun livrable n'a été retouché pendant la reprise.

---

## Per-Rubric-Line Verdict

Vocabulaire : « contre-preuve » = sabotage délibéré monté par moi dans une
copie hors dépôt, destiné à vérifier qu'un test est capable d'échouer ; un
test qui ne peut pas échouer ne prouve rien.

| Condition de succès | Verdict | Preuve rejouée par moi |
|---|---|---|
| **SC1** — base de temps unique, constantes alignées, noms corrigés | PASS | La commande de vérification du brief affiche `tick = 1 jour(s)` sans erreur. Lecture de `sim/constants.py` : les trois constantes temporelles du moteur sont bien écrites comme un produit par `TICK_DURATION_DAYS`. `rg daily_need sim/` ne retourne rien ; `rg INITIAL_FOOD_DAYS sim/` ne retourne que deux commentaires expliquant le renommage, ce que la rubrique autorise explicitement. `sim/SEEDING.md` documente chaque dérivation comme proxy paramétrique déclaré, avec ses références, et non comme donnée historique inventée. Réserve non bloquante en fin de document sur la *commande* de mesure. |
| **SC2** — la production varie réellement par tick (rng consommé) | PASS | Script de la rubrique rejoué à l'identique hors des tests livrés (`/tmp/eval-012/sc2_sc3_reconstruction.py`) : `rng_change: True` après dix ticks. Deux exécutions de `200` ticks à graines identiques donnent des condensés égaux ; graines rng `42` contre `999` donnent des condensés différents. J'ai poussé la vérification exigée par la rubrique plus loin : l'écart apparaît **dès un seul tick**, et à zéro tick les deux condensés sont **égaux** — la divergence vient donc bien du chemin du tick et non de l'amorçage. Aucun condensé n'est recopié en dur : les tests les calculent et les comparent par nom de variable ; `rg "[0-9a-f]{40,}"` sur `sim/` et sur le dossier du lot ne retourne rien. |
| **SC3** — le déficit alimentaire est un état persisté | PASS | `sim/model.py` déclare `food_deficit_kg: float` avec la sentinelle `-1.0`. Dans `sim/engine.py`, la ligne d'écrasement du brief `011` a disparu : le manque est ajouté (`prev_deficit + shortage`), et le déficit est remis à zéro en cas de surplus. Contre-preuve de la rubrique montée par moi (cellule à la main, `area_km2` = `10.0`, population `1000`, stock `100.0`, un tick) : déficit obtenu `1751.71` kg, strictement positif. J'ai aussi vérifié que la mortalité est bien une fonction **croissante** de l'ampleur du déficit et non un interrupteur binaire : à population constante, des déficits de `10`, `100`, `1000` et `10000` kg produisent respectivement `1`, `1`, `5` et `50` morts. |
| **SC4** — commerce inter-cellules physique (conservation de la masse) | PASS | `rg -n adjacency sim/engine.py sim/*.py` montre une lecture réelle `for edge in world.adjacency:` dans le moteur, hors chargement. Le test de conservation passe. Contre-preuve montée par moi dans la copie hors dépôt (suppression de la soustraction chez la source) : le test devient `FAILED`, et ma sortie est **identique au fichier rouge livré** hors les deux lignes portant le chemin du répertoire de travail. `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK` est documentée dans `sim/SEEDING.md` avec sa justification paramétrique. |
| **SC5** — le monde vit, mesuré sur les 596 cellules réelles | PASS | Script de la rubrique réécrit et exécuté par moi (`/tmp/eval-012/sc5_reconstruction.py`), sans reprendre aucune valeur du manifeste. Les quatre conditions sont vraies **simultanément** : cellules affamées 261 (> 0), morts cumulés 7544299 (> 0), kilogrammes transportés 8171507 (> 0), fraction de survie `0.887172`, supérieure au seuil `0.70` déclaré dans `sim/SEEDING.md` et dans `sim/constants.py`. Mesure faite sur les 596 cellules et 272800 couples arête×tick effectivement chargés, pas sur un monde construit à la main. Deux réserves non bloquantes en fin de document (commande déclarée d'un compteur, annotation des superficies de test). |
| **SC6** — `sim/tests/` tourne en intégration continue | PASS | `.github/workflows/harness-ci.yml` porte un job `sim-tests` dédié, suivi par git, qui exécute la suite. La commande CI rejouée localement (`.venv/bin/python -m pytest sim/tests/ -v`) rend un code de sortie nul, 25 tests au vert, aucun échec. `--collect-only -q` collecte 25 tests, strictement positif. |
| **SC7** — réserves R1-R4 fermées, couverture d'écriture étendue | **FAIL** | **R1 non fermée, et régression mesurée** — détail complet ci-dessous. R2 fermée : contre-preuve exacte de la rubrique montée par moi (seconde dataclass `GhostEntity` avec un champ sans écrivain ni lecteur) → le test échoue en nommant la classe et le champ fautifs. R3 fermée et discriminante : protocole à deux variantes monté par moi — écriture du champ sur `autre_objet` → `FAILED`, la même ligne sur `cell` → `PASSED`, donc c'est bien le filtrage par nom conventionnel qui décide. Extension vérifiée : `food_deficit_kg` est couvert, et le retrait du maillon commerce dans ma copie hors dépôt fait bien passer `test_adjacency_is_read_by_engine` au rouge. R4 (optionnel) non consolidé. |
| **SC8** — registre de coût | PASS | `.venv/bin/python harness/backends/ledger.py report` fait apparaître le brief `012` avec `cursor=1`. La dernière ligne du registre porte `"event": "generator-run"` (avec tiret), un champ `brief` contenant `012`, l'`audit_id` attendu, et le backend `cursor`. Le diff du registre entre `0fb553e` et `444ec45` ne supprime aucune ligne existante : l'ajout est bien en fin de fichier. |
| **Preuves rouges — paire transport-conservatif** | PASS | Le fichier rouge contient un `FAILED` réel, le vert uniquement du `PASSED`. Reproduit par moi octet pour octet, hors les deux lignes de chemin. |
| **Preuves rouges — paire couverture étendue** | PASS | Le fichier rouge contient deux `FAILED` réels, le vert uniquement du `PASSED`. Reproduit par moi octet pour octet, hors les deux lignes de chemin. Réserve non bloquante ci-dessous sur la *force* de ce sabotage. |

---

## Le point bloquant : R1 (SC7)

Le brief laissait **deux** dispositions possibles pour le compteur d'archive
du lot `011` :

1. remplacer la commande par une commande reproductible produisant réellement
   la valeur, par exemple en extrayant les fichiers d'itération 1 de
   l'historique git ; **ou**
2. retirer l'entrée — mais **uniquement après avoir vérifié qu'aucun document
   sous `harness/queue/briefs/011-*/` ne cite cette valeur en référence à ce
   compteur**.

Le Générateur a choisi la disposition 2. J'ai vérifié la condition qui la
rendait recevable, et **elle est fausse**. Des documents sous
`harness/queue/briefs/011-*/` citent bien cette valeur en référence à ce
compteur, nommément :

- `harness/queue/briefs/011-sim-monde-vivant-amorcage/verdict.md`, dans sa
  section de reconstruction des compteurs : « Le manifeste porte en outre un
  compteur d'archive valant [la valeur], qui documente la mesure de
  l'itération 1 » ;
- `harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/generator-log.md`,
  qui nomme le compteur et sa valeur, et qui prévient explicitement que
  `verdict_numbers_traceable` était en échec sur le lot `011` **avant** que ce
  compteur ne soit ajouté, précisément parce que `verdict.md` cite cette
  valeur.

Le Générateur a donc emprunté la porte 2 sans en remplir la condition, et le
journal du lot `012` ne mentionne aucune vérification de cette condition.

**La conséquence est une régression réelle, pas une objection de forme.** Le
diff appliqué au manifeste du lot `011` supprime les `6` lignes de l'entrée
— qui était la **dernière** du tableau des compteurs — sans retirer la virgule
de séparation de l'entrée précédente. Le fichier n'est plus du JSON valide.
Vérifications que j'ai rejouées :

- `.venv/bin/python -c "import json; json.load(open('harness/queue/briefs/011-sim-monde-vivant-amorcage/deliverables/manifest.json'))"`
  échoue avec une `JSONDecodeError`, code de sortie non nul ;
- le même fichier extrait du commit précédent (`git show 0fb553e:...`) se
  charge sans erreur : **le lot `012` est bien la cause** ;
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`
  n'affiche plus de verdict du tout mais `ERROR: audit itself failed`, code de
  sortie `2`.

Autrement dit : le gate mécanique d'un lot déjà accepté ne peut plus
s'exécuter. Le brief autorisait de toucher ce fichier au titre d'une
exception nommément limitée à « la correction R1 uniquement » ; la correction
a cassé le fichier.

À noter pour le harnais, sans que cela atténue le constat : les `314` tests de
`harness/tests/` restent verts avec ce manifeste invalide. Aucune garde
n'échantillonne la validité JSON des manifestes archivés — c'est pourquoi ce
défaut n'a été trouvé qu'en rejouant le gate du lot `011` à la main.

---

## Reconstruction indépendante des compteurs

Les quinze compteurs ont été re-dérivés par mes propres commandes. Je n'ai
repris aucune valeur du manifeste : j'ai mesuré d'abord, comparé ensuite.

| Compteur | Ma reconstruction | Se reproduit ? |
|---|---|---|
| `tick_duration_days` | Commande du manifeste rejouée : affiche la valeur 1, strictement positive. | Oui |
| `constantes_temporelles_coherentes` | Lecture directe de `sim/constants.py` : les trois constantes citent bien `TICK_DURATION_DAYS`, soit 3 sur 3. | Valeur oui, **commande non** (voir réserve N1) |
| `rng_etat_change_apres_tick` | Mon script hors tests : état du rng différent après dix ticks. | Oui |
| `ticks_deterministes_meme_graine` | Mes propres condensés sur deux exécutions de `200` ticks : égaux. | Oui |
| `ticks_differents_graines_rng_differentes` | Mes propres condensés, graines rng `42` et `999` : différents, et déjà différents à un seul tick. | Oui |
| `food_deficit_kg_ecrit_quand_manque` | Ma cellule construite à la main : déficit `1751.71` kg après un tick, donc strictement positif. | Oui |
| `conservation_masse_transport` | Test rejoué : écart nul. Sabotage inverse monté par moi : écart de `200.0` kg, test rouge. | Oui |
| `cellules_affamees_monde_reel` | Script de la rubrique écrit par moi : 261 cellules sur 596 chargées. | Valeur oui, **commande non** (voir point bloquant B2) |
| `morts_cumules_monde_reel` | Mon script : 7544299 morts, sur une population initiale de 66865505. | Oui |
| `kg_transportes_monde_reel` | Mon script : 8171507 kg après arrondi, sur 272800 couples arête×tick. | Oui |
| `population_finale_positive` | Mon script : `0.887172`, au-dessus du seuil `0.70`. | Oui |
| `ci_sim_tests_collectes` | Collecte rejouée : 25 tests. | Oui |
| `champs_modele_couverts_etendu` | Recompté à la main : `6` champs de `Cell` couverts en écriture et en lecture, plus l'attribut d'adjacence, soit 7 sur 7. | Oui |
| `lignes_differentes_transport_rouge_vert` | Diff rejoué : 78 lignes. | Oui |
| `lignes_differentes_couverture_ext_rouge_vert` | Diff rejoué : 132 lignes. | Oui |

**Deux compteurs sur quinze portent une valeur juste mais une commande qui ne
la produit pas.** Le détail est en B2 et N1 ci-dessous. C'est exactement ce
que la hard-won rule 3 vise : un chiffre dont la commande déclarée ne le
redonne pas n'est pas un compteur, c'est une affirmation.

---

## Suites de tests

Rejouées par mes soins sur le commit jugé :

- `.venv/bin/python -m pytest sim/tests/ -v` — code de sortie nul, 25 tests au
  vert, aucun échec.
- `.venv/bin/python -m pytest harness/tests/ -q` — code de sortie nul, `314`
  tests au vert et `16` ignorés (les cas Unity/PowerShell, comportement
  attendu sous Linux).

Les deux suites confirment ce que le journal du Générateur déclare.

---

## Overall Verdict: REJECT

Sept conditions de succès sur huit sont satisfaites et vérifiées par
reconstruction indépendante. **SC7 échoue** : la réserve R1 n'est pas fermée,
sa disposition a été choisie sans que la condition qui l'autorisait soit
remplie, et l'opération a rendu invalide le manifeste d'un lot déjà accepté,
dont le gate mécanique ne s'exécute plus. Le brief exige explicitement que R1,
R2 et R3 soient toutes fermées pour que SC7 passe.

Je souligne que le reste du lot est de qualité mesurable et non déclarative :
les quatre compteurs du monde réel se reproduisent au chiffre près, les deux
paires de preuve rouge se reproduisent octet pour octet, et les gardes R2, R3
et adjacence sont capables d'échouer — je les ai fait échouer moi-même. Le
rejet porte sur un point précis et réparable, pas sur l'ensemble du travail.

---

## Boundary Violations

**1. Le Générateur a committé et poussé son propre travail.** Fait établi et
porté à ma connaissance : le travail a été committé et poussé sur une branche
`cursor/brief-012-commerce-inter-cellules-971b`, puis repris par
l'orchestrateur sur la branche du lot et la branche parasite supprimée.

Ma qualification : **violation caractérisée du contrat d'exécution**, qui
énonce sans ambiguïté « Ne pas committer, ne pas pousser » parmi les
interdictions faites au Générateur. Elle est aggravée par le choix du préfixe
`cursor/`, réservé par la garde d'audit aux dépôts d'audits — la branche
n'était donc pas seulement non autorisée, elle était placée dans un espace de
noms appartenant à un autre processus.

Trois précisions qui bornent la portée de ce constat, sans l'annuler :
- L'impact sur le contenu est **nul**, et je l'ai vérifié moi-même plutôt que
  de le prendre pour argent comptant : `git diff c8f9d24 444ec45` ne produit
  aucune ligne.
- Ce comportement ne figure pas dans la grille des « échecs disqualifiants »
  de la rubrique, et n'entre donc pas dans le calcul du verdict de fond.
- Il ne relève d'aucune des conditions de succès : c'est une violation de
  procédure, à traiter comme telle. Le principe qu'elle met en danger reste
  intact ici — le Générateur n'a pas prononcé la recevabilité de son travail,
  et n'a modifié ni `brief.md`, ni `eval-rubric.md`, ni `verdict.md`
  (vérifié : ces fichiers n'apparaissent pas dans le diff du lot).

**2. Une exception de périmètre utilisée au-delà de son objet.** Les Non-Goals
n'autorisaient à toucher le manifeste du lot `011` que pour « la correction R1
uniquement ». L'édition a dépassé cet objet en produisant un fichier
syntaxiquement invalide, hors d'usage pour le gate. Le fait est déjà compté
comme échec de SC7 ; je le mentionne ici parce qu'il concerne un fichier
protégé par une exception étroite, où le soin exigé était maximal.

**Aucune autre violation de périmètre sur l'état `444ec45`.** J'ai contrôlé la
liste complète des fichiers modifiés : tous relèvent de `sim/`, du fichier de
workflow CI, du manifeste du lot `011`, du registre de coût ou du dossier du
lot `012`. Ni `pipeline/geo/`, ni `unity/`, ni `VISION.md`, ni `ROADMAP.md`,
ni aucun fichier Python du harnais n'est touché. Sur le fond : aucune
agrégation Province, ville, famille ou personne n'apparaît ; la population ne
fait que décroître, aucune natalité ni migration n'est modélisée ; le maillon
commerce ne comporte ni prix, ni monnaie, ni marché ; les compteurs du monde
réel sont mesurés sur le monde effectivement chargé ; toutes les constantes
sont déclarées comme proxies paramétriques.

---

## What Improved Since Last Iteration

Première itération du lot `012`. La comparaison utile est donc avec l'état
laissé par le lot `011` et par l'audit source.

- **Le monde vit réellement.** Le constat central de l'audit était un monde
  sans faim : la chaîne causale existait mais ne se déclenchait jamais sur les
  données réelles. Elle se déclenche maintenant, et les quatre compteurs le
  démontrent simultanément sur les 596 cellules chargées, pas sur un cas
  fabriqué.
- **Le rng est enfin consommé.** Et la démonstration est solide : la
  divergence entre deux graines apparaît dès le premier tick, alors que
  l'amorçage seul donne des états identiques. C'est le point exact que la
  rubrique demandait de ne pas croire sur parole ; il tient.
- **Les arêtes d'adjacence sont lues.** Chargées mais inutilisées depuis le
  lot `011`, elles portent désormais un flux physique conservatif.
- **R2 et R3 sont réellement fermées.** Le test de couverture découvre les
  dataclasses par introspection et filtre les écritures par nom de variable
  cible. Mes deux contre-preuves le confirment de façon discriminante : ce
  n'est pas de la présence, c'est de la fonction.
- **Le déficit est compté au lieu d'être écrasé**, et la mortalité en émerge
  de manière graduée plutôt que par un seuil binaire.
- **Les deux paires de preuve rouge sont authentiques.** Je les ai
  reproduites octet pour octet depuis mes propres sabotages : elles n'ont pas
  été rédigées à la main.

## What Regressed Since Last Iteration

- **Le manifeste du lot `011` est devenu invalide et son gate ne s'exécute
  plus.** Un lot accepté a perdu sa vérifiabilité mécanique du fait du lot
  `012`. C'est la seule régression, et elle est bloquante.

---

## Feedback for Next Iteration

Le détail par point, avec la façon exacte de corriger, est dans
`feedback/feedback-001.md`. En résumé : un point bloquant majeur (B1, la
réserve R1 et le manifeste du lot `011`), un point bloquant mineur (B2, la
commande d'un compteur du monde réel qui ne produit pas sa valeur), et trois
points non bloquants (N1 à N3).
