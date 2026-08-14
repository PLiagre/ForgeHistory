# Eval Rubric — Brief 020 : réparer la provenance du littoral de G3

**Authored**: 2026-08-14T12:02:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : le rôle signataire est le rôle natif du harnais
`forge-planificateur`. L'acteur réel est un sous-agent Cursor Cloud (modèle
Claude Opus 5), orchestré par un agent Cursor Cloud qui remplace le CTO Claude.
Aucun suffixe n'est ajouté à la signature : le contrôle mécanique
`verdict_is_not_self_authored` compare les acteurs de part et d'autre d'un lot,
et un couple de signatures suffixées serait refusé.

---

## Guide de lecture

Pour chaque condition de succès du `brief.md` :

- **Vérification** : commandes rejouables. Depuis la racine du dépôt avec
  `.venv/bin/python`, ou depuis `pipeline/geo/` avec `../../.venv/bin/python`.
  Jamais l'alias nu de l'interpréteur (règle durement acquise n° 1).
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur lui-même,
  depuis les fichiers du dépôt, **sans lire d'abord le manifeste du lot**. Un
  compteur qu'on ne peut pas re-dériver n'est pas une mesure.
- **Contre-preuve disqualifiante** : sabotage monté par l'Évaluateur dans une
  copie de travail **hors du dépôt**. Si le contrôle reste vert sous sabotage,
  la condition n'est pas satisfaite, même si tout le reste est vert.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire employé ci-dessous, expliqué une fois :

- **littoral vivant** : le fichier `pipeline/geo/artifacts/coastline_1400.json`
  tel que la chaîne le régénère aujourd'hui. Il est ignoré par git, donc absent
  d'un clone frais ; il se régénère depuis `pipeline/geo/` par
  `../../.venv/bin/python tests/run_proof_g2b.py`.
- **déclaration d'entrée** : la valeur que `artifacts/MANIFEST_g3.json` porte
  sous `inputs.coastline_1400`, c'est-à-dire « voici la terre qui a produit ces
  cellules ».
- **empreinte SHA256** : condensé d'un fichier, qui prouve que deux fichiers
  sont octet pour octet identiques. Elle se cite **par son nom de source**,
  jamais par sa valeur hexadécimale (règle n° 12) ; elle se compare à
  l'exécution.
- **écart de sérialisation** : deux fichiers décrivant la même géométrie aux
  epsilon près, mais dont les octets diffèrent (ordre, arrondi, mise en forme).
  À distinguer d'un écart de géométrie, où la terre elle-même a bougé.
- **maille gelée** : la maille des 596 cellules committées n'est pas rejouée par
  ce lot ; les identifiants que `sim/` consomme restent donc ceux du fichier
  committé, par construction et non par espoir.

Note d'environnement, à vérifier avant de conclure quoi que ce soit : la pile
scientifique vit dans `.venv/` à la racine. Les fichiers
`pipeline/geo/tests/test_qa_red_g*.py` ne sont pas collectés par `pytest` (ils
exposent des fonctions que les scripts de preuve importent) : une collecte à
zéro test dans ce répertoire n'est pas un défaut.

---

## SC1 — Le diagnostic est rejoué, et il conclut « sérialisation », pas « géométrie »

**Vérification :**

1. Régénérer le littoral vivant si le disque ne le porte pas, depuis
   `pipeline/geo/` :
   ```
   ../../.venv/bin/python tests/run_proof_g2b.py
   ```

2. Lire la sortie de la commande d'écart **avant** réparation, committée sous
   `deliverables/pre-edit/check_provenance_avant.txt` : elle doit porter le
   message d'écart nommant ses deux sources, sans aucune valeur hexadécimale, et
   le code de sortie 1 doit être consigné dans le journal.

3. Rejouer la même commande **après** réparation, depuis la racine :
   ```py
   .venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/check_provenance_coastline_020.py
   ```
   Code de sortie attendu : 0. Un code 2 signifie qu'une source est absente du
   disque (le littoral vivant et `MANIFEST_g2b.json` sont ignorés par git) : il
   faut régénérer puis rejouer, jamais conclure. Un code 2 n'excuse aucune
   condition.

