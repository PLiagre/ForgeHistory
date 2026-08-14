# generator-log.md — Brief 017 : Le seuil de survie honnête

**Author**: forge-generateur

**Note de transparence (acteur réel)** : le rôle natif est `forge-generateur`.
L'acteur qui l'a exécuté est un sous-agent Cursor Cloud (Claude Opus 5),
orchestré depuis une session Grok 4.6 qui remplace le CTO Claude
(quota/plafond atteint). La signature reste celle du rôle, conformément au
contrat des trois rôles : le Générateur construit, l'Évaluateur juge, et ce
document ne prononce aucune recevabilité.

---

## 1. Vérifications préalables (avant tout travail de fond)

Pré-vol de taille du lot :

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/017-sim-seuil-survie-honnete \
  --estimated-calls 130
```

Sortie : `advisory : SIZE_OK (advisory -- the Planificateur decides)`.

Budget d'exécution :

```
.venv/bin/python harness/budget.py status \
  --brief harness/queue/briefs/017-sim-seuil-survie-honnete
```

Sortie :

```
status     : UNMEASURABLE
reason     : no agent transcript naming 017-sim-seuil-survie-honnete under /home/ubuntu/.claude/projects/-workspace
Nothing is being enforced. This is not OK -- it is unmeasured.
```

Le budget lit les transcriptions de session Claude Code, qui n'existent pas sur
cette machine (l'acteur est un agent Cursor Cloud). Une dérogation est portée
dans `manifest.json`, avec la commande et le message d'erreur exigés par le
brief. Le budget n'a donc pas été mesuré mécaniquement pendant ce lot ; il n'a
pas non plus été estimé de mémoire, ce qui serait une narration.

---

## 2. Ce qui a été fait

### 2.1 Récupération physique du déficit (SC5)

`sim/engine.py`, `_apply_consumption` : la formule
`food_deficit_kg × (1 − DEFICIT_RECOVERY_RATE_PER_TICK)` disparaît. Elle
effaçait 10 % de la dette quel que soit le surplus — un surplus d'un
nanogramme effaçait 1 000 kg sur une dette de 10 000 kg, des kilogrammes
disparaissant sans contrepartie physique.

Voie (a) du brief, ratio 1:1 :

- `remboursement = min(dette, surplus_du_tick × ratio)` ;
- les kilogrammes remboursés **quittent le stock** (`stock = surplus −
  remboursement`) : ils sont mangés en sus du besoin d'entretien ;
- le ratio est borné à 1 dans le moteur, donc la réduction de dette ne peut
  jamais dépasser le surplus physique du tick, même si la constante était
  modifiée en mémoire.

`DEFICIT_RECOVERY_RATE_PER_TICK` est supprimée ; son successeur nommé est
`DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG = 1.0` (kilogrammes de dette remboursés
par kilogramme de surplus). C'est ce successeur qui entre dans le modèle SC1.
Sémantique documentée dans `sim/SEEDING.md`, section « SC5 brief 017 ».

### 2.2 Critère de faim (SC4)

`_apply_consumption` retourne désormais la **pénurie du tick en kg** (0.0 s'il
n'y a pas eu de manque) et `_update_hunger(cell, penurie_kg)` n'incrémente
`hunger_ticks` que si cette pénurie est strictement positive. Le test
`food_stock_kg <= 0` a disparu du maillon faim : une cellule ravitaillée
exactement à sa ration termine le tick le garde-manger vide sans avoir manqué
de rien.

L'ordre du tick (production → commerce → consommation → faim → mortalité) est
inchangé ; `food_deficit_kg` n'est toujours pas touché par le commerce.

### 2.3 Accumulateur de mortalité fractionnaire (SC3)

Nouveau champ `Cell.mortality_remainder` (float, sentinelle `-1.0` = non
calculé). `_apply_mortality` calcule `raw = population × death_rate +
remainder`, applique `deaths = int(raw)` et persiste `raw − deaths`. Une
cellule de 5 habitants en famine totale produit 0.5 mort par tick : elle était
immortelle par arrondi, elle ne l'est plus.

Le champ est initialisé à `0.0` dans `World.from_g3` (monde amorcé : aucune
fraction en attente, ce qui est une mesure réelle et non un « non calculé »),
et il est ajouté à `World.to_dict()` pour que l'empreinte de déterminisme
couvre cet état.

### 2.4 Modèle de survie stationnaire (SC1) et sensibilité (SC2)

`sim/constants.py` reçoit un modèle en deux termes, entièrement dérivé des
constantes (détail complet dans `sim/SEEDING.md`, section « SC1 brief 017 ») :

1. **Dépassement déterministe.** Tant que la densité dépasse la capacité de
   charge, le couple (déficit, écart de densité) est un oscillateur de
   pulsation `sqrt(HUNGER_DEATH_SCALE × FOOD_CONSUMPTION)`. La densité au
   moment où le déficit revient à zéro vaut `2 × cap − d0`, soit 8 hab/km²
   pour 10 au départ : la population dépasse la capacité de charge **par le
   bas**, parce que la dette accumulée pendant la descente continue de tuer.
2. **Érosion stochastique.** À cette densité, un mauvais rendement crée encore
   un déficit dont l'espérance a une forme fermée ; ce déficit tue
   `min(HDS × déficit_par_tête, MAX_DEATH_RATE_PER_TICK)` de la population
   pendant sa durée de remboursement et sur l'échelle de temps du tampon
   alimentaire.

C'est ce second terme qui fait entrer `HUNGER_DEATH_SCALE` et
`MAX_DEATH_RATE_PER_TICK` dans la prédiction — le défaut central relevé par
les audits sources : l'ancienne garde certifiait la survie sans regarder ce
qui tue.

**Mécanisme du remplacement en mémoire (piège n° 5 du brief).** La prédiction
est le résultat d'une fonction, `compute_survie_fraction_predite_stationnaire()`,
qui relit les globales **courantes** du module à chaque appel ; la constante de
module `SURVIE_FRACTION_PREDITE_STATIONNAIRE` reste figée au chargement et
n'est donc pas utilisée par les tests de sensibilité. Symétriquement,
`sim/engine.py` lit `HUNGER_DEATH_SCALE` et `MAX_DEATH_RATE_PER_TICK` via le
module (`_constantes.HUNGER_DEATH_SCALE`) et non par valeur importée : sans
cela, remplacer la constante en mémoire aurait changé la prédiction sans
changer le comportement mesuré, et le test aurait comparé deux mondes
différents. `importlib.reload` n'est volontairement pas utilisé : il recharge
`sim.constants` sans recharger `sim.engine`, laissant moteur et prédiction sur
deux jeux de constantes.

**Conséquence pour qui veut re-dériver le signe à la main.** Un contrôle écrit
sous la forme « je remplace `HUNGER_DEATH_SCALE` en mémoire, puis
`importlib.reload(sim.constants)`, puis je relis la constante » ne peut pas
montrer la propriété, quelle que soit l'implémentation : le rechargement
ré-exécute le fichier source et remet `HUNGER_DEATH_SCALE` à sa valeur écrite,
donc la prédiction revient à sa valeur nominale. La vérification équivalente,
sans rechargement, est :

```
.venv/bin/python -c "
import sim.constants as C
base = C.compute_survie_fraction_predite_stationnaire()
C.HUNGER_DEATH_SCALE = C.HUNGER_DEATH_SCALE * 2
haut = C.compute_survie_fraction_predite_stationnaire()
print('HDS x2 -> prediction diminue ?', haut < base)
"
```

Sortie réelle : `HDS x2 -> prediction diminue ? True`. Le même schéma
(remplacement en mémoire, sans rechargement) est celui du test SC2, et celui du
sabotage de la paire rouge A.

### 2.5 Ordre de travail : documentation avant mesure

`sim/SEEDING.md` (sections SC1, SC2, SC3, SC4, SC5 du brief 017) a été rédigé
**avant** la première simulation du monde réel. Les tolérances sont des
expressions dérivées des constantes :

| constante | dérivation | valeur |
|---|---|---|
| `SURVIE_TOLERANCE_STATIONNAIRE` | dispersion relative du rendement × (cap / d0) × probabilité qu'un tick soit déficitaire | ≈ 0.1010 |
| `SURVIE_CONVERGENCE_DELTA` | dispersion relative du rendement × `MAX_DEATH_RATE_PER_TICK` | ≈ 0.0289 |
| `SURVIE_TOLERANCE_SENSIBILITE` | tolérance stationnaire + amplitude du dépassement initial | ≈ 0.2010 |
| `N_STAT_SURVIE` | `max(1000, ceil(période d'oscillation / MAX_DEATH_RATE_PER_TICK))` | 1000 |
| `N_BOUND_MORT` | `ceil(1 / MAX_DEATH_RATE_PER_TICK)` | 10 |

Aucune de ces expressions n'a été retouchée après avoir vu une valeur mesurée.
La seule chose vérifiée avant la mesure du monde réel est le **signe** de la
prédiction (propriétés exigées par le brief lui-même), sur la prédiction seule,
sans exécuter la simulation :

```
.venv/bin/python -c "import sim.constants as C; ..."
```

→ `HDS ×2` fait baisser la prédiction, `HDS ×0.5` la fait monter,
`DEFICIT_RECOVERY_RATE_PER_SURPLUS_KG ×2` la fait monter,
`FOOD_PRODUCTION ×2` la fait monter.

---

## 3. Comment chaque compteur a été mesuré

Chaque valeur du manifeste vient de la sortie réelle de la commande citée ici.
Aucune n'a été recopiée d'une estimation.

### SC1 — conformité stationnaire

```
.venv/bin/python -m pytest sim/tests/test_survie_stationnaire.py -q -s
```

Sortie réelle (extrait) :

```
N_STAT_SURVIE = 1000
SURVIE_FRACTION_PREDITE_STATIONNAIRE = 0.7965972222222222
SURVIE_TOLERANCE_STATIONNAIRE = 0.10103629710818451
SURVIE_CONVERGENCE_DELTA = 0.028867513459481294
cellules = 596
pop_init = 66865505, pop_fin = 49588760
s(N=1000) = 0.741619
s(N/2=500) = 0.744310
derive = 0.002691 (delta = 0.028868)
predite = 0.796597
ecart = 0.054978 (tolerance = 0.101036)
converge = True
dans_tolerance = True
fraction_survie_dans_tolerance_stationnaire = 1
```

→ `N_STAT_SURVIE`, `SURVIE_FRACTION_PREDITE_STATIONNAIRE`,
`SURVIE_TOLERANCE_STATIONNAIRE`, `SURVIE_CONVERGENCE_DELTA`,
`fraction_survie_dans_tolerance_stationnaire` (échantillon : les 596 cellules
chargées par G3).

### SC2 — sensibilité

```
.venv/bin/python -m pytest sim/tests/test_sensibilite_survie.py -q -s
```

Sortie réelle (extrait) :

```
HDS nominal = 0.005
regime x0.5 : mesure=0.768320 predite=0.798299
regime nominal  : mesure=0.757555 predite=0.796597
regime x2.0 : mesure=0.740239 predite=0.793194
ecarts = 0.029979, 0.039042, 0.052955 (tolerance = 0.201036)
sensibilite_hds_05_passe = 1
sensibilite_hds_2_passe = 1
DRR nominal = 1.0, predite = 0.796597
DRR x2.0 = 2.0, predite = 0.798299
sensibilite_drr_direction_passe = 1
```

Mesure et prédiction décroissent toutes deux quand la mortalité par faim
augmente, et chaque régime reste dans la tolérance de sensibilité.

### SC3 — accumulateur de mortalité

```
.venv/bin/python -m pytest sim/tests/test_mortalite_accumulateur.py -q -s
```

Sortie réelle (extrait) :

```
N_BOUND_MORT = 10 (= ceil(1 / 0.1))
deficit_kg = 1000.0
tick_du_premier_mort = 2
famine_tue_cellule_5hab = 1
pop_initiale=50: morts_appliques=50, somme_exacte=50.000000, ecart=0.000000, remainder_final=0.000000
pop_initiale=137: morts_appliques=137, somme_exacte=137.060000, ecart=0.060000, remainder_final=0.060000
pop_initiale=500: morts_appliques=500, somme_exacte=500.050000, ecart=0.050000, remainder_final=0.050000
mortalite_precision_n_ticks = 0.060000
```

L'écart maximal (0.06 mort sur 1 000 ticks et 3 cellules) est la fraction
encore en attente : c'est exactement ce que le report garantit.

### SC4 — critère de faim

```
.venv/bin/python -m pytest sim/tests/test_hunger_criterion.py -q -s
```

Sortie réelle (extrait) :

```
temoin    : stock=0.0, deficit=0.0, hunger_ticks=0
receveuse : stock=0.0, deficit=0.0, hunger_ticks=0
hunger_ticks_cellule_ravitaillee = 0
penurie_kg = 100.0, hunger_ticks = 1
```

La valeur 0 de `hunger_ticks_cellule_ravitaillee` est une **mesure réelle**, pas
un « non calculé » (hard-won rule 8) : c'est la propriété demandée par SC4. Le
second contrôle (`penurie_kg = 100.0 → hunger_ticks = 1`) évite un critère qui
n'incrémenterait jamais et passerait le premier test par vacuité.

### SC5 — récupération physique

```
.venv/bin/python -m pytest sim/tests/test_deficit_physique.py -q -s
```

Sortie réelle (extrait) :

```
deficit_reduction_infinitesimal = 1.000444171950221e-09
deficit_reduction_proportionnel = 5000.0
surplus=1e-09 → reduction=1.00044e-09, stock_apres=0, dette=10000
surplus=5000 → reduction=5000, stock_apres=0, dette=5000
surplus=20000 → reduction=10000, stock_apres=10000, dette=0
cas_testes = 6
```

L'écart de `1.000444e-09` par rapport au surplus nominal de `1e-09` est le
résidu de la représentation flottante du stock (`besoin + 1e-9`), pas une
réduction supplémentaire : la dette reste à 9999.999999999 kg.

### SC6 — re-mesure du monde réel

```
.venv/bin/python harness/queue/briefs/017-sim-seuil-survie-honnete/deliverables/measure_sc6_017.py
```

Sortie réelle :

```
horizon N_STAT_SURVIE = 1000 ticks
cellules chargées par G3 = 596
arêtes d'adjacence       = 1364
pop_initiale = 66865505
pop_finale   = 49588760

cellules_affamees_monde_reel_017 = 586
  (dénominateur : 596 cellules chargées ; condition : > 0)

morts_cumules_monde_reel_017 = 17276745
  (dénominateur : 66865505 habitants initiaux ; condition : > 0)

kg_transportes_monde_reel_017 = 5073132
  (dénominateur : 1364 arêtes × 1000 ticks = 1364000 occasions de transport ; condition : > 0)

fraction_survie_monde_reel_017 = 0.741619
  (fait observé, sans borne imposée par le brief ; prédiction stationnaire = 0.796597, tolérance = 0.101036, écart = 0.054978)

TOUTES LES CONDITIONS SC6 SONT SATISFAITES.
```

Ces valeurs remplacent celles du brief 013 pour le brief 017 ; l'archive 013
(`cellules_affamees_monde_reel_re = 536`) n'est pas touchée. Le nombre de
cellules ayant connu une pénurie augmente parce que l'horizon passe de 200 à
1 000 ticks, et parce que le critère compte désormais la pénurie réelle du
tick.

---

## 4. Adaptation des tests des briefs précédents

Aucune suppression silencieuse. Trois fichiers ont été adaptés, un quatrième
recentré.

### `sim/tests/test_mortalite_continue.py::test_deficit_non_efface_en_1_tick`

**Adapté.** Il encodait la valeur exacte
`D × (1 − DEFICIT_RECOVERY_RATE_PER_TICK)`, c'est-à-dire précisément la formule
que SC5 supprime. La **propriété** testée est conservée — un tick de surplus
n'efface pas une dette accumulée — mais elle est désormais adossée à la
physique : le surplus du tick (200 kg) est très inférieur à la dette
(10 000 kg), donc la dette survit, et la valeur attendue devient
`D − surplus × ratio`. Le test reste falsifiable : si la réduction redevenait
indépendante du surplus, la valeur exacte ne correspondrait plus.

`test_plafond_toute_population` est **conservé tel quel** : la propriété
« pas de plancher `max(1, …)` » du brief 013 reste acquise, et le report de
fraction ne la met pas en cause (une cellule fraîchement construite a un reste
nul).

### `sim/tests/test_survie_derivee.py`

**Recentré, pas supprimé.** `test_fraction_dans_marge` comparait la fraction
mesurée à N = 200 ticks à la fenêtre
`[fraction_predite ± SURVIE_MARGE_DERIVEE]`. Cette fenêtre :

1. ne dépendait ni de `HUNGER_DEATH_SCALE` ni de `MAX_DEATH_RATE_PER_TICK` ;
2. était verte à N = 200 et rouge à N ≥ 1600 sans régression du moteur.

`SURVIE_MARGE_DERIVEE` et `SEUIL_SURVIE_POPULATION_FRACTION` sont **supprimées**
de `sim/constants.py` (le brief laisse ce choix au Générateur) : les conserver
comme archives inertes aurait laissé deux constantes utilisables par un futur
brief comme si elles étaient encore un critère. Leur dérivation historique
reste lisible dans `sim/SEEDING.md` (section « SC3 brief 013 »), qui n'est pas
retouchée.

Le fichier conserve `test_fraction_predite_analytique` (la capacité de charge
analytique, toujours vraie) et gagne
`test_stationnaire_est_sous_la_capacite_de_charge`, qui rougirait si la densité
stationnaire redevenait la simple capacité de charge. La conformité de la
couche F2 est désormais portée par `test_survie_stationnaire.py` (horizon,
convergence, tolérance) et `test_sensibilite_survie.py` (signes) : ce fichier
n'est plus la seule garde F2, comme l'exige le brief.

### `sim/tests/test_causal_chain.py::test_sc7b_hunger_ticks_increments_when_stock_empty`

**Adapté.** Le maillon faim ne lit plus `food_stock_kg` : le test passe
maintenant explicitement la pénurie du tick à `_update_hunger`. La pénurie
utilisée est le besoin complet de la cellule, qui ne dispose d'aucun stock. Le
test prouve toujours la même chose : une cellule qui a réellement manqué voit
`hunger_ticks` progresser.

### `sim/tests/test_tick_nourrit_une_fois.py`

**Inchangé.** Le scénario témoin/receveuse continue de passer sans
modification : les deux cellules terminent le tick avec un stock nul (la
receveuse ne rembourse rien puisqu'elle n'a aucun surplus).

---

## 5. Preuves rouges (hard-won rule 4)

Les deux sabotages ont été faits sur des **copies de travail hors dépôt**
(`/tmp/forge_red_a`, `/tmp/forge_red_b`, avec `pipeline/` en lien symbolique
vers les artefacts G3). Le dépôt n'a jamais été saboté. Les quatre sorties sont
committées en `.txt` sous `sim/tests/proof_red/`.

### Paire A — « prédiction aveugle à `HUNGER_DEATH_SCALE` »

Sabotage : dans la copie, `HUNGER_DEATH_SCALE` est remplacée par un littéral
dans l'expression de la prédiction.

```
cd /tmp/forge_red_a && /workspace/.venv/bin/python -m pytest \
  sim/tests/test_sensibilite_survie.py::test_sensibilite_hds -v
```

Résultat (`sim/tests/proof_red/run_sensibilite_hds_red.txt`) :

```
regime x0.5 : mesure=0.768320 predite=0.796597
regime nominal  : mesure=0.757555 predite=0.796597
regime x2.0 : mesure=0.740239 predite=0.796597
sensibilite_hds_05_passe = 0
sensibilite_hds_2_passe = 0
FAILED sim/tests/test_sensibilite_survie.py::test_sensibilite_hds - Assertion...
```

La mesure bouge, la prédiction ne bouge plus : c'est exactement le défaut
dénoncé par l'audit, et le test le voit.
`sim/tests/proof_red/run_sensibilite_hds_green.txt` : `1 passed`.

### Paire B — « `int()` sans accumulateur »

Sabotage : dans la copie, retour à `deaths = int(population × death_rate)` sans
report de la fraction.

```
cd /tmp/forge_red_b && /workspace/.venv/bin/python -m pytest \
  sim/tests/test_mortalite_accumulateur.py::test_famine_tue_en_borne_de_ticks -v
```

Résultat (`sim/tests/proof_red/run_accumulateur_mort_red.txt`) :

```
N_BOUND_MORT = 10 (= ceil(1 / 0.1))
tick_du_premier_mort = -1
famine_tue_cellule_5hab = 0
FAILED sim/tests/test_mortalite_accumulateur.py::test_famine_tue_en_borne_de_ticks
```

`sim/tests/proof_red/run_accumulateur_mort_green.txt` : `1 passed`.

---

## 6. Suites complètes

```
.venv/bin/python -m pytest sim/tests/ -v
```

```
============================= test session starts ==============================
platform linux -- Python 3.12.3, pytest-9.1.1, pluggy-1.6.0 -- /workspace/.venv/bin/python
rootdir: /workspace
collecting ... collected 50 items
...
sim/tests/test_survie_stationnaire.py::test_fraction_survie_dans_tolerance_stationnaire PASSED [ 80%]
sim/tests/test_write_coverage.py::test_write_coverage_counter_etendu PASSED [100%]

============================== 50 passed in 5.26s ==============================
```

```
.venv/bin/python -m pytest harness/tests/ -q
```

```
........................................................................ [ 19%]
........................................................................ [ 39%]
........................................................................ [ 59%]
...............................................ssssssssssssssss......... [ 79%]
........................................................................ [ 98%]
....                                                                     [100%]
348 passed, 16 skipped in 18.18s
```

Aucun `FAILED`. Les 16 `SKIP` sont les tests Unity/PowerShell, ignorés sous
Linux comme prévu par `AGENTS.md`.

---

## 7. Registre de coût (SC8)

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/017-sim-seuil-survie-honnete \
  --event generator-run \
  --audit-id CURSOR-0e98199-pr69-seuil-survie-ignore-mortalite
```

`.venv/bin/python harness/backends/ledger.py report` affiche désormais
`harness/queue/briefs/017-sim-seuil-survie-honnete: cursor=1`.

---

## 8. Auto-contrôle mécanique (sans verdict)

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/017-sim-seuil-survie-honnete
```

Au moment de cette écriture, la porte répond `REJECT` sur trois contrôles, tous
attendus et aucun ne portant sur le fond du lot :

- `verdict_numbers_traceable` et `verdict_is_not_self_authored` : `verdict.md`
  n'existe pas. C'est le travail de l'Évaluateur. **Le Générateur n'écrit jamais
  de verdict**, et n'en a pas écrit.
- `declared_files_are_tracked` : les livrables ne sont pas encore suivis par
  git, puisque le Générateur ne committe pas (voir § 9).

Les autres contrôles sont au vert : `mtime_after_brief`,
`captures_differ_when_should` (les deux paires rouge/vert diffèrent bien),
`waivers_have_command_and_error`, `no_empty_sample_pass`,
`no_bare_python_alias`, `rubric_predates_deliverables`.

**Celui qui produit ne prononce pas la recevabilité.** Ce journal ne conclut
rien sur la valeur du travail : il rapporte ce qui a été fait et ce qui a été
mesuré.

---

## 9. Confirmation : aucun commit, aucune branche

Aucun `git commit`, aucun `git push`, aucun `git checkout -b`, aucun
`git branch` n'a été exécuté pendant ce lot. La branche de travail
(`forge/017-seuil-survie-honnete-ba01`) était déjà créée par l'orchestrateur et
n'a pas été quittée ni modifiée. Aucune branche `cursor/*` n'a été créée. Les
fichiers modifiés et créés sont laissés dans l'arbre de travail pour que
l'orchestrateur committe.

Fichiers touchés : `sim/constants.py`, `sim/engine.py`, `sim/model.py`,
`sim/world.py`, `sim/SEEDING.md`, cinq nouveaux fichiers de test sous
`sim/tests/`, trois tests adaptés, quatre preuves rouges sous
`sim/tests/proof_red/`, les trois livrables du présent dossier, et une ligne
ajoutée à `harness/queue/cost-ledger.jsonl`.

Aucun fichier interdit n'a été touché : ni `harness/*.py`, ni
`harness/pipeline/`, ni `architecture/`, ni `pipeline/geo/`, ni `unity/`, ni
`VISION.md`, ni `ROADMAP.md`, ni `.github/workflows/`, ni les archives des
briefs 011 à 014.
