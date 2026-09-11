# Notice d'utilisation

> Tout ce qu'on peut lancer, dans l'ordre où on en a besoin. Chaque commande
> a été jouée pour écrire ce fichier ; les sorties citées sont réelles.

---

## 0. Ce dont on a besoin

| pour | il faut |
|---|---|
| le moteur et le tableau de bord | **Python 3.11+**, rien d'autre |
| la chronique | Python seul ; Chromium et ffmpeg **seulement** pour la vidéo |
| la carte 3D | `numpy`, `pillow`, `forge3d>=1.35.0`, et un GPU |
| les tests | `pytest` |
| le registre des lots | **ForgeAtelier** sur le `PYTHONPATH` |
| la ville | **Unity 6000.0.43f1** + URP 17.0.4, sur Windows |

Le moteur ne dépend de rien. C'est voulu : `python3 -m sim` doit tourner sur
une machine nue.

```bash
python3 -m pip install pytest                      # les tests
python3 -m pip install numpy pillow forge3d        # la carte 3D seulement
```

---

## 1. Lancer une simulation

```bash
# le monde à t0, sans jouer un seul tick — le test de fumée
python3 -m sim --ticks 0 --json
```

```json
{"cellules": 596, "cellules_affamees": 0, "kg_transportes": 0.0,
 "population_arrivee": 66649511, "population_depart": 66649511,
 "sans_unity": true, "seed": 0, "stock_kg_arrivee": 666495110.0,
 "stock_kg_depart": 666495110.0, "ticks": 0}
```

```bash
# une année simulée (~36 s)
python3 -m sim --ticks 365 --seed 0 --json

# une graine différente donne un autre monde ; la même graine, le même monde
python3 -m sim --ticks 60 --seed 7 --json
```

> **Ce que vous allez voir, et ce n'est pas une panne.** À 365 ticks la
> population passe de 66,6 M à 9,6 M. Le monde amorce plus de bouches que sa
> terre n'en nourrit — plafond de survie **0,691** au lieu de 1. C'est un
> défaut connu, chiffré, et c'est le premier lot de la V1 (brief 055).

```bash
# le mesurer soi-même
python3 -m pytest sim/tests/test_survie.py -k nourrit_pas_plus -q -s
# → plafond derive = 0.691386
```

---

## 2. Photographier le monde

Une **photographie** (snapshot) est un document JSON déterministe : la carte
figée, plus l'état que le moteur a fait évoluer. C'est le seul objet que les
vues lisent. Aucune vue ne parle au moteur.

```bash
python3 -m sim --ticks 0 --seed 0 --snapshot-json /tmp/monde.json
python3 -m sim --ticks 365 --seed 0 --snapshot-json /tmp/monde-an1.json
```

Une photographie pèse environ 2 Mo, dont ~95 % de géométrie qui ne bougera
jamais.

---

## 3. L'afficher — trois vues, une seule photographie

### 3a. Le tableau de bord 2D (aucune dépendance)

```bash
python3 -m viewer --snapshot /tmp/monde.json --port 8000
# puis http://localhost:8000
```

Le bandeau donne la population totale, le stock de nourriture et le nombre de
cellules affamées ; la carte colorie une couche à la fois.

```bash
# comparer deux instants
python3 -m viewer --snapshot /tmp/monde.json --compare /tmp/monde-an1.json

# une preuve SVG sans serveur, pour la CI
python3 -m viewer --snapshot /tmp/monde.json --proof-svg /tmp/preuve.svg --layer population
```

### 3b. La chronique — la suite des instants

`viewer/` montre **un** instant ; la chronique montre leur suite.