4. Rejouer le script de mesure, depuis la racine :
   ```
   .venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/measure_g3_provenance_020.py
   ```
   La sortie doit nommer, chacun avec son dénominateur :
   `terre_vivante_m2`, `depassement_cellules_hors_terre_m2`,
   `terre_non_couverte_m2`, `epsilon_surface_g3_m2`, `ecart_est_serialisation`,
   `code_sortie_ecart_avant`, `code_sortie_ecart_apres`, `cellules_lues_g3`.

5. Vérifier que l'epsilon est **lue** et non recopiée :
   ```
   .venv/bin/python -c "import sys; sys.path.insert(0,'pipeline/geo'); import constants as c; print(c.G3_AREA_EPS_M2)"
   ```
   Puis relire le script de mesure, le script d'alignement et la garde durable :
   cette valeur ne doit apparaître en littéral dans aucun des trois (règles n° 2
   et n° 3 — un contrôle et un compteur dérivent, ils ne se nomment pas d'après
   leur cible).

**Reconstruction indépendante :**
L'Évaluateur refait la mesure géométrique lui-même, hors dépôt : il charge la
terre du littoral vivant, charge les géométries de `artifacts/cells_g3.json`,
calcule l'union des cellules, puis les deux aires — la part d'union qui sort de
la terre, et la part de terre qu'aucune cellule ne couvre. Il compare les deux à
`G3_AREA_EPS_M2` lue du fichier de constantes, et conclut lui-même « écart de
sérialisation » ou « écart de géométrie ». Il ne reprend aucun nombre du
manifeste du lot.

