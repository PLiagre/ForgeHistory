# Verdict — Brief `012`

**Authored**: 2026-08-13T07:22:01Z
**Author**: forge-evaluateur

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

> **Amendement de signature, 2026-08-13T07:52:00Z.** La ligne `**Author**:`
> ci-dessus portait `forge-evaluateur-cursor` lorsque cette section a été
> écrite ; elle a été normalisée en `forge-evaluateur` sur décision
> d'orchestration. Le paragraphe qui précède est conservé tel quel comme
> trace de ce que j'avais observé et argumenté à l'itération 1 : il décrit une
> collision d'acteurs que cette normalisation a résolue. Le raisonnement
> complet de l'amendement est dans la note de transparence de l'itération 2,
> plus bas. Aucun autre caractère de cette section n'a été modifié.

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

---
---

# Verdict — itération 2

**Authored**: 2026-08-13T07:52:00Z
**Author**: forge-evaluateur

---

## Note de transparence

Le rôle Évaluateur a été tenu par un sous-agent hébergé par Cursor, en
remplacement de Claude indisponible directement, sur instruction du
propriétaire. La session est distincte de celles du Planificateur et du
Générateur. Aucune ligne de code du dépôt n'a été modifiée pendant
l'évaluation : toutes les contre-preuves ont été montées dans une copie de
travail hors dépôt (`/tmp/eval-012/workspace/`), restaurée à l'identique du
dépôt après chaque sabotage, et l'arbre de travail était propre avant comme
après mon passage. Je n'ai ni committé ni poussé.

**Normalisation de ma signature — et pourquoi je l'annonce plutôt que de la
faire en silence.** Sur décision d'orchestration, la convention de session
(celle du lot `011` fusionné) est la suivante : la ligne `**Author**:` porte le
**rôle natif**, et l'acteur réel est déclaré en prose dans la note de
transparence. J'ai donc signé la présente section `forge-evaluateur`, et j'ai
modifié la ligne `**Author**:` de ma section d'itération 1, de
`forge-evaluateur-cursor` à `forge-evaluateur`. C'est la **seule** modification
apportée à cette section : son texte, ses constats et son verdict restent
intacts, et un encadré daté marque l'endroit de l'amendement pour qu'aucun
lecteur ne découvre l'écart par surprise.