```bash
# dérouler 180 ticks, un instant tous les 4, et dessiner la planche
python3 -m chronique --ticks 180 --pas 4 --html /tmp/chronique.html

# garder la chronique pour la redessiner sans resimuler
python3 -m chronique --ticks 180 --pas 4 --json /tmp/chronique.json
python3 -m chronique --chronique /tmp/chronique.json --html /tmp/planche.html

# une page qui n'appelle personne (fontes du système)
python3 -m chronique --chronique /tmp/chronique.json --html /tmp/p.html --sans-reseau

# en faire une vidéo — demande Chromium et ffmpeg
python3 -m chronique --chronique /tmp/chronique.json --html /tmp/p.html \
        --bobine /tmp/monde.webm --bobine-lecture faim
```

Un instant précis se demande par l'adresse :
`planche.html?image=12&lecture=faim`. C'est ce qui permet de filmer la planche
sans la piloter, et de pointer un lien sur un instant.

**Pourquoi c'est léger** : la chronique sépare le **décor** (géométrie, relief,
climat, gisements — écrit une fois) de l'**image** (population, panier, faim,
dette — ce qui bouge). Cinquante instants ne coûtent pas cinquante fois deux
mégaoctets.

### 3c. La carte 3D (forge3d, demande un GPU)

```bash
# depuis une photographie déjà écrite
python3 -m visualisateur --snapshot /tmp/monde.json --png /tmp/monde-3d.png

# photographier puis rendre, en un seul geste
python3 -m visualisateur --ticks 0 --seed 0 \
        --png /tmp/monde-3d.png --apercu /tmp/monde-mnt.png
```

`--apercu` écrit une vue du dessus : c'est un contrôle du raster, **pas** un
rendu 3D. Utile quand on doute de la géométrie avant de payer un rendu.

Sans GPU la commande **refuse proprement** avec le code de retour 2. C'est
voulu : la CI ne prétend pas rendre ce qu'elle ne peut pas rendre.

> **Ce que la carte 3D ne fait pas encore.** Elle pose une altitude plausible
> par classe de relief et rend le terrain. Elle ne colorie **aucune donnée du
> monde** — ni population, ni faim, ni stock, ni bourg. La « carte de
> statistique » est le lot qui manque, et c'est une des quatre conditions de
> la V1.

---

## 4. Les tests

```bash
# tout ce qui ne demande ni GPU ni ForgeAtelier
python3 -m pytest sim/tests/ viewer/tests/ chronique/tests/ -q

# la chaîne d'intégration (demande ForgeAtelier sur le PYTHONPATH)
PYTHONPATH=/chemin/vers/ForgeAtelier python3 -m pytest outils/tests/ -q

# la carte 3D
python3 -m pytest visualisateur/tests/ -q
```

Relevé du 10 septembre 2026 : **560 tests verts**, plus 7 qui échouent
uniquement faute de ForgeAtelier sur le `PYTHONPATH` — avec lui, ils passent.

---

## 5. Le registre des lots

Le registre est la **seule** représentation qui fait autorité sur l'état d'un
lot. Une machine le lit, et refuse toute fiche mal formée, tout numéro
dupliqué, tout brief attendu qui manque, toute dépendance qui n'existe pas.

```bash
export PYTHONPATH=/chemin/vers/ForgeAtelier

# le registre est-il cohérent ?
python3 -m atelier feuille valider --projet .

# où en est chaque lot ?
python3 -m atelier feuille etat --projet .

# changer l'état d'un lot (--ecrire pour toucher le fichier)
python3 -m atelier feuille marquer --projet . --lot 051 --etat livre --pr 241
```

Aujourd'hui, sur `master` :

```
FAIL  briefs/055-le-monde-nourrit-ceux-qu-il-amorce.md — brief orphelin :
      aucune fiche ne le nomme
```

**Conséquence** : toute PR rejouée sur `master` échoue. Six PR sont bloquées.
C'est la phase 0 de la roadmap.

---

## 6. La chaîne d'intégration, jouée à la main

Ces commandes **décident** et n'écrivent jamais sur GitHub. Chacune imprime une
ligne sur la sortie standard — celle que le workflow lit — et son compte rendu
sur l'erreur standard.