Il vérifie en outre que la surface de terre qu'il mesure est du même ordre que
`land_area_km2` du littoral vivant : une mesure faite sur une géométrie vide ou
sur une fenêtre tronquée donnerait un résidu trompeusement nul (mode d'échec
n° 6 — l'échantillon vide qui passe en silence).

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, retirer une cellule entière de `cells_g3.json` et
rejouer la mesure : `terre_non_couverte_m2` doit alors dépasser franchement
l'epsilon lue, et `ecart_est_serialisation` doit tomber à 0. Si le diagnostic
reste « sérialisation » après amputation d'une cellule, il ne mesure rien.

Est également disqualifiant : `ecart_est_serialisation` affirmé sans que les
deux aires soient imprimées avec leur dénominateur ; ou l'epsilon écrite en
littéral au lieu d'être lue.

**Résultat attendu :** PASS si le diagnostic est rejoué de bout en bout, si les
deux résidus mesurés restent sous l'epsilon lue, et si la commande d'écart passe
du code 1 avant réparation au code 0 après.

---

## SC2 — La maille n'a pas bougé et les identifiants consommés par `sim/` sont gelés

**Vérification :**

1. Vérifier que les quatre fichiers de la maille n'ont aucune modification :
   ```
   git status --porcelain -- pipeline/geo/artifacts/cells_g3.json pipeline/geo/artifacts/adjacency_g3.json pipeline/geo/artifacts/stats_g3.json pipeline/geo/registry/cell_registry.json
   ```
   Sortie attendue : vide.

2. Comparer l'instantané des identifiants actifs pris **avant** toute écriture à
   l'état du dépôt après le lot :
   ```
   .venv/bin/python -c "
   import json, pathlib
   snap = pathlib.Path('harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/pre-edit/cell_ids_actifs.txt')
   avant = {int(l) for l in snap.read_text(encoding='utf-8').split()}
   apres = {c['cell_id'] for c in json.load(open('pipeline/geo/artifacts/cells_g3.json'))['cells']}
   declare = json.load(open('pipeline/geo/artifacts/stats_g3.json'))['cell_count']
   print('instantane:', len(avant), '/ cell_count declare:', declare)
   print('inchangees:', len(avant & apres), '/', len(avant))
   print('ajoutees:', len(apres - avant), 'retirees:', len(avant - apres))
   "
   ```
   Attendu : instantané égal au `cell_count` déclaré, intersection égale à
   l'instantané, zéro ajout et zéro retrait. Ces zéros sont des **mesures** ; la
   sentinelle « non calculé » du projet est `-1` (règle n° 8) et ne doit
   apparaître pour aucun compteur calculé de ce lot.

3. Vérifier que `sim/` n'a pas été écrit :
   ```
   git status --porcelain -- sim/
   ```
   Sortie attendue : vide.

4. Suite de simulation non régressée :
   ```
   .venv/bin/python -m pytest sim/tests/ -q
   ```

**Reconstruction indépendante :**
L'Évaluateur relit `sim/world.py` et confirme lui-même ce que `sim/` consomme
réellement : `cell_id` et `area_km2` de `artifacts/cells_g3.json`, plus les
arêtes de `artifacts/adjacency_g3.json`. Aucune géométrie de littoral. Il en
déduit que la déclaration d'entrée d'un manifeste n'est pas une donnée consommée
par la simulation, et que l'aligner ne peut donc pas déplacer une cellule — à la
condition, vérifiée au point 1, que la maille n'ait effectivement pas été
rejouée.

Il vérifie enfin que le lot n'a lancé ni `run_cells`, ni
`tests/run_proof_g3.py`, à l'intérieur du dépôt : le journal doit être muet
là-dessus, et les quatre fichiers de maille sans diff.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, relancer la maille et comparer les identifiants
obtenus à l'instantané. Deux issues, toutes deux instructives : identiques, la
décision de ne pas remailler est confirmée sans coût ; différents, c'est un
**constat ouvert** que le journal doit porter — et en aucun cas une licence pour
remplacer les cellules committées, puisque la géométrie actuelle les recouvre
encore (SC1). Est disqualifiant : des artefacts issus d'une telle copie recopiés
dans le dépôt.

Est également disqualifiant : un `cells_g3.json`, `adjacency_g3.json`,
`stats_g3.json` ou `registry/cell_registry.json` modifié, même « à l'identique
après régénération » ; un fichier écrit sous `sim/`.

**Résultat attendu :** PASS si la maille est intacte, si les 596 identifiants de
l'instantané sont exactement ceux du dépôt après le lot, et si `sim/` est resté
en lecture seule avec sa suite verte.

---

## SC3 — `MANIFEST_g3.json` déclare enfin le littoral que la chaîne produit

**Vérification :**

1. Vérifier les deux égalités d'empreinte, **par calcul** et sans citer aucune
   valeur (règle n° 12), depuis la racine :
   ```py
   .venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/check_provenance_coastline_020.py
   ```
   Code de sortie attendu : 0, avec un message nommant ses sources.

2. Vérifier que les **sorties** déclarées par G3 sont restées justes — ce lot ne
   touche que l'entrée :
   ```
   .venv/bin/python -c "
   import hashlib, json, pathlib
   geo = pathlib.Path('pipeline/geo')
   man = json.load(open(geo/'artifacts'/'MANIFEST_g3.json'))
   ok = 0
   for rel, declare in sorted(man['outputs'].items()):
       vif = hashlib.sha256((geo/rel).read_bytes()).hexdigest()
       ok += (vif == declare)
       print(rel, 'conforme:', vif == declare)
   print('sorties_g3_conformes:', ok, '/', len(man['outputs']))
   "
   ```
   Attendu : toutes conformes. Aucune valeur n'est imprimée, seulement des
   résultats de comparaison.

3. Vérifier qu'**un seul** champ du manifeste a changé, en comparant à
   l'instantané pris avant édition :
   ```
   .venv/bin/python -c "
   import json, pathlib
   base = pathlib.Path('harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/pre-edit/MANIFEST_g3.json.orig')
   avant = json.loads(base.read_text(encoding='utf-8'))
   apres = json.loads(pathlib.Path('pipeline/geo/artifacts/MANIFEST_g3.json').read_text(encoding='utf-8'))
   def feuilles(o, p=''):
       if isinstance(o, dict):
           for k, v in o.items():
               yield from feuilles(v, p + '.' + str(k))
       elif isinstance(o, list):
           for i, v in enumerate(o):
               yield from feuilles(v, p + '[' + str(i) + ']')
       else:
           yield p, o
   a, b = dict(feuilles(avant)), dict(feuilles(apres))
   diff = sorted(k for k in set(a) | set(b) if a.get(k) != b.get(k))
   print('champs_manifeste_g3_modifies:', len(diff), '/', len(b))
   print('chemins:', diff)
   "
   ```
   Attendu : exactement un chemin, `.inputs.coastline_1400`. Les **noms** de
   chemins sont imprimés, jamais les valeurs.

4. Vérifier que l'horodatage figé du manifeste est conservé (aucune horloge
   murale dans un artefact) et que la sérialisation reste celle de la chaîne :
   ```
   .venv/bin/python -c "
   import json, pathlib
   p = pathlib.Path('pipeline/geo/artifacts/MANIFEST_g3.json')
   brut = p.read_bytes()
   d = json.loads(brut)
   canon = (json.dumps(d, ensure_ascii=False, separators=(',',':'), sort_keys=True) + '\n').encode('utf-8')
   print('fixed_timestamp:', d['fixed_timestamp'])
   print('serialisation canonique:', brut == canon)
   "
   ```