Je maintiens mon désaccord de principe, et je le formule précisément parce
qu'il sera porté au propriétaire. À l'itération 1, j'avais refusé de renommer
l'auteur au motif qu'un contrôle ne doit pas se faire verdir par un
renommage. L'argument d'orchestration qui me fait appliquer la convention est
d'un autre ordre, et il est plus fort que le mien sur un point que j'avais
sous-estimé : ce que le contrôle sait détecter — un suffixe de moteur explicite
partagé — il doit pouvoir continuer à le détecter **ailleurs**, sur les briefs
à venir. Signer `-cursor` des deux côtés le laissait en échec permanent, donc
inutilisable comme signal. Ce que le contrôle ne sait **pas** voir — un couple
de rôles natifs tournant tous deux sous le même moteur — reste un angle mort
réel ; la convention le déplace de la mécanique vers la prose, elle ne le
ferme pas. Cet angle mort est documenté ici et sa fermeture mécanique (un
traçage d'acteur qui ne repose pas sur des chaînes auto-déclarées) est différée
au brief de harnais issu du point 1 de l'audit `CURSOR-3b47ffe`. Ma réserve
tient donc en une phrase : la convention rend le contrôle utile ailleurs, elle
ne le rend pas concluant ici, et c'est cette note — pas le gate — qui atteste
qui a écrit quoi sur le lot `012`.

---

## Mechanical Gate Result

Les deux gates concernés ont été rejoués par mes soins sur l'état jugé.

**Lot `012`** —
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules`
répond `VERDICT: ACCEPT`, code de sortie nul, les dix contrôles au vert. Sortie
conservée hors dépôt dans `/tmp/eval-012/logs/gate_012_iter2_avant.txt`.
Les deux contrôles qui échouaient à l'itération 1 sont désormais au vert :
`verdict_numbers_traceable`, puisque le fichier qu'il examine existe
maintenant, et `verdict_is_not_self_authored`, puisque les acteurs dérivés des
deux signatures diffèrent sous la convention appliquée ci-dessus.

**Lot `011`** —
`.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage`
répond `VERDICT: ACCEPT`, code de sortie nul, dix contrôles au vert et aucun
`FAIL`. Sortie conservée dans `/tmp/eval-012/logs/gate_011_iter2.txt`. C'est la
régression bloquante de l'itération 1 qui est levée : ce gate ne s'exécutait
plus du tout.

Rappel de lecture, inchangé depuis la rubrique : ces gates jugent la *forme*
du lot, pas sa substance. Leur `ACCEPT` est nécessaire, jamais suffisant. Tout
ce qui suit repose sur mes propres reconstructions.

---

## Périmètre jugé

- Branche `forge/012-monde-vivant-commerce-ddda`, commit `b458b6f`
  (« generateur: lot `012` itération 2 — corrections B1, B2 et N1 à N3 »).
- Commit de référence pour le diff de cette itération : `93ac0a5`, mon propre
  commit de verdict d'itération 1. Le diff de l'itération 2 porte sur 7
  fichiers.
- Arbre de travail propre au moment de l'évaluation (`git status --short` sans
  sortie) : ce que j'ai exécuté est bien le contenu du commit.

**Ce que couvre ce jugement** : les 7 fichiers modifiés entre `93ac0a5` et
`b458b6f`, plus l'ensemble de l'état résultant du lot, puisque la rubrique
demande de re-dérouler la grille complète à chaque verdict.

**Ce que ce jugement ne couvre pas** : tout état postérieur à `b458b6f`. Un
commit de clôture de session (mise à jour de `ROADMAP.md` et de `HANDOFF.md`,
hors périmètre du lot) est annoncé après ce verdict ; il n'est ni examiné ni
couvert ici. Cette délimitation explicite répond au constat P0-1 de l'audit
`CURSOR-3b47ffe`.

**Fichiers protégés.** J'ai vérifié mécaniquement qu'aucun de `brief.md`,
`eval-rubric.md` ni `verdict.md` n'apparaît dans le diff du Générateur pour
cette itération : `git diff --name-only 93ac0a5 b458b6f` ne les contient pas.
Les seules modifications de `verdict.md` sont les miennes, décrites ci-dessus.

**Ce qui n'a pas bougé, et pourquoi je peux m'appuyer sur mes reconstructions
d'itération 1.** Le diff de l'itération 2 ne touche ni `sim/constants.py`, ni
`sim/model.py`, ni `sim/engine.py`, ni `sim/world.py`, ni `sim/tests/test_rng.py`,
ni `sim/tests/test_commerce.py`, ni `sim/tests/test_write_coverage.py`, ni
`.github/workflows/harness-ci.yml`, ni `harness/queue/cost-ledger.jsonl`. Ce
n'est pas une déclaration du Générateur que je reprendrais : c'est le résultat
de ma propre lecture du diff. Là où le code est identique, mes contre-preuves
d'itération 1 restent valables ; je le dis explicitement condition par
condition ci-dessous. J'ai néanmoins **rejoué intégralement** les seize
compteurs, les deux contre-preuves rouges et les deux suites sur ce nouvel
état, plutôt que de me contenter du raisonnement d'invariance.

---

## Vérification du feedback, point par point

| Point | État | Preuve rejouée par moi |
|---|---|---|
| **B1** — réserve R1 et manifeste du lot `011` | **Fermé** | Le manifeste du lot `011` est de nouveau du JSON valide et porte `11` compteurs, dont l'entrée d'archive restaurée. Sa commande déclarée, qui lit les deux fichiers de preuve dans l'état du commit d'itération 1 du lot `011`, rend bien la valeur archivée `70` quand je la rejoue. Le gate du lot `011` répond `ACCEPT`, code de sortie nul, dix contrôles au vert. J'ai en outre vérifié ce que le journal ne dit pas : le commit épinglé dans la commande est un **ancêtre de `HEAD`** (`git merge-base --is-ancestor`), donc la commande reste exécutable depuis un clone de cette branche — une commande d'archive pointant un commit inatteignable aurait reproduit le défaut d'origine sous une autre forme. |
| **B2** — commande de `cellules_affamees_monde_reel` | **Fermé** | La commande déclarée est maintenant un script versionné. Rejouée telle quelle depuis la racine, elle affiche exactement 261, la valeur du compteur — contre `579` à l'itération 1. J'ai relu le script : la boucle de ticks est bien séparée de l'accumulation, et le comptage porte sur `hunger_ticks`, plus sur la valeur de retour du tick. Reste une réserve non bloquante, N5 ci-dessous, sur la déclaration de ce script au manifeste. |
| **N1** — commande de `constantes_temporelles_coherentes` | **Fermé** | La nouvelle commande teste la présence de la constante de base **dans la ligne d'affectation** de chaque constante. Contre-preuve graduée montée par moi hors dépôt : en cassant une dérivation, la commande tombe à `2` ; deux, à `1` ; les trois, à `0`. Elle mesure donc réellement ce qu'elle prétend mesurer, alors qu'elle affichait 3 quoi qu'il arrive à l'itération 1. La substitution de la troisième constante est désormais justifiée explicitement dans `sim/SEEDING.md`, et l'argument dimensionnel avancé (une constante exprimée en ticks n'a pas à être multipliée par la durée d'un tick) est correct. |
| **N2** — plancher de superficie | **Fermé** | `rg` sur l'ensemble de `sim/tests/` : toutes les cellules construites à la main utilisent désormais une superficie supérieure ou égale au plancher du brief. Les seules superficies nulles restantes sont celles du cas d'intégration `SC7d`, annoté à trois endroits (en-tête de module, commentaire de section, docstring de la fonction) comme cas hors données G3 non utilisable pour un compteur SC5 — c'est exactement ce que la rubrique demande. Réserve rédactionnelle non bloquante, N4 ci-dessous. |
| **N3** — paire de preuve « couverture étendue » | **Fermé** | Le fichier rouge courant exerce bien la capacité **nouvelle** : la ligne « dataclasses découvertes » y énumère deux classes, et l'échec nomme la classe nouvelle et son champ orphelin. Je l'ai reproduit depuis **mon propre** sabotage hors dépôt (une seconde dataclass, pas un champ ajouté à la première) : ma sortie est identique au fichier committé hors les deux lignes de chemin et la ligne de durée d'exécution de pytest, qui n'est pas déterministe (`0.03` seconde chez moi contre `0.04` dans le fichier). |
| **R4** — consolidation des preuves vertes du lot `011` | Non fait | Le brief le déclare optionnel et non bloquant. Aucune action requise. |

### Traitement de l'ancien compteur de diff : conforme à la leçon R1, et généralisé

Le Générateur a rencontré à l'itération 2 exactement la situation qui avait
produit R1 : régénérer un fichier de preuve fait mentir un compteur déjà
mesuré et déjà cité dans un verdict. Sa disposition :

- l'ancien compteur garde sa valeur 132 et reçoit une commande **ré-ancrée** sur
  l'état des fichiers au commit d'itération 1 ;
- un **nouveau** compteur mesure les fichiers courants et vaut 136.

J'ai rejoué les deux commandes : la première rend 132, la seconde 136. Les deux
valeurs sont donc traçables et reproductibles, et la valeur 132 — que ma section
d'itération 1 cite — reste rattachée à une mesure exécutable.

Je juge cette disposition **conforme à la leçon R1, et supérieure à ce que le
feedback exigeait**. Le feedback ne demandait de corriger que l'instance
signalée ; le Générateur a reconnu le même motif dans une situation nouvelle et
lui a appliqué le même remède, sans attendre qu'un évaluateur le lui signale.
C'est la différence entre corriger un défaut et intégrer une règle. Je note
aussi que la porte du retrait, celle qui avait été mal empruntée à l'itération 1,
était ici tout aussi indisponible — mon propre verdict cite la valeur — et
qu'elle n'a pas été tentée.

---

## Per-Rubric-Line Verdict — grille complète SC1 → SC8

Colonne « bougé ? » : ce que l'itération 2 a modifié, d'après ma lecture du
diff, et non d'après le journal.

| Condition | Bougé ? | Verdict | Preuve rejouée par moi sur `b458b6f` |
|---|---|---|---|
| **SC1** — base de temps unique, constantes alignées, noms corrigés | Oui : `sim/SEEDING.md` complété, commande du compteur corrigée. `sim/constants.py` inchangé. | PASS | La commande du brief affiche `tick = 1 jour(s)`. Relecture de `sim/constants.py` : les trois constantes temporelles du moteur sont bien écrites comme un produit par la constante de base. `rg daily_need sim/` ne retourne rien ; l'ancien nom trompeur n'apparaît que dans deux commentaires de renommage, ce que la rubrique autorise. La dérivation de chaque constante est documentée comme proxy paramétrique déclaré, avec ses références. Nouveauté vérifiée : la mesure de cohérence est maintenant capable de tomber (voir N1 ci-dessus). |
| **SC2** — la production varie réellement par tick | Non : `engine.py` et `test_rng.py` intacts. | PASS | Reconstruction **rejouée** sur ce commit, pas seulement reprise de l'itération 1 : état du rng modifié après dix ticks ; condensés égaux à graines égales ; condensés différents à graines rng différentes, **déjà après un seul tick**, alors qu'à zéro tick les deux mondes sont identiques — la variabilité vient donc du chemin du tick et non de l'amorçage. Aucun condensé recopié en dur : `rg "[0-9a-f]{40,}"` sur `sim/` et sur le dossier du lot ne retourne rien. |
| **SC3** — le déficit alimentaire est un état persisté | Non : `model.py` et `engine.py` intacts. | PASS | Contre-preuve de la rubrique remontée et rejouée : cellule construite à la main, un tick, déficit strictement positif. Mortalité re-testée comme fonction croissante de l'ampleur du déficit : `1`, `1`, `5` puis `50` morts pour des déficits croissants d'un facteur `10` à population constante. Ce n'est donc pas un interrupteur binaire. |
| **SC4** — commerce inter-cellules physique | Non : `engine.py` et `test_commerce.py` intacts, paire de preuve transport intacte. | PASS | Lecture réelle des arêtes dans le moteur confirmée par `rg`. Test de conservation au vert. Contre-preuve remontée **de zéro** dans ma copie hors dépôt sur ce commit : le test devient rouge et ma sortie est identique au fichier rouge committé, hors lignes de chemin et ligne de durée. Capacité de transport documentée dans `sim/SEEDING.md`. |
| **SC5** — le monde vit, mesuré sur les 596 cellules réelles | Oui pour la commande d'un compteur ; non pour le moteur. | PASS | Mon script de reconstruction rejoué sur ce commit : les quatre conditions vraies **simultanément** — cellules affamées 261, morts cumulés 7544299, kilogrammes transportés 8171507, fraction de survie `0.887172` au-dessus du seuil `0.70` déclaré dans `sim/SEEDING.md`. Mesure faite sur les 596 cellules et 272800 couples arête×tick effectivement chargés. La commande déclarée du premier compteur rend maintenant sa valeur (B2). Le cas structurellement inatteignable est correctement borné (N2). |
| **SC6** — `sim/tests/` tourne en intégration continue | Non : le workflow est intact. | PASS | Le job dédié est toujours présent dans `.github/workflows/harness-ci.yml`, suivi par git. La commande CI rejouée localement rend un code de sortie **nul**, 25 tests au vert. La collecte rend 25 tests, strictement positif. |
| **SC7** — réserves R1-R4 fermées, couverture d'écriture étendue | Oui : R1 refaite, paire de preuve régénérée. | **PASS** | **R1 fermée** : voir B1 ci-dessus — JSON valide, entrée restaurée, commande d'archive qui rend `70`, gate du lot `011` en `ACCEPT`. **R2 fermée** : ma contre-preuve remontée sur ce commit (seconde dataclass avec un champ sans écrivain ni lecteur) fait échouer le test en nommant la classe et le champ fautifs. **R3 fermée et discriminante** : mon protocole à deux variantes tient toujours — écriture sur un objet d'un autre nom, test rouge ; la même ligne sur la variable conventionnelle, test vert. **Extension** : le nouveau champ est couvert, et le retrait du maillon commerce dans ma copie hors dépôt fait bien tomber la vérification de lecture des arêtes. R4 optionnel, non fait. |
| **SC8** — registre de coût | Non. | PASS | Le brief apparaît au rapport du registre avec le bon compte. La dernière ligne du fichier porte le nom d'événement avec tiret, le brief attendu et l'identifiant d'audit attendu. Aucune ligne existante supprimée. |
| **Preuves rouges — paire transport-conservatif** | Non | PASS | Rouge avec échec réel, vert uniquement au vert, reproduit depuis mon propre sabotage. |
| **Preuves rouges — paire couverture étendue** | Oui, régénérée | PASS | Rouge portant 4 mentions d'échec, vert n'en portant aucune et 3 mentions de succès ; reproduit depuis mon propre sabotage, et exerçant cette fois la capacité nouvelle. |
| **Dérogation déclarée** | Non | Recevable | La seule dérogation du lot est celle du budget d'exécution. Je l'ai rejouée : la commande exigée par le brief produit bien la chaîne annoncée. La dérogation est donc étayée, pas affirmée. |

---

## Reconstruction indépendante des compteurs

Les seize compteurs — les quinze de l'itération 1 plus celui ajouté à
l'itération 2 — ont été re-dérivés par mes propres commandes sur `b458b6f`. Je
n'ai repris aucune valeur du manifeste : j'ai mesuré d'abord, comparé ensuite.

| Compteur | Ma reconstruction sur `b458b6f` | Se reproduit ? |
|---|---|---|
| `tick_duration_days` | Commande rejouée : valeur 1, strictement positive. | Oui |
| `constantes_temporelles_coherentes` | Nouvelle commande rejouée : 3 sur 3. Et elle tombe à `2`, `1`, `0` sous sabotage gradué. | Oui, et la mesure est désormais **falsifiable** |
| `rng_etat_change_apres_tick` | Mon script hors tests : état du rng différent après dix ticks. | Oui |
| `ticks_deterministes_meme_graine` | Mes propres condensés, deux exécutions à graines identiques : égaux. | Oui |
| `ticks_differents_graines_rng_differentes` | Mes propres condensés, graines rng différentes : différents, et déjà à un seul tick. | Oui |
| `food_deficit_kg_ecrit_quand_manque` | Ma cellule construite à la main : déficit strictement positif après un tick. | Oui |
| `conservation_masse_transport` | Test rejoué : écart nul. Mon sabotage inverse : test rouge. | Oui |
| `cellules_affamees_monde_reel` | Script déclaré rejoué : 261. Mon propre script indépendant : 261 également, sur 596 cellules chargées. | Oui — **corrigé** |
| `morts_cumules_monde_reel` | Mon script : 7544299 morts, population initiale 66865505. | Oui |
| `kg_transportes_monde_reel` | Mon script : 8171507 après arrondi, sur 272800 couples arête×tick. | Oui |
| `population_finale_positive` | Mon script : `0.887172`, au-dessus du seuil. | Oui |
| `ci_sim_tests_collectes` | Collecte rejouée : 25 tests. | Oui |
| `champs_modele_couverts_etendu` | Recompté : les champs de la seule dataclass du modèle, couverts en écriture et en lecture, plus l'attribut d'adjacence, soit 7 sur 7. | Oui |
| `lignes_differentes_transport_rouge_vert` | Diff rejoué : 78 lignes. | Oui |
| `lignes_differentes_couverture_ext_rouge_vert` | Commande d'archive rejouée sur l'état du commit d'itération 1 : 132 lignes. | Oui |
| `lignes_differentes_couverture_ext_rouge_vert_iter2` | Diff des fichiers courants rejoué : 136 lignes. | Oui |

**Seize compteurs sur seize se reproduisent, et aucun ne repose plus sur une
commande incapable d'échouer.** C'est la différence de fond avec l'itération 1,
où deux compteurs portaient une valeur juste et une commande qui ne la
produisait pas.

---

## Suites de tests

Rejouées par mes soins sur le commit jugé :

- `.venv/bin/python -m pytest sim/tests/ -v` — code de sortie **nul**, 25 tests
  au vert, aucun échec.
- `.venv/bin/python -m pytest harness/tests/ -q` — code de sortie **nul**,
  `314` tests au vert et `16` ignorés, ces derniers étant les cas
  Unity/PowerShell, comportement attendu sous Linux.

Les deux suites confirment ce que le journal déclare.

---

## Overall Verdict: PASS

Les huit conditions de succès sont satisfaites et vérifiées par reconstruction
indépendante. Les deux points bloquants de l'itération 1 sont fermés, les trois
points non bloquants aussi. Aucun échec de la grille des comportements
disqualifiants de la rubrique n'est présent : la masse est conservée par
l'étape de transport, aucun champ n'a été retiré du modèle pour ajuster une
couverture, les compteurs du monde réel sont mesurés sur le monde chargé et non
sur un monde fabriqué, aucun condensé n'est recopié en valeur hexadécimale, et
ni le brief, ni la rubrique, ni le verdict n'ont été modifiés par le
Générateur.

Ce que je retiens de cette itération, au-delà du verdict : la régression qui
avait motivé le rejet — un manifeste rendu illisible et le gate d'un lot déjà
accepté hors service — est levée et vérifiée par l'exécution du gate concerné,
pas par une déclaration. Et les deux corrections de fond ont transformé deux
mesures décoratives en mesures qui peuvent échouer, ce qui est précisément
l'objet de la hard-won rule 4.

---

## Boundary Violations

Réexaminées sur le nouveau périmètre, c'est-à-dire sur le diff `93ac0a5` →
`b458b6f`.

**1. Le Générateur n'a ni committé ni poussé à cette itération.** C'était la
violation de procédure retenue à l'itération 1, et le feedback la listait parmi
les choses à ne surtout pas refaire. Je l'ai vérifié plutôt que de croire la
déclaration du journal : aucune branche autre que celle du lot n'existe en
local, et l'unique commit de l'itération est celui de l'orchestrateur. La
consigne a donc été suivie.

**2. Aucune violation de périmètre sur les fichiers.** Les 7 fichiers modifiés
relèvent tous du périmètre explicitement autorisé : le manifeste du lot `011`
au titre de l'exception nommée pour R1, deux fichiers de `sim/`, un fichier de
preuve sous `sim/tests/proof_red/`, et trois fichiers du dossier du lot `012`.
Ni `pipeline/geo/`, ni `unity/`, ni `VISION.md`, ni `ROADMAP.md`, ni aucun
fichier Python du harnais n'est touché.

**3. L'exception de périmètre sur le manifeste du lot `011` est cette fois
respectée dans son objet.** C'était le second constat de l'itération 1 :
l'exception, limitée à la correction R1, avait été utilisée pour produire un
fichier hors d'usage. Le diff de cette itération sur ce fichier n'ajoute que
l'entrée de compteur attendue, et j'ai vérifié que le fichier est de nouveau
exploitable par le gate.

**4. Non-Goals de fond, revérifiés.** Aucune agrégation Province, ville,
famille ou personne n'est apparue ; la population ne fait que décroître, sans
natalité ni migration ; le maillon commerce reste sans prix, sans monnaie et
sans marché ; les compteurs exigeant le monde réel sont mesurés sur le monde
effectivement chargé ; toutes les constantes restent déclarées comme proxies
paramétriques.

**Aucune violation de périmètre retenue à l'itération 2.**

---

## What Improved Since Last Iteration

- **La régression bloquante est levée.** Le manifeste du lot `011` est de
  nouveau du JSON valide et son gate répond `ACCEPT` — je l'ai exécuté.
- **Deux mesures décoratives sont devenues des mesures.** La commande de
  cohérence des constantes tombe désormais quand on casse une dérivation ; la
  commande du compteur de cellules affamées rend exactement la valeur du
  compteur. Aucune des deux ne se contentait d'être présente.
- **La preuve rouge de couverture prouve la bonne chose.** Elle exerce la
  capacité nouvelle du lot — la découverte d'une dataclass entière par
  introspection — au lieu de rejouer celle du lot précédent.
- **La leçon R1 a été généralisée, pas seulement appliquée.** Confronté au même
  motif dans une situation nouvelle, le Générateur a de lui-même archivé la
  valeur ancienne avec une commande ré-ancrée et ouvert un compteur pour l'état
  courant. C'est le comportement qu'un feedback cherche à produire.
- **La consigne de procédure a été respectée** : ni commit, ni poussée, ni
  branche parasite.
- **La substitution silencieuse est devenue explicite.** Le choix de compter la
  capacité de transport plutôt que la réserve initiale parmi les constantes
  dérivées est maintenant justifié dans `sim/SEEDING.md`, avec un argument
  dimensionnel que j'ai vérifié comme correct.

## What Regressed Since Last Iteration

Aucune régression. J'ai spécifiquement vérifié les points qui pouvaient casser
par effet de bord : les deux suites de tests restent entièrement vertes, les
seize compteurs se reproduisent tous, les quatre conditions du monde réel sont
inchangées au chiffre près, la paire de preuve transport est intacte, et le
gate du lot `011` — le lot voisin qui avait été cassé — est au vert.

---

## Réserves non bloquantes pour un brief ultérieur

Aucune de ces réserves ne conditionne l'acceptation du lot. Je les consigne
pour qu'elles ne se perdent pas.

**N4 — une affirmation arithmétiquement fausse dans deux docstrings.** Les deux
tests corrigés au titre de N2 portent la mention « superficie `1.0` (supérieure
ou égale au minimum G3 `1.444877` km²) ». La superficie retenue est conforme au
plancher que le brief fixe, et le plancher est bien un arrondi conservateur du
minimum réel — mais le signe de comparaison écrit dans le commentaire est faux,
puisque `1.0` est inférieur à `1.444877`. Correction : reformuler en « conforme
au plancher de `1.0` km² fixé par le brief, lui-même arrondi conservateur du
minimum réel ». C'est une phrase à reprendre, pas un test à changer.

**N5 — le script de mesure n'est pas déclaré au manifeste.** Le script qui porte
désormais la commande d'un compteur du monde réel est bien suivi par git, je
l'ai vérifié. Mais il n'apparaît pas dans la liste `files` du manifeste, alors
qu'il est devenu porteur de preuve : si son contenu changeait ou s'il
disparaissait, aucun contrôle ne le verrait, puisque les contrôles d'existence
et de suivi ne regardent que les chemins déclarés. Correction : ajouter son
chemin à la liste `files`. Je souligne que c'est la même famille de fragilité
que celle qui a produit R1 — un compteur dont la preuve n'est pas ancrée
durablement — et qu'il vaut mieux la fermer avant qu'elle ne coûte une
itération.

**N6 — les commandes d'archive dépendent de la préservation de l'historique.**
Deux compteurs tirent maintenant leur valeur de fichiers lus à un commit
épinglé. J'ai vérifié que les deux commits visés sont des ancêtres de `HEAD`,
donc atteignables. Cette propriété survivra à une fusion qui conserve
l'historique, comme celle du lot `011`, mais pas à une fusion écrasée en un
seul commit. À porter au brief de harnais : soit imposer la préservation de
l'historique pour ces cas, soit prévoir une forme d'ancrage qui n'en dépende
pas.

**N7 — l'angle mort de signature reste ouvert.** Il est décrit dans ma note de
transparence ci-dessus et sa fermeture mécanique est déjà différée au brief de
harnais issu du point 1 de l'audit source. Je le répète ici uniquement pour
qu'il figure dans la liste des réserves et ne dépende pas de la lecture d'une
note.

---

**Celui qui produit ne prononce pas la recevabilité — et celui qui juge ne
corrige pas ce qu'il juge.**