```bash
# la PR a-t-elle été relue par un tiers, sur sa révision courante ?
python3 -m outils relecture --depot PLiagre/ForgeHistory --pr 246

# quelle PR entre dans master — ou RIEN
python3 -m outils integration --depot PLiagre/ForgeHistory --projet .

# une couche finie attend-elle son lot de stabilisation ?
python3 -m outils palier --projet .

# écrire la page « où en est le travail »
python3 -m outils tableau --depot PLiagre/ForgeHistory --projet . --sortie site/index.html

# les contrôles requis manquent-ils ? est-ce encore un brouillon ?
python3 -m outils controles --depot PLiagre/ForgeHistory --pr 246
python3 -m outils brouillon --depot PLiagre/ForgeHistory --pr 246
```

`GITHUB_TOKEN` doit être dans l'environnement pour tout ce qui lit GitHub.
`palier --ecrire`, `saisie --ecrire` et `etat --ecrire` sont les seules qui
touchent un fichier — et seulement celui du registre.

---

## 7. La ville, sous Unity

Unity n'est pas sur la VM Linux. Tout ce qui touche au jeu passe par une
machine Windows.

```
Unity     : 6000.0.43f1
Pipeline  : URP 17.0.4
Scène     : Assets/CityLabHost/Scenes/CityLab.unity
```

Au premier import : `Victoria > CityLab > Configure Project`. En batch :

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe' `
  -batchmode -quit -projectPath <racine> `
  -executeMethod Victoria.CityLab.Editor.CityLabProjectSetup.Configure
```

Les tests EditMode réels passent par le worker Windows :

```bash
gh workflow run unity-windows.yml --repo <depot> -f sha=<40-hex> -f ref_name=<branche>
python3 Tools/unity_nunit.py <editmode.xml> --summary /tmp/summary.json
```

**Commandes du prototype jouable** — `WASD` ou bords de l'écran pour déplacer
la caméra, molette pour zoomer, bouton droit pour tourner, `F` pour recentrer,
`R` puis deux clics pour tracer une route, `Z` puis un clic sur une route pour
créer des parcelles, `B` pour fonder un camp de bûcherons, `Échap` pour
annuler, `Espace` pour la pause, `1`/`2`/`3` pour la vitesse.

---

## 8. La fabrique d'assets (Blender, hors Unity)

Aucune de ces commandes ne lance Unity.

```bash
py Tools/AssetFactory/citylab_factory.py doctor          # Blender est-il là ?
py Tools/AssetFactory/citylab_factory.py scan            # inventaire par SHA-256
py Tools/AssetFactory/citylab_factory.py scan --check
py Tools/AssetFactory/citylab_factory.py recipe-check
py Tools/AssetFactory/citylab_factory.py admission-check --write-report
py Tools/AssetFactory/qa_factory_release.py              # la QA transversale
```

La publication est en **dry-run par défaut** : rien n'est copié tant que
`publication-check` n'est pas appelé avec `--publish`.

---

## 9. Les pièges qu'on rencontre vraiment

| symptôme | cause | remède |
|---|---|---|
| `AtelierAbsent` dans les tests `outils/` | ForgeAtelier n'est pas sur le `PYTHONPATH` | l'y mettre — 7 tests en dépendent |
| `feuille valider` refuse | brief orphelin, fiche sans brief, dépendance fantôme | le message nomme le fichier fautif |
| `visualisateur` sort en code 2 | pas de GPU, ou `forge3d` absent | c'est un refus propre, pas un bug |
| la population s'effondre | plafond de survie à 0,691 | connu, chiffré, c'est le brief 055 |
| `--bobine` échoue | Chromium ou ffmpeg manquant | `--chrome` et `--ffmpeg` pointent un binaire |
| `--ticks` négatif | refusé | code 2, volontairement |
| une PR verte n'entre pas | contrôle absent, ou approbation périmée par un nouveau commit | un contrôle absent n'est pas un contrôle vert |