**Reconstruction indépendante :**
L'Évaluateur calcule lui-même, hors dépôt, l'empreinte du littoral vivant, puis
la compare à ce que `MANIFEST_g3.json` déclare en entrée et à ce que
`MANIFEST_g2b.json` déclare en sortie pour ce même fichier. Il conclut sur les
deux comparaisons et ne les cite, dans son verdict comme dans son feedback, que
par nom de source et par résultat. Une reconstruction qui se contenterait de
relire le compteur du manifeste ne mesurerait rien.

Il relit ensuite le script d'alignement et vérifie que la valeur écrite est
**calculée à l'exécution** depuis le fichier vivant, et non recopiée depuis un
littéral, depuis `MANIFEST_g2b.json` ou depuis `MANIFEST_g4.json` : une valeur
copiée d'un autre manifeste réussirait la comparaison sans jamais avoir lu la
terre.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, modifier un octet du littoral vivant puis rejouer la
commande d'écart : elle doit repasser au code 1. Si elle reste à 0, elle ne lit
pas le fichier qu'elle prétend lire.

Est également disqualifiant : une valeur hexadécimale d'empreinte recopiée dans
le script d'alignement, dans un test, dans un document ou dans le champ `error`
d'une dérogation ; l'égalité obtenue en changeant de cible (comparer G4 à G2b
plutôt que d'aligner G3) ; plus d'un champ modifié dans `MANIFEST_g3.json` ;
un bloc `outputs` de G3 retouché.

**Résultat attendu :** PASS si l'entrée déclarée par G3 égale le littoral vivant,
qui égale lui-même la sortie déclarée par l'étape qui le produit, et si rien
d'autre n'a bougé dans le manifeste.

---

## SC4 — G4 relit la provenance réparée, sans que son graphe bouge d'un octet

**Vérification :**

1. Vérifier les trois champs de provenance, par comparaison et sans impression
   de valeur :
   ```
   .venv/bin/python -c "
   import json, pathlib
   art = pathlib.Path('pipeline/geo/artifacts')
   g3 = json.loads((art/'MANIFEST_g3.json').read_text(encoding='utf-8'))
   g4 = json.loads((art/'MANIFEST_g4.json').read_text(encoding='utf-8'))
   st = json.loads((art/'stats_g4.json').read_text(encoding='utf-8'))
   print('provenance_g4_egale_entree_g3:', int(g4['coastline_1400_sha_declared_by_g3'] == g3['inputs']['coastline_1400']))
   print('inputs_g4_egale_entree_g3:', int(g4['inputs']['coastline_1400'] == g3['inputs']['coastline_1400']))
   print('drapeau_egalite_manifeste_g4:', g4['coastline_1400_sha_equal'])
   print('drapeau_egalite_stats_g4:', st['coastline_1400_sha_equals_g3_input'])
   "
   ```
   Attendu : les quatre lignes à 1.

2. Vérifier que les sorties déclarées par G4 décrivent les fichiers réellement
   présents (même boucle que SC3 point 2, sur `MANIFEST_g4.json`).

3. Vérifier que le graphe G4 est **octet-identique** :
   ```
   git status --porcelain -- pipeline/geo/artifacts/sea_zones_g4.json pipeline/geo/artifacts/adjacency_g4.json pipeline/geo/artifacts/topology_links_g4.json pipeline/geo/artifacts/adjacency_divergence_g4.json pipeline/geo/registry/sea_zone_registry.json pipeline/geo/capture pipeline/geo/logs/v1_050_qa.json
   ```
   Sortie attendue : vide. Seuls `stats_g4.json` et `MANIFEST_g4.json` avaient le
   droit de changer.

4. Vérifier que `stats_g4.json` n'a changé que d'un champ, par la même
   comparaison de feuilles que SC3 point 3, contre
   `deliverables/pre-edit/stats_g4.json.orig`. Attendu : exactement
   `.coastline_1400_sha_equals_g3_input`.

**Reconstruction indépendante :**
L'Évaluateur re-dérive lui-même les compteurs de graphe depuis
`artifacts/adjacency_g4.json` et `sea_zones_g4.json` — nombre de zones, comptes
par type d'arête — et vérifie qu'ils coïncident avec ceux que
`pipeline/geo/README.md` rapportait **avant** ce lot. Un semis rejoué, un
recalcul d'arêtes ou une saturation « améliorée » se verraient ici, même si les
diffs avaient été maquillés.

**Contre-preuve disqualifiante :**
Dans une copie hors dépôt, remettre l'ancienne déclaration d'entrée dans
`MANIFEST_g3.json` et rejouer la garde durable : les champs de provenance de G4
doivent redevenir incohérents et la garde rougir. Une garde qui reste verte quand
la déclaration ment ne garde rien (règle n° 7 — la présence n'est pas la
fonction).

Est également disqualifiant : `sea_zone_count`, `by_kind` ou toute borne de
`constants.py` touchée « au passage » ; un semis de zones rejoué ; une capture
G4 régénérée.

**Résultat attendu :** PASS si les champs de provenance de G4 décrivent le G3
réparé, si les empreintes de sortie réécrites correspondent aux fichiers, et si
le graphe lui-même n'a pas bougé d'un octet.

---

## SC5 — La garde durable existe, elle a été vue rougir, et l'alignement est déterministe

**Vérification :**

1. Rejouer la garde, depuis `pipeline/geo/` :
   ```
   ../../.venv/bin/python tests/run_proof_coastline_provenance.py
   ```
   Code de sortie attendu : 0. Sa sortie verte est committée sous
   `logs/v1_051_provenance_vert.txt`.

2. Lire la preuve rouge committée sous `logs/v1_051_provenance_rouge.txt` :
   elle doit provenir d'un sabotage monté **hors du dépôt** (déclaration d'entrée
   mutée), montrer un code de sortie non nul, nommer ses sources et ne contenir
   aucune valeur hexadécimale. Vérifier que les deux sorties diffèrent
   réellement et qu'elles sont déclarées en couple `must_differ_from` dans
   `deliverables/manifest.json`.

