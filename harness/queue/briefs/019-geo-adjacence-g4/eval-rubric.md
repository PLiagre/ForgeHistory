# Eval Rubric — Brief 019 : l'adjacence maritime (G4)

**Authored**: 2026-08-14T08:49:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : le rôle signataire est le rôle natif du harnais
`forge-planificateur`. L'acteur réel est un sous-agent Cursor Cloud (modèle
Claude Opus 5), orchestré par un agent Cursor Cloud qui remplace le CTO Claude
(plafond de quota atteint). Aucun suffixe n'est ajouté à la signature : le
contrôle mécanique `verdict_is_not_self_authored` compare les acteurs de part et
d'autre d'un lot, et un couple suffixé serait refusé.

---

## Guide de lecture

Pour chaque condition de succès du brief :

- **Vérification** : commandes rejouables. Depuis la racine du dépôt avec
  `.venv/bin/python`, ou depuis `pipeline/geo/` avec `../../.venv/bin/python`.
  Jamais l'alias nu de l'interpréteur (règle durement acquise n° 1).
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur lui-même,
  depuis les fichiers du dépôt, **sans reprendre aucun nombre du manifeste**.
  Un compteur qu'on ne peut pas re-dériver n'est pas une mesure.
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans une
  copie de travail **hors du dépôt**. Si le contrôle reste vert sous sabotage,
  la condition n'est pas satisfaite, même si toute la suite est verte.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire employé ci-dessous, expliqué une fois :

- **zone de mer** : morceau d'eau découpé comme une cellule l'est pour la terre,
  identifié par `zone_id`.
- **arête typée** : lien portant sa nature (`land-land`, `land-sea`, `sea-sea`,
  `strait`).
- **bassin enfermé** : ensemble d'eaux qui ne touche pas le bord de la fenêtre
  d'étude, donc ne communique pas avec la mer extérieure par la géométrie seule.
- **lien topologique déclaré** : arête ajoutée parce qu'une source historique
  atteste une communication que la géométrie moderne ne montre plus.
- **cas rouge naturel** : preuve qu'un contrôle mord obtenue en retirant une
  cause réelle (ici la déclaration historique), et non en mutant une donnée.

Note d'environnement, à vérifier avant de conclure quoi que ce soit : la pile
scientifique vit dans `.venv/` à la racine. `pipeline/geo/tests/test_qa_red_g4.py`
n'est **pas** censé être collecté par la suite de tests automatiques (comme ses
homologues G2/G3, il expose une fonction que le script de preuve importe) : une
collecte à zéro test n'est donc pas un défaut, c'est `tests/run_proof_g4.py` qui
fait foi.

---

## SC1 — Zones de mer dénombrées dans la fourchette lue, sans collision d'identifiant

**Vérification :**

1. Rejouer la preuve, depuis `pipeline/geo/` :
   ```
   ../../.venv/bin/python tests/run_proof_g4.py
   ```
   Code de sortie attendu : 0.

2. Rejouer le script de mesure, depuis la racine :
   ```
   .venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py
   ```
   La sortie doit nommer, chacun avec son dénominateur :
   `zones_mer_denombrees`, `composantes_mer_totales`,
   `composantes_mer_couvertes`, `plans_eau_exclus_lacs`,
   `collisions_id_mer_terre`, `ids_mer_sous_la_base`,
   `copie_sea_zones_identique`, `cellules_lues_g3`.

3. Vérifier que la fourchette est **lue** et non recopiée :
   ```
   .venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants as c; print(c.SEA_ZONE_COUNT_MIN, c.SEA_ZONE_COUNT_MAX, c.SEA_ZONE_ID_BASE)"
   ```
   Puis relire `tests/run_proof_g4.py`, `tests/test_qa_red_g4.py` et
   `steps/04_adjacency.py` : aucune de ces trois valeurs ne doit y apparaître en
   littéral. Un test qui écrit la borne à la main ne contrôle plus rien, il se
   contrôle lui-même (règle n° 2 : un contrôle dérive, il n'est jamais nommé
   d'après sa cible ; règle n° 3 : un compteur dérive aussi).

4. Vérifier l'égalité octet pour octet de la copie de noms de mer, **par calcul**
   et non par lecture d'une valeur citée :
   ```
   .venv/bin/python -c "
   import hashlib, pathlib
   a = pathlib.Path('unity/game_unity/Assets/StreamingAssets/data/sea_zones.json').read_bytes()
   b = pathlib.Path('pipeline/geo/legacy_game_data/sea_zones.json').read_bytes()
   print('identique:', hashlib.sha256(a).hexdigest() == hashlib.sha256(b).hexdigest())
   "
   ```
   Résultat attendu : `identique: True`. Vérifier aussi que le fichier Unity
   n'a pas été modifié :
   ```
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','unity/'],
                        capture_output=True, text=True).stdout or 'unity/ intact')
   "
   ```

5. Vérifier `collisions_id_mer_terre == 0` et `ids_mer_sous_la_base == 0`. Ces
   zéros sont des **mesures réelles** : la sentinelle « non calculé » du projet
   est `-1` (règle n° 8) et ne doit apparaître pour aucun compteur calculé de ce
   lot.

**Reconstruction indépendante :**
L'Évaluateur écrit son propre relevé hors dépôt : il lit l'ensemble des
`cell_id` de `pipeline/geo/artifacts/cells_g3.json` et l'ensemble des `zone_id`
de `pipeline/geo/artifacts/sea_zones_g4.json`, calcule leur intersection
(doit être vide), compte les `zone_id` inférieurs à `SEA_ZONE_ID_BASE` lu de
`constants.py` (doit être zéro), et recompte les zones. Il vérifie que le
nombre de cellules lues égale `cell_count` de `stats_g3.json`.

Attention, c'est le piège de ce lot : les identifiants terrestres de la maille
actuelle montent bien au-delà de la base des identifiants maritimes. Une
attribution naïve depuis la base **doit** provoquer une collision. Si le
Générateur n'a pas sauté les valeurs prises, l'intersection sera non vide et la
condition échoue — même si `g4d_sea_ids_no_collision` avait été déclaré vert.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, forcer un `zone_id` égal à un `cell_id` existant,
puis rejouer la preuve : `G4-D` doit passer au rouge. Si tout reste vert, le
contrôle ne protège rien (règle n° 7 : la présence n'est pas la fonction).

Deuxième contre-preuve : retirer une zone d'un bassin enfermé (le laisser sans
zone). `g4c_sea_covers_without_holes` doit rougir. Une couverture obtenue en
oubliant un bassin n'est pas une couverture.

Est également disqualifiant : une borne de `pipeline/geo/constants.py` modifiée
pour faire entrer le compte dans la fourchette.

**Résultat attendu :** PASS si les zones existent, se comptent dans la fourchette
lue du fichier de constantes, couvrent chaque composante d'eau, et si aucun
identifiant maritime ne heurte un identifiant terrestre.

---

## SC2 — Graphe typé : les quatre natures d'arête existent, mesurées sur le monde réel

**Vérification :**

1. Lire les comptes par type depuis l'artefact, sans les recopier du manifeste :
   ```
   .venv/bin/python -c "
   import json
   s = json.load(open('pipeline/geo/artifacts/stats_g4.json'))
   print('by_kind:', s['by_kind'])
   print('total:', s['adjacency_count'], 'coastal:', s['coastal_cell_count'])
   "
   ```
   Les quatre types doivent être présents avec un compte strictement positif.

2. Vérifier qu'aucune arête ne porte encore l'identifiant fourre-tout de mer du
   graphe G3 :
   ```
   .venv/bin/python -c "
   import json, sys
   sys.path.insert(0,'pipeline/geo')
   from constants import SEA_CELL_ID
   d = json.load(open('pipeline/geo/artifacts/adjacency_g4.json'))
   edges = d['adjacency']
   bad = [e for e in edges if SEA_CELL_ID in (e['a'], e['b'])]
   print('aretes_avec_id_mer_placeholder:', len(bad), '/', len(edges))
   "
   ```
   Résultat attendu : 0 sur un total strictement positif.

3. Rejouer la suite de contrôles et vérifier que `Q4`, `Q7` et `G4-A` sont verts
   avec une preuve rouge non vide :
   ```
   .venv/bin/python -c "
   import json
   q = json.load(open('pipeline/geo/logs/v1_050_qa.json'))
   for c in q['checks']:
       print(c['id'], c['passed'], bool(c['red_proof']))
   "
   ```

**Reconstruction indépendante :**
L'Évaluateur recompte lui-même, hors dépôt, les arêtes par type depuis
`adjacency_g4.json`, et re-dérive la littoralité : l'ensemble des cellules
apparaissant dans une arête `land-sea` doit être **exactement** l'ensemble des
cellules que `sea_zones_g4.json` / `stats_g4.json` déclarent littorales. Il
vérifie que le compte de cellules littorales est strictement inférieur au nombre
total de cellules (une littoralité universelle signalerait une mer qui touche
tout, donc une dérivation cassée) et strictement positif.

Il vérifie aussi que les arêtes `land-land` correspondent, une pour une, aux
arêtes `land-land` de `artifacts/adjacency_g3.json` : ce lot les **lit**, il ne
les recalcule pas. Un écart ici veut dire que la maille committée n'est plus la
source unique.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, saisir la littoralité à la main sur une cellule qui
ne porte aucune arête `land-sea` (ou en retirer une qui en porte). `G4-A` doit
rougir. Une littoralité qui survit à la disparition de son arête n'est pas
dérivée, elle est stockée — c'est le mode d'échec n° 5 (compteur en dur).

Est également disqualifiant : un type d'arête à zéro « parce que la géométrie
ne s'y prête pas » sans escalade ; ou un type rendu non vide par une arête
fabriquée à la main plutôt que dérivée de la géométrie.

**Résultat attendu :** PASS si les quatre natures d'arête existent sur données
réelles, si aucune arête ne garde l'identifiant fourre-tout, et si la
littoralité se dérive des arêtes.

---

## SC3 — Détroit : seuil lu, largeur mesurée, au moins un entre deux masses distinctes

**Vérification :**

1. Vérifier que le seuil vient du fichier de constantes et non d'un littéral :
   ```
   .venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants as c; print(c.G4_STRAIT_MAX_WIDTH_M); print(c.G4_STRAIT_JUSTIFICATION)"
   ```
   Puis relire `steps/04_adjacency.py` : la valeur ne doit pas y figurer en
   littéral.

2. Vérifier les largeurs mesurées portées par les arêtes :
   ```
   .venv/bin/python -c "
   import json
   d = json.load(open('pipeline/geo/artifacts/adjacency_g4.json'))
   st = [e for e in d['adjacency'] if e['kind'] == 'strait']
   gaps = sorted(float(e['gap_m']) for e in st)
   print('detroits:', len(st), 'ecart_min_m:', gaps[0] if gaps else None, 'ecart_max_m:', gaps[-1] if gaps else None)
   "
   ```
   Toutes les largeurs doivent être strictement positives et inférieures ou
   égales au seuil lu.

3. Vérifier `detroits_entre_masses_differentes > 0` dans la sortie du script de
   mesure.

**Reconstruction indépendante :**
L'Évaluateur re-dérive lui-même, hors dépôt : il charge les géométries des
cellules de `cells_g3.json`, calcule les composantes connexes de la terre à
partir des arêtes `land-land` (deux cellules d'une même composante sont
joignables à pied), puis, pour chaque arête `strait`, vérifie que les deux
cellules ne sont pas contiguës, que leur distance mesurée correspond à la
largeur déclarée, et compte celles dont les deux cellules appartiennent à des
composantes différentes. Ce compte doit être strictement positif.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, abaisser le seuil de détroit jusqu'à ce qu'aucun
détroit ne subsiste — la preuve doit alors montrer un type d'arête vide et
échouer. Puis, dans une seconde copie, déclarer en `strait` deux cellules
**contiguës** : `q7_adjacency_contiguous_typed` doit rougir (un détroit entre
deux terres qui se touchent n'est pas un détroit).

Est également disqualifiant : un détroit dont la largeur déclarée ne correspond
pas à la distance géométrique mesurable entre les deux cellules ; ou un unique
détroit entre deux cellules de la **même** masse terrestre présenté comme
satisfaisant la condition.

**Résultat attendu :** PASS si les détroits sont dérivés du seuil lu, portent
une largeur mesurée juste, et si au moins un relie deux terres qu'on ne peut pas
rejoindre à pied.

---

## SC4 — Le lien déclaré est porteur : sans lui, le monde se referme

**Vérification :**

1. Vérifier que chaque lien appliqué reste traçable jusqu'à sa source :
   ```
   .venv/bin/python -c "
   import json
   t = json.load(open('pipeline/geo/artifacts/topology_links_g4.json'))
   print('reachability:', t['reachability'])
   for l in t.get('links', []):
       print(l.get('id'), l.get('date'), l.get('certainty'), '|', str(l.get('source'))[:60])
   "
   ```
   Le nombre de liens doit être **égal** au nombre de corrections
   `declare_topology_link` du fichier de déclarations :
   ```
   .venv/bin/python -c "
   import json
   d = json.load(open('pipeline/geo/data/corrections_1400.json'))
   print(len([c for c in d['corrections'] if c.get('operation') == 'declare_topology_link']))
   "
   ```

2. Lire les deux sorties committées et vérifier qu'elles disent des choses
   opposées : `pipeline/geo/logs/v1_050_g4b_links_on.txt` (tout bassin
   atteignable) et `pipeline/geo/logs/v1_050_g4b_links_off.txt` (bassins nommés
   injoignables). Vérifier qu'elles diffèrent réellement :
   ```
   .venv/bin/python -c "
   import hashlib, pathlib
   p = pathlib.Path('pipeline/geo/logs')
   a = hashlib.sha256((p/'v1_050_g4b_links_on.txt').read_bytes()).hexdigest()
   b = hashlib.sha256((p/'v1_050_g4b_links_off.txt').read_bytes()).hexdigest()
   print('different:', a != b)
   "
   ```

3. Vérifier que `G4-B`'s `red_proof` de `logs/v1_050_qa.json` désigne bien le cas
   **naturel** (liens coupés) et non une mutation de donnée. Un `red_proof` qui
   décrit une géométrie mutée ne prouve pas que la déclaration historique est
   porteuse : il prouve seulement que le contrôle sait rougir. Cette distinction
   est la condition, pas un détail de forme.

4. Regarder les deux captures du Zuiderzee soi-même (règle n° 11) et vérifier
   qu'elles montrent visiblement deux situations différentes ; vérifier que le
   journal les décrit.

**Reconstruction indépendante :**
L'Évaluateur reconstruit l'atteignabilité lui-même, hors dépôt : il construit le
graphe des seules arêtes `sea-sea` **non déclarées** de `adjacency_g4.json`,
part des zones de la mer extérieure, et vérifie que les zones des bassins
enfermés sont alors **injoignables**. Puis il ajoute les arêtes déclarées et
vérifie qu'elles le deviennent. Le lien doit donc être la cause de
l'atteignabilité, et non un ornement posé sur un graphe déjà connexe.

Il vérifie aussi que la cible du lien appartient à la mer extérieure et porte le
nom attesté demandé par la déclaration — un lien qui relierait le bassin à
lui-même serait vert sans rien démontrer.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, retirer la déclaration du fichier de corrections (ou
appeler la dérivation avec les liens désactivés) et rejouer la preuve : `G4-B`
doit rougir et nommer les bassins injoignables. S'il reste vert, alors la
continuité vient d'ailleurs — probablement d'une géométrie retouchée, ce qui est
un échec de fond.

Est également disqualifiant : une brèche ouverte dans le trait de côte, une
tolérance géométrique élargie, ou un bassin enfermé fusionné avec la mer
extérieure pour rendre `G4-B` vert. Vérifier par
`git status --porcelain -- pipeline/geo/data/ pipeline/geo/steps/02b_corrections_1400.py`
qu'aucune déclaration ni aucun code de correction n'a été touché.

**Résultat attendu :** PASS si l'atteignabilité des bassins enfermés dépend
réellement des liens déclarés, si les deux sorties et les deux captures existent,
diffèrent et sont déclarées en couples, et si le rouge de `G4-B` est le cas
naturel.

---

## SC5 — Les noms de mer sont un proxy hérité, déclaré avant mesure

**Vérification :**

1. Lire la section de `pipeline/geo/README.md` consacrée à ce lot. Elle doit
   dire, en clair : que les noms viennent de données **héritées du jeu** ; que
   ce ne sont ni des frontières historiques de 1400 ni une source savante ; que
   le tableau d'identifiants riverains hérités n'est qu'un **proxy de
   localisation de nom**, jamais une clé spatiale ; quelle règle d'attribution
   est employée et comment les égalités de distance sont départagées.

2. Vérifier l'**ordre d'écriture** : cette déclaration précède, dans le fichier,
   toute citation d'un compteur mesuré de ce lot. Une justification écrite après
   la mesure est une calibration déguisée.

3. Vérifier les comptes de noms :
   ```
   .venv/bin/python -c "
   import json
   legacy = json.load(open('pipeline/geo/legacy_game_data/sea_zones.json'))['sea_zones']
   zones = json.load(open('pipeline/geo/artifacts/sea_zones_g4.json'))
   z = zones['sea_zones'] if isinstance(zones, dict) and 'sea_zones' in zones else zones
   used = {x.get('name') for x in z if x.get('name')}
   attested = {x['name'] for x in legacy}
   print('noms_attestes_lus:', len(attested))
   print('zones_nommees:', sum(1 for x in z if x.get('name')), '/', len(z))
   print('noms_distincts_employes:', len(used & attested), '/', len(attested))
   print('noms_attestes_non_employes:', len(attested - used), '/', len(attested))
   print('noms_hors_liste_attestee:', sorted(used - attested))
   "
   ```
   `noms_hors_liste_attestee` doit être **vide** : aucun nom inventé, traduit ni
   complété.

**Reconstruction indépendante :**
L'Évaluateur re-dérive l'attribution hors dépôt : pour chaque zone nommée
héritée il recalcule le point d'ancrage (moyenne des coordonnées héritées
riveraines, lues de `legacy_game_data/province_coordinates.json`, projetées),
puis attribue à chaque zone de mer le nom de l'ancrage le plus proche, en
départageant les égalités par le plus petit identifiant hérité. L'attribution
reconstruite doit coïncider, zone par zone, avec celle de l'artefact.

**Contre-preuve disqualifiante :**
Un nom présent dans l'artefact et absent du fichier hérité : nom inventé,
condition non satisfaite (règle n° 10 — quand une donnée manque, un agent
l'invente par défaut ; l'absence doit être déclarable et le code doit refuser de
deviner).

Également disqualifiant : un plancher imposé sur le nombre de noms employés (par
exemple un test exigeant que les quatorze noms servent), ou une règle
d'attribution ajustée après mesure pour y parvenir ; une phrase présentant les
noms hérités comme une source savante ou des frontières d'époque ; une
documentation qui met en œuvre une règle de départage sans la nommer.

**Résultat attendu :** PASS si les noms sont lus, l'attribution reconstructible,
la provenance déclarée avant mesure, et si aucun plancher n'est imposé.

---

## SC6 — ADR-0003 dans les artefacts, pas seulement dans la prose

**Vérification :**

1. Compter les occurrences de la sous-chaîne dans chaque artefact :
   ```
   .venv/bin/python -c "
   import pathlib
   art = pathlib.Path('pipeline/geo/artifacts')
   noms = ['adjacency_g4.json','sea_zones_g4.json','topology_links_g4.json',
           'stats_g4.json','MANIFEST_g4.json']
   total = 0
   for n in noms:
       t = (art/n).read_text(encoding='utf-8')
       c = t.count('province')
       total += c
       print(n, c)
   reg = pathlib.Path('pipeline/geo/registry/sea_zone_registry.json').read_text(encoding='utf-8')
   print('sea_zone_registry.json', reg.count('province'))
   div = (art/'adjacency_divergence_g4.json').read_text(encoding='utf-8')
   print('adjacency_divergence_g4.json', div.count('province'), '(doit etre > 0)')
   print('total_hors_divergence:', total + reg.count('province'), '(doit etre 0)')
   "
   ```
   Attendu : zéro partout sauf dans le fichier de divergence, où le compte doit
   être strictement positif.

2. Vérifier que personne ne lit le fichier de divergence en dehors de la preuve
   QA :
   ```
   rg -n "adjacency_divergence_g4" pipeline/geo --glob '*.py'
   ```
   Seuls le module de dérivation (qui l'écrit) et le script de preuve peuvent
   apparaître. Aucune lecture par un autre code.

3. Vérifier que le fichier de divergence se déclare lui-même comme QA seulement
   (`"qa_only": true`) et que `README.md` comme `deliverables/manifest.json` le
   décrivent ainsi.

4. Lire les trois constats de comparaison (`aretes_heritees_confirmees`,
   `_contredites`, `_manquantes`) et vérifier qu'aucun **seuil** ne leur est
   appliqué nulle part : ce sont des constats, pas des objectifs.

**Reconstruction indépendante :**
L'Évaluateur relit `docs/adr/0003-single-spatial-primary-key.md` et vérifie que
la frontière est bien celle qu'exige la décision : une seule clé spatiale, la
cellule ; tout regroupement plus grossier est **dérivé**. Puis il refait
lui-même le balayage de sous-chaîne, sur les fichiers du dépôt, et recompte les
arêtes héritées lues de `legacy_game_data/province_adjacency.json` pour vérifier
le dénominateur des trois constats.

**Contre-preuve disqualifiante :**
Un seul champ nommé d'après une province dans `adjacency_g4.json` (ou dans tout
artefact autre que le fichier de divergence) : la seconde réponse inscriptible à
la question « où ? » est réintroduite, condition non satisfaite.

Également disqualifiant, et c'est la contre-preuve la plus importante ici :
`occurrences_province_dans_divergence` à **zéro**. Cela voudrait dire que la
comparaison n'a pas eu lieu et que le contrôle de frontière n'a rien eu à
retenir — un contrôle qui ne peut rien rattraper ne prouve rien (règle n° 7).

Également disqualifiant : un autre artefact, ou un autre morceau de code, qui
lit le fichier de divergence ; ou une appartenance de cellule fondée sur un
identifiant hérité.

**Résultat attendu :** PASS si la frontière est mécaniquement vérifiée sur les
artefacts, si la comparaison a réellement eu lieu, et si elle reste confinée à
son fichier étiqueté.

---

## SC7 — Déterminisme deux passes, huit contrôles verts, chacun mordant

**Vérification :**

1. Relire le rapport de contrôle :
   ```
   .venv/bin/python -c "
   import json
   q = json.load(open('pipeline/geo/logs/v1_050_qa.json'))
   ch = q['checks']
   print('controles:', len(ch))
   print('verts:', sum(1 for c in ch if c['passed']))
   print('rouges_prouves:', sum(1 for c in ch if c.get('red_proof')))
   pairs = q['determinism']['sha256']
   ok = sum(1 for p in pairs.values() if len(p) == 2 and p[0] == p[1] and p[0])
   print('paires_egales:', ok, '/', len(pairs))
   for c in ch:
       print(' ', c['id'], c['passed'], '|', str(c.get('red_proof'))[:50])
   "
   ```
   Attendu : 8 contrôles, 8 verts, 8 preuves rouges non vides, toutes les paires
   d'empreintes égales et non vides, total de paires strictement positif.

2. Rejouer la preuve entière depuis `pipeline/geo/` et vérifier que le code de
   sortie est 0 **et** que les artefacts régénérés sont identiques à ceux
   committés (déterminisme entre deux sessions, pas seulement entre deux passes
   d'une même exécution) :
   ```
   ../../.venv/bin/python tests/run_proof_g4.py
   ```
   puis, depuis la racine :
   ```
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','pipeline/geo/artifacts','pipeline/geo/registry'],
                        capture_output=True, text=True).stdout or 'artefacts identiques apres re-execution')
   "
   ```
   Une re-exécution propre qui ne produit aucune différence est l'état vert
   attendu, pas un signe que rien n'a tourné.

3. Vérifier l'égalité d'empreinte entre la terre employée par G4 et l'entrée
   déclarée de G3, **par calcul** :
   ```
   .venv/bin/python -c "
   import hashlib, json, pathlib
   art = pathlib.Path('pipeline/geo/artifacts')
   m3 = json.load(open(art/'MANIFEST_g3.json'))
   m4 = json.load(open(art/'MANIFEST_g4.json'))
   vivant = hashlib.sha256((art/'coastline_1400.json').read_bytes()).hexdigest() if (art/'coastline_1400.json').exists() else None
   print('g3_inputs == g4_inputs :', m3['inputs']['coastline_1400'] == m4['inputs'].get('coastline_1400'))
   print('artefact vivant identique :', vivant == m3['inputs']['coastline_1400'] if vivant else 'artefact absent (regenerable)')
   "
   ```

4. Vérifier qu'aucune constante n'a bougé :
   ```
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','pipeline/geo/constants.py','pipeline/geo/qa/checks.py','pipeline/geo/pipeline.py','pipeline/geo/io_util.py','pipeline/geo/projection.py','pipeline/geo/steps/02_coastline.py','pipeline/geo/steps/02b_corrections_1400.py','pipeline/geo/steps/03_cells.py'],
                        capture_output=True, text=True).stdout or 'fichiers partages inchanges')
   "
   ```

5. Lire les constats ouverts éventuels (`zones_hors_bornes_intention` non nul) :
   ils doivent figurer dans le journal **et** dans `README.md`, sans qu'aucune
   borne ait été déplacée.

**Reconstruction indépendante :**
L'Évaluateur recalcule lui-même les empreintes des artefacts G4 committés et les
compare à celles du bloc `outputs` de `MANIFEST_g4.json` — le manifeste doit
décrire les fichiers réellement présents. Il vérifie, en lisant
`tests/test_qa_red_g4.py`, qu'il existe bien un cas par identifiant de contrôle
et que celui de `G4-B` passe par la dérivation liens coupés et non par une
mutation.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, forcer une empreinte différente entre les deux passes
(par exemple en introduisant un ordre de tri instable dans un export) : `Q10`
doit rougir. S'il reste vert, le déterminisme n'est pas prouvé, il est espéré.

Également disqualifiant : un `red_proof` vide accompagné d'un `passed: true` ;
un contrôle rendu vert par une modification de `qa/checks.py` ; une valeur de
`constants.py` déplacée ; un horodatage courant dans un artefact (qui rendrait
tout déterminisme impossible à revérifier demain).

**Résultat attendu :** PASS si les huit contrôles sont verts et mordants, si les
deux passes coïncident, si la re-exécution ne produit aucune différence, et si
aucun fichier partagé n'a bougé.

---

## SC8 — Le contrat du crochet existant est réellement satisfait

**Vérification :**

1. Depuis `pipeline/geo/` :
   ```
   ../../.venv/bin/python pipeline.py --source adjacency
   ```
   La commande doit se terminer sans erreur et afficher la ligne de résumé G4 :
   projection, nombre de zones, nombre d'arêtes par type, cellules littorales,
   atteignabilité. Comparer cette sortie à celle recopiée dans le journal.

2. Vérifier que `pipeline/geo/pipeline.py` est **inchangé** :
   ```
   .venv/bin/python -c "
   import subprocess
   d = subprocess.run(['git','diff','--','pipeline/geo/pipeline.py'], capture_output=True, text=True).stdout
   print('diff vide:', d.strip() == '')
   "
   ```

3. Vérifier que le module rend bien toutes les clés attendues par le crochet :
   `metrics.sea_zone_count`, `metrics.adjacency_count`, `metrics.by_kind`,
   `metrics.coastal_cell_count`, `projection.epsg`,
   `reachability.all_enclosed_reachable`, `captures`, `shas`. Une clé absente
   ferait échouer la commande ci-dessus : c'est la commande qui fait foi, pas la
   lecture du code.

**Reconstruction indépendante :**
L'Évaluateur relit la branche `adjacency` de `pipeline/geo/pipeline.py` telle
qu'elle existe dans le dépôt, énumère les clés qu'elle consulte, et vérifie une
par une que la sortie de la commande les a réellement affichées — pas qu'elles
sont mentionnées dans le journal.

**Contre-preuve disqualifiante :**
Un ajustement, même minuscule, de `pipeline/geo/pipeline.py` : ce brief n'en
autorise aucun, et le crochet a été vérifié comme déjà câblé avant l'écriture du
brief. Un crochet « rendu compatible » en modifiant le crochet ne prouve pas que
le contrat est satisfait.

Également disqualifiant : la commande rapportée comme fonctionnelle dans le
journal sans exécution réelle dans ce dépôt (règle n° 7 : la présence n'est pas
la fonction).

**Résultat attendu :** PASS si la commande du crochet existant fonctionne sans
que le crochet ait été touché.

---

## SC9 — Preuves committées, re-vérifiables depuis un clone, README sans sur-revendication

**Vérification :**

1. Vérifier que chaque preuve déclarée sous `pipeline/geo/` est suivie par git,
   malgré la règle d'exclusion :
   ```
   .venv/bin/python -c "
   import subprocess
   out = subprocess.run(['git','ls-files','pipeline/geo/artifacts','pipeline/geo/logs','pipeline/geo/capture','pipeline/geo/registry','pipeline/geo/legacy_game_data'],
                        capture_output=True, text=True).stdout
   for l in sorted(out.splitlines()):
       print(l)
   "
   ```
   Tous les fichiers de preuve G4 nommés par le brief doivent y figurer. Un
   fichier déclaré mais absent de cette liste est une preuve qu'un clone ne peut
   pas revérifier — c'est un défaut, pas un détail : la porte mécanique ne
   vérifie pas le suivi des chemins qui sortent du dossier du brief, ce compteur
   existe précisément pour cela.

2. Vérifier `pipeline/geo/.gitignore` inchangé (le suivi passe par un ajout
   forcé, pas par un assouplissement de la règle) :
   ```
   .venv/bin/python -c "
   import subprocess
   print(subprocess.run(['git','status','--porcelain','--','pipeline/geo/.gitignore'],
                        capture_output=True, text=True).stdout or 'gitignore inchange')
   "
   ```

3. Lire `pipeline/geo/README.md` : il doit dire que G4 (zones de mer + adjacence
   typée) est livré, et **énumérer ce qui ne l'est pas** : fleuves, relief,
   climat, ressources, villes, propriété, LOD, textures d'identifiants, QA de
   chaîne complète. Aucune phrase ne doit laisser croire que le jalon E1 est
   clos, ni que la mer est « simulée ».

4. Vérifier le couple d'instantanés du README :
   ```
   .venv/bin/python -c "
   import hashlib, pathlib
   a = pathlib.Path('harness/queue/briefs/019-geo-adjacence-g4/deliverables/pre-edit/pipeline-geo-README.md.orig').read_bytes()
   b = pathlib.Path('pipeline/geo/README.md').read_bytes()
   print('different:', hashlib.sha256(a).hexdigest() != hashlib.sha256(b).hexdigest())
   "
   ```
   Attendu : `different: True`, et le couple déclaré par `must_differ_from` dans
   le manifeste.

5. Vérifier que le README reste **descriptif** :
   ```
   .venv/bin/python -m pytest harness/tests/test_single_source_of_instruction.py -v
   ```

**Reconstruction indépendante :**
L'Évaluateur compare la liste des fichiers déclarés par
`deliverables/manifest.json` à la sortie de `git ls-files`, ligne par ligne, et
signale tout écart dans les deux sens : un fichier déclaré non suivi, et un
fichier de preuve produit mais non déclaré (une preuve non déclarée n'est
vérifiée par personne).

**Contre-preuve disqualifiante :**
Une preuve laissée dans un chemin ignoré par git. Le contrôle direct : cloner le
dépôt dans un répertoire temporaire hors dépôt, à la même révision, et vérifier
que chaque preuve déclarée y est présente. Une preuve qui n'existe que dans un
seul répertoire de travail n'est pas une preuve.

Également disqualifiant : un README qui revendique le relief, le climat, les
ressources, les fleuves, ou le jalon E1 ; ou une règle d'exclusion assouplie
pour faire entrer les preuves.

**Résultat attendu :** PASS si toutes les preuves sont récupérables depuis un
clone frais et si le README dit exactement ce qui est livré, ni plus.

---

## SC10 — Mesure rejouable, manifeste complet, suites non régressées, registre de coût

**Vérification :**

1. Rejouer le script de mesure depuis la racine :
   ```
   .venv/bin/python harness/queue/briefs/019-geo-adjacence-g4/deliverables/measure_g4_019.py
   ```
   Chaque compteur du tableau du brief doit être imprimé **avec son
   dénominateur**. Un compteur sans dénominateur est irrecevable. Vérifier aussi
   que le script **lit** les artefacts et les constantes — un script qui
   imprimerait des valeurs écrites à la main est un compteur en dur (mode
   d'échec n° 5).

2. Vérifier que chaque compteur du manifeste porte un `sample_size` réel, non nul
   et différent de la sentinelle (contrôle mécanique `no_empty_sample_pass`), et
   que les trois couples `must_differ_from` y sont déclarés (contrôle
   `captures_differ_when_should` — un couple non déclaré n'est pas vérifié, et
   l'absence de déclaration est en soi un défaut).

3. Suite du harnais toujours verte :
   ```
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   Aucun `FAILED`. Les `SKIP` propres à Linux (tests Unity) sont acceptés et
   doivent être déclarés dans le journal.

4. Vérifier qu'aucune archive n'a été retouchée et qu'aucun fichier hors
   périmètre n'a bougé :
   ```
   .venv/bin/python -c "
   import subprocess
   out = subprocess.run(['git','status','--porcelain'], capture_output=True, text=True).stdout
   print(out or 'arbre propre')
   "
   ```
   Toute ligne doit correspondre à un chemin autorisé par le périmètre du brief
   (D16). Un fichier sous `sim/`, `harness/*.py`, `docs/adr/`, `architecture/`,
   `ROADMAP.md`, `HANDOFF.md`, `VISION.md`, `.github/` ou sous les archives des
   briefs 001 à 018 est disqualifiant.

5. Registre de coût :
   ```
   .venv/bin/python harness/backends/ledger.py report
   ```
   Le brief `019-geo-adjacence-g4` doit apparaître avec au moins `cursor=1`, et
   la dernière ligne de `harness/queue/cost-ledger.jsonl` doit porter
   `"event": "generator-run"`, `"backend": "cursor"` et un chemin de brief
   contenant `019`. L'absence d'`audit_id` est normale : ce brief naît de la
   feuille de route.

6. Vérifier que le Générateur n'a **ni committé, ni poussé, ni créé de branche** :
   la branche courante est celle fournie par l'orchestrateur, et l'historique ne
   contient aucun commit signé du Générateur.

**Reconstruction indépendante :**
L'Évaluateur re-dérive lui-même au moins dix compteurs du tableau, choisis dans
des familles différentes (dénombrement de zones, types d'arêtes, atteignabilité,
noms, frontière ADR-0003, déterminisme, suivi git), et compare aux valeurs du
manifeste sans les avoir lues d'abord. Un écart, même d'une unité, est un écart :
le manifeste décrit alors autre chose que ce que le dépôt contient.

**Contre-preuve disqualifiante :**
Un compteur du manifeste que le script de mesure ne produit pas, ou qu'on ne
peut pas re-dériver depuis les fichiers du dépôt. Un `sample_size` égal à zéro
ou à la sentinelle pour une affirmation réelle. Un zéro rapporté avec la
sentinelle alors qu'il a été mesuré, ou l'inverse (règle n° 8).

Également disqualifiant : un compteur mesuré sur une liste vide (aucune zone,
aucune arête) et présenté comme satisfaisant — mode d'échec n° 6, l'échantillon
vide qui passe en silence.

**Résultat attendu :** PASS si tout se rejoue, si tous les compteurs sont
échantillonnés et re-dérivables, si la suite du harnais reste verte et si le
périmètre a été respecté.

---

## Porte mécanique

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/019-geo-adjacence-g4
```

Doit répondre `VERDICT: ACCEPT`, tous contrôles applicables au vert, avant que
l'Évaluateur ne rédige son verdict de fond.

**Avertissement :** la porte juge la forme du lot, pas sa substance. Un lot peut
obtenir `ACCEPT` de la porte et `FAIL` de l'Évaluateur. En particulier, la porte
ne vérifie pas le suivi git des fichiers déclarés **hors** du dossier du brief :
c'est SC9 qui le fait, à la main.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| Une valeur de `pipeline/geo/constants.py` modifiée, dans quelque sens que ce soit | Une borne déplacée après mesure n'est plus une borne — la leçon coûteuse des amendements du brief 007 |
| `pipeline/geo/qa/checks.py` modifié | Un contrôle rendu vert en modifiant le contrôle ne contrôle plus rien (règle n° 5 : une garde placée après l'effet ne protège rien) |
| `pipeline/geo/pipeline.py` modifié | Le crochet était déjà câblé ; le rendre compatible en le changeant ne prouve pas que le contrat est satisfait |
| Une borne de fourchette ou de seuil écrite en littéral dans un test ou dans le module | Règles n° 2 et n° 3 : un contrôle et un compteur dérivent, ils ne se nomment pas d'après leur cible |
| Un `zone_id` égal à un `cell_id` existant | Deux entités différentes portant la même identité ; c'est le piège de la maille actuelle, dont les identifiants dépassent la base maritime |
| Un type d'arête à zéro, ou rendu non vide par une arête fabriquée à la main | Le graphe typé n'est pas dérivé du monde réel |
| L'identifiant fourre-tout de mer de G3 encore présent dans une arête exportée | L'adjacence n'est pas typée : la mer reste un trou noir unique |
| La littoralité saisie ou stockée au lieu d'être dérivée des arêtes `land-sea` | Mode d'échec n° 5 (compteur en dur) ; retirer l'eau doit retirer la littoralité |
| `G4-B` rendu vert par une brèche dans le trait de côte, une tolérance élargie, ou une fusion de bassins | La continuité historique se **déclare** ; falsifier la géométrie n'est pas la corriger |
| Le cas rouge de `G4-B` obtenu par mutation de donnée au lieu des liens coupés | Le rouge prouverait le code du contrôle, pas le caractère porteur de la déclaration |
| Un lien déclaré posé sur un graphe déjà connexe (lien décoratif) | Mode d'échec n° 3 : une variable calculée que rien ne consomme ; le lien doit être la cause de l'atteignabilité |
| Un nom de mer inventé, traduit ou complété | Règle n° 10 : quand une donnée manque, l'agent l'invente par défaut ; l'absence doit être déclarable |
| Un plancher imposé sur le nombre de noms employés | Compteur en dur : le nombre de noms employés est un fait mesuré |
| Les noms hérités présentés comme frontières historiques ou source savante | Le proxy doit être déclaré comme tel |
| Une occurrence de la sous-chaîne `province` dans un artefact G4 autre que le fichier de divergence | ADR-0003 : la seconde réponse inscriptible au « où ? » est réintroduite |
| Zéro occurrence dans le fichier de divergence | La comparaison n'a pas eu lieu ; un contrôle qui ne peut rien rattraper ne prouve rien (règle n° 7) |
| Le fichier de divergence lu par un autre code, ou traité comme autorité spatiale | Frontière dure du brief, pas une préférence de style |
| Un `red_proof` vide avec `passed: true` | Règle n° 4 : un contrôle qui ne peut pas rougir ne prouve rien |
| Une paire d'empreintes inégale, vide, ou un total de paires nul | Le déterminisme n'est pas prouvé, il est espéré (mode d'échec n° 6) |
| L'empreinte du littoral employé par G4 différente de l'entrée déclarée de G3 | La mer et les cellules ne décrivent pas le même monde |
| Une valeur hexadécimale d'empreinte recopiée dans un test, un document ou un commentaire | Règle n° 12 : piège pour tout brief ultérieur, exactement ce qui est arrivé à l'empreinte citée par le brief 007 |
| Une preuve laissée dans un chemin ignoré par git, ou absente d'un clone frais | Une preuve qui n'existe que dans un répertoire de travail n'est pas une preuve |
| `pipeline/geo/.gitignore` assoupli pour faire entrer les preuves | Le mécanisme décidé est l'ajout forcé, pas l'assouplissement de la règle |
| Un README revendiquant le relief, le climat, les ressources, les fleuves ou le jalon E1 | Sur-revendication : ce lot est le premier lot d'E1 |
| Une justification (provenance des noms, règle de départage) écrite après la mesure | Calibration déguisée |
| Un couple `must_differ_from` manquant, identique, ou non déclaré | La porte ne peut pas deviner qu'un couple doit différer |
| Un compteur à `sample_size` nul ou à la sentinelle pour une affirmation réelle | Mode d'échec n° 6 : l'échantillon vide qui passe en silence |
| Un barème de jeu introduit (bonus, malus, pourcentage) | Principe n° 2 : le moteur raisonne en termes de monde, jamais en termes de règle de jeu |
| Un fichier modifié hors du périmètre de D16 | Périmètre explicite du brief |
| L'alias nu de l'interpréteur, ou un chemin `.venv/Scripts/` (Windows), dans une commande | Règle n° 1 ; la machine est Linux |
| Commit, poussée ou création de branche par le Générateur | Interdiction explicite ; l'orchestrateur seul dépose |
| Archives des briefs 001 à 018 modifiées, ou brief 007 réouvert | Archives intangibles ; 007a a déjà un verdict |