3. Refaire soi-même le sabotage, hors dépôt, et confirmer que la garde rougit.
   Une preuve rouge recopiée sans être reproductible n'est pas une preuve
   (règle n° 4).

4. Vérifier le déterminisme de l'alignement : le relancer une seconde fois,
   depuis `pipeline/geo/`,
   ```
   ../../.venv/bin/python steps/03b_align_coastline_provenance.py
   ```
   puis, depuis la racine :
   ```
   git status --porcelain -- pipeline/geo/artifacts
   ```
   Sortie attendue : vide. Une seconde passe qui ne produit aucune différence est
   l'état vert attendu, pas le signe que rien n'a tourné — le point 1 le montre
   déjà.

5. Vérifier que la garde est nommée d'après ce qu'elle **dérive** (la provenance
   du littoral), et non d'après le fichier qu'elle surveille (règle n° 2).

**Reconstruction indépendante :**
L'Évaluateur lit le code de la garde et vérifie qu'elle recalcule l'empreinte du
littoral vivant à chaque exécution, qu'elle lit les déclarations depuis les
manifestes du disque, et qu'elle ne porte aucune valeur attendue en dur. Une
garde qui compare une déclaration à une constante écrite dans son propre code se
contrôle elle-même (mode d'échec n° 6).

Il vérifie aussi le cas d'absence : en déplaçant hors du dépôt le littoral vivant
(ignoré par git), la garde doit sortir avec le code 2 et nommer la commande qui
le régénère — jamais 1, pour qu'une absence ne soit jamais confondue avec un
écart mesuré (règle n° 10 : l'absence doit être déclarable, et le code doit
refuser de deviner).

**Contre-preuve disqualifiante :**
Une garde qui reste verte quand la déclaration d'entrée est mutée. Une garde
placée de telle sorte qu'elle ne s'exécute jamais (règle n° 5 : une garde placée
après l'effet qu'elle doit prévenir ne protège rien) : vérifier qu'elle est
lançable seule, par la commande du point 1, sans dépendre d'un autre script.

Est également disqualifiant : `pipeline/geo/qa/checks.py` modifié pour héberger
la garde alors que le brief tranche pour un script de preuve dédié ; une preuve
rouge obtenue en mutant le code de la garde plutôt que la déclaration.

**Résultat attendu :** PASS si la garde est verte sur le dépôt réparé, rouge sous
sabotage hors dépôt, et si une seconde passe d'alignement ne change aucun octet.

---

## SC6 — Le README ferme le constat de 019 sans rien sur-revendiquer

**Vérification :**

1. Lire la section « Constats ouverts » de `pipeline/geo/README.md` : l'entrée
   sur l'empreinte du littoral doit y avoir disparu en tant que constat
   **ouvert**, et être remplacée par une mention **fermée** disant ce qui a été
   réparé et comment (la déclaration d'entrée de G3 alignée sur le littoral que
   la chaîne produit, la maille non rejouée, la géométrie inchangée aux epsilon
   près). L'entrée sur les bornes d'intention de surface et de compacité, elle,
   **reste ouverte** : ce lot ne la traite pas.

2. Compter les constats ouverts avant et après :
   ```
   .venv/bin/python -c "
   import pathlib, re
   def compte(p):
       t = pathlib.Path(p).read_text(encoding='utf-8')
       bloc = t.split('Constats ouverts', 1)[1].split('\n## ', 1)[0]
       return len(re.findall(r'^- \*\*', bloc, re.M))
   avant = compte('harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/pre-edit/pipeline-geo-README.md.orig')
   apres = compte('pipeline/geo/README.md')
   print('constats_ouverts_README:', apres, '/ avant:', avant)
   "
   ```
   Attendu : strictement moins de constats ouverts après qu'avant.

3. Vérifier qu'aucune sur-revendication n'a été introduite : le README ne doit
   affirmer ni que le jalon E1 est clos, ni que le relief, le climat, les
   ressources, les fleuves ou les villes sont livrés, ni que la mer est
   « simulée ». La section « Not yet landed » doit rester complète.

4. Vérifier le couple d'instantanés :
   ```
   .venv/bin/python -c "
   import hashlib, pathlib
   a = pathlib.Path('harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/pre-edit/pipeline-geo-README.md.orig').read_bytes()
   b = pathlib.Path('pipeline/geo/README.md').read_bytes()
   print('different:', hashlib.sha256(a).hexdigest() != hashlib.sha256(b).hexdigest())
   "
   ```
   Attendu : `different: True`, et le couple déclaré en `must_differ_from`.

5. Vérifier que le README reste **descriptif** — il dit ce qui existe, il
   n'adresse aucune instruction à un agent :
   ```
   .venv/bin/python -m pytest harness/tests/test_single_source_of_instruction.py -q
   ```

**Reconstruction indépendante :**
L'Évaluateur compare, phrase par phrase, la nouvelle mention du README à ce que
les mesures de SC1, SC3 et SC4 établissent réellement. Une phrase qui dit plus
que ce qui a été mesuré est une sur-revendication, même si elle est vraie ; une
phrase qui affirme que la mer et les cellules décrivent le même monde n'est
recevable que parce que SC1 a mesuré que la terre n'a pas bougé — et le README
doit dire cela, pas se contenter de l'égalité d'empreinte.

**Contre-preuve disqualifiante :**
Un README qui déclare le constat fermé alors que la commande d'écart sort encore
au code 1. Vérifier l'ordre : la mesure d'abord, la déclaration ensuite. Une
fermeture écrite avant la mesure est une calibration déguisée.

Est également disqualifiant : le jalon E1 déclaré clos ; le brief 007 rouvert ;
l'entrée « bornes d'intention » supprimée du README alors qu'elle n'a pas été
traitée.

**Résultat attendu :** PASS si le constat de 019 est fermé honnêtement, si la
liste des non-livrés reste complète, et si le README reste descriptif.

---

## SC7 — Mesure rejouable, manifeste complet, périmètre tenu, suites vertes

**Vérification :**

1. Rejouer le script de mesure depuis la racine :
   ```
   .venv/bin/python harness/queue/briefs/020-geo-provenance-littoral-g3/deliverables/measure_g3_provenance_020.py
   ```
   Chaque compteur du tableau du brief doit être imprimé **avec son
   dénominateur**. Un compteur sans dénominateur est irrecevable. Vérifier que le
   script **lit** les artefacts et les constantes : un script qui imprimerait des
   valeurs écrites à la main est un compteur en dur (mode d'échec n° 5).

2. Vérifier que chaque compteur du manifeste porte un `sample_size` réel, non nul
   et différent de la sentinelle (contrôle mécanique `no_empty_sample_pass`), et
   que les cinq couples `must_differ_from` y sont déclarés (contrôle
   `captures_differ_when_should` — un couple non déclaré n'est pas vérifié).

3. Vérifier qu'aucune valeur hexadécimale d'empreinte n'est citée :
   ```
   rg -n "[0-9a-f]{32,}" harness/queue/briefs/020-geo-provenance-littoral-g3 pipeline/geo/README.md pipeline/geo/steps/03b_align_coastline_provenance.py pipeline/geo/tests/run_proof_coastline_provenance.py pipeline/geo/logs/v1_051_provenance_vert.txt pipeline/geo/logs/v1_051_provenance_rouge.txt
   ```
   Attendu : aucune correspondance, **à l'exception** des instantanés
   `deliverables/pre-edit/MANIFEST_g3.json.orig` et
   `deliverables/pre-edit/stats_g4.json.orig`, qui sont des copies machine
   d'artefacts et non des citations. Toute autre correspondance est
   disqualifiante (règle n° 12).

4. Vérifier qu'aucune commande n'emploie l'alias nu de l'interpréteur ni un
   chemin `.venv/Scripts/` (Windows) : la machine est Linux (règle n° 1).

5. Vérifier le périmètre : toute ligne de
   ```
   git status --porcelain
   ```
   doit correspondre à un chemin autorisé par le brief. Un fichier sous `sim/`,
   `unity/`, `harness/*.py`, `docs/adr/`, `architecture/`, `ROADMAP.md`,
   `HANDOFF.md`, `VISION.md`, `.github/`, ou sous les archives des briefs 001 à
   019, est disqualifiant. `pipeline/geo/constants.py`, `qa/checks.py`,
   `pipeline.py`, `steps/02_coastline.py`, `steps/02b_corrections_1400.py`,
   `steps/03_cells.py` et `steps/04_adjacency.py` doivent être sans diff.

6. Vérifier que chaque preuve déclarée sous `pipeline/geo/` est suivie par git
   malgré la règle d'exclusion :
   ```
   git ls-files pipeline/geo/logs pipeline/geo/artifacts
   ```
   Un fichier déclaré mais absent de cette liste est une preuve qu'un clone ne
   peut pas revérifier. La porte mécanique ne vérifie pas le suivi des chemins
   qui sortent du dossier du brief : ce contrôle-ci existe pour cela.

7. Suites vertes :
   ```
   .venv/bin/python -m pytest harness/tests/ -q
   ```
   ```
   .venv/bin/python -m pytest sim/tests/ -q
   ```
   Aucun `FAILED`. Les `SKIP` propres à Linux (tests Unity) sont acceptés et
   doivent être déclarés dans le journal.

8. Registre de coût :
   ```
   .venv/bin/python harness/backends/ledger.py report
   ```
   Le brief `020-geo-provenance-littoral-g3` doit apparaître avec au moins
   `cursor=1`, et la dernière ligne de `harness/queue/cost-ledger.jsonl` doit
   porter `"event": "generator-run"`, `"backend": "cursor"` et un chemin de brief
   contenant `020`.

9. Vérifier que le Générateur n'a **ni committé, ni poussé, ni créé de branche** :
   la branche courante est celle fournie par l'orchestrateur, et l'historique ne
   contient aucun commit signé du Générateur.

**Reconstruction indépendante :**
L'Évaluateur re-dérive lui-même au moins huit compteurs du tableau, choisis dans
des familles différentes (diagnostic géométrique, identifiants gelés, provenance
G3, provenance G4, déterminisme, README, suivi git), et compare aux valeurs du
manifeste sans les avoir lues d'abord. Un écart, même d'une unité, est un écart :
le manifeste décrit alors autre chose que ce que le dépôt contient.

**Contre-preuve disqualifiante :**
Un compteur du manifeste que le script de mesure ne produit pas, ou qu'on ne peut
pas re-dériver depuis les fichiers du dépôt. Un `sample_size` nul ou à la
sentinelle pour une affirmation réelle. Un zéro rapporté avec la sentinelle alors
qu'il a été mesuré, ou l'inverse (règle n° 8).

**Résultat attendu :** PASS si tout se rejoue, si tous les compteurs sont
échantillonnés et re-dérivables, si les deux suites restent vertes et si le
périmètre a été tenu.

---

## Porte mécanique

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/020-geo-provenance-littoral-g3
```

Doit répondre `VERDICT: ACCEPT`, tous contrôles applicables au vert, avant que
l'Évaluateur ne rédige son verdict de fond.

**Avertissement :** la porte juge la forme du lot, pas sa substance. Un lot peut
obtenir `ACCEPT` de la porte et `FAIL` de l'Évaluateur. En particulier, la porte
ne vérifie pas le suivi git des fichiers déclarés **hors** du dossier du brief :
c'est SC7 qui le fait, à la main.

---

## Échecs disqualifiants

| Comportement | Raison |
|---|---|
| La maille G3 rejouée, régénérée ou remplacée dans le dépôt | La géométrie actuelle recouvre encore la terre aux epsilon près : remailler pour réparer une déclaration détruirait des identifiants consommés par `sim/` sans nécessité mesurée |
| Un fichier écrit sous `sim/` | Ce lot est en lecture seule sur la simulation ; le jalon E2 est clos |
| L'égalité obtenue en changeant de cible (comparer à `MANIFEST_g2b.json`) au lieu d'aligner l'entrée de G3 | Renommer la cible pour la toucher ; c'est exactement ce que 019 interdisait déjà |
| Une valeur hexadécimale d'empreinte recopiée dans un document, un test, un commentaire ou un champ `error` | Règle n° 12 : une empreinte citée par valeur piège tous les briefs suivants |
| Une valeur de `pipeline/geo/constants.py` modifiée, dans quelque sens que ce soit | Une borne déplacée après mesure n'est plus une borne |
| Le graphe G4 régénéré (semis rejoué, arêtes recalculées, `sea_zone_count` « amélioré », saturation recalibrée) | Ce lot met à jour les champs que G4 **lit**, il ne rejoue pas G4 |
| `pipeline/geo/qa/checks.py`, `pipeline.py`, `steps/03_cells.py` ou `steps/04_adjacency.py` modifiés pour autre chose que l'exposition d'une relecture de provenance déjà existante | Un contrôle rendu vert en modifiant le contrôle ne contrôle plus rien (règle n° 5) |
| Le jalon E1 revendiqué clos, ou le brief 007 rouvert | Sur-revendication ; archives intangibles |
| L'alias nu de l'interpréteur, ou un chemin `.venv/Scripts/` (Windows), dans une commande | Règle n° 1 ; la machine est Linux |
| Commit, poussée ou création de branche par le Générateur | Interdiction explicite ; l'orchestrateur seul dépose |
| La sentinelle `-1` à la place d'un zéro **mesuré**, ou l'inverse | Règle n° 8 : un zéro peut être une mesure réelle |
| `ecart_est_serialisation` affirmé sans les deux aires mesurées et l'epsilon **lue** | Règle n° 3 : un compteur dérive ; sinon le diagnostic se contrôle lui-même |
| Un débordement de cellules hors terre supérieur à l'epsilon lue, traité par un remaillage au lieu d'une escalade | La planification a mesuré le contraire ; si la mesure la contredit, c'est le Planificateur qui tranche, pas le Générateur |
| Une garde durable qui ne peut pas rougir, ou dont la preuve rouge vient d'une mutation de son propre code | Règle n° 4 : un contrôle qui ne peut pas rougir ne prouve rien |
| Une garde nommée d'après le fichier qu'elle surveille plutôt que d'après ce qu'elle dérive | Règle n° 2 |
| Un code de sortie 2 (source absente) présenté comme un écart, ou comme une excuse pour une condition | Règle n° 10 : l'absence doit être déclarable et ne doit jamais être devinée |
| Une preuve laissée dans un chemin ignoré par git, ou absente d'un clone frais | Une preuve qui n'existe que dans un répertoire de travail n'est pas une preuve |
| `pipeline/geo/.gitignore` assoupli pour faire entrer les preuves | Le mécanisme décidé est l'ajout forcé, pas l'assouplissement de la règle |
| Un couple `must_differ_from` manquant, identique, ou non déclaré | La porte ne peut pas deviner qu'un couple doit différer |
| Un compteur à `sample_size` nul ou à la sentinelle pour une affirmation réelle | Mode d'échec n° 6 : l'échantillon vide qui passe en silence |
| Un barème de jeu introduit (bonus, malus, pourcentage) | Principe n° 2 : le moteur raisonne en termes de monde, jamais en termes de règle de jeu |
| Un fichier modifié hors du périmètre du brief | Périmètre explicite |
