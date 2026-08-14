# Amendement 001 — escalade sur l'empreinte du littoral (SC7)

**Authored**: 2026-08-14T10:45:00Z
**Author**: forge-planificateur

> **Note de transparence.** Le harnais tourne ici via Cursor Cloud : l'acteur
> réel de cet amendement est un sous-agent Cursor Cloud endossant le rôle natif
> `forge-planificateur`, sans aucun suffixe ajouté à la signature, pour que le
> contrôle mécanique `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.

**Répond à** : `verdict.md` (constat A, observations 1 et 2) et
`feedback/feedback-001.md` (point 1, explicitement adressé au Planificateur).

**Cet amendement est postérieur au code.** C'est assumé et daté : le trou de
planification qu'il répare n'était pas visible avant la mesure, mais il était
déjà écrit dans le brief. Il n'annule ni ne requalifie le verdict rendu ; celui-ci
vaut contre le texte qui existait quand il a été rendu.

---

## 1. Ce qui était contradictoire dans le brief

Trois passages du brief 019 disaient trois choses incompatibles au sujet du même
fait :

- **D2** annonçait, en cas d'écart entre l'empreinte du littoral corrigé de 1400
  relu par G4 et l'entrée que `MANIFEST_g3.json` déclare : « c'est un cas
  d'escalade (voir Waivers), pas un détail à ignorer ».
- **La table des dérogations recevables** ne portait **aucune ligne** pour cette
  affirmation, et se refermait sur « aucune autre dérogation n'est recevable ».
- **SC7** exigeait sèchement que le compteur
  `empreinte_terre_g4_egale_entree_g3` vaille `1`, et la table des échecs
  disqualifiants de la rubrique rangeait l'inégalité parmi les motifs de rejet.

Le renvoi de D2 pointait donc vers une porte qui n'existait pas. Un Générateur
honnête n'avait aucune issue : mesurer l'écart le faisait échouer sur SC7 ;
l'invoquer comme dérogation le faisait échouer sur une table fermée ; le faire
disparaître demandait de toucher G3, ce que D16 interdit. C'est un défaut du
brief, pas du travail — et c'est le Planificateur qui le répare.

---

## 2. Ce que l'Évaluateur a mesuré

L'Évaluateur a recalculé lui-même trois empreintes à l'exécution, sans en
recopier aucune, et cet amendement ne recopie non plus aucune valeur (règle
durement acquise n° 12) :

- l'empreinte du `pipeline/geo/artifacts/coastline_1400.json` régénéré est
  **égale** à la sortie que `pipeline/geo/artifacts/MANIFEST_g2b.json` déclare
  pour ce même fichier ;
- elle **diffère** de l'entrée que `pipeline/geo/artifacts/MANIFEST_g3.json`
  déclare sous `inputs.coastline_1400` ;
- les deux manifestes sont **antérieurs** au lot ; `MANIFEST_g3.json` est suivi
  par git et n'a aucune modification.

L'incohérence est donc antérieure au lot. Le Générateur ne l'a pas créée, et
aucune action dans le périmètre autorisé par D16 ne peut la résorber : le
compteur a été rapporté à `0` — un zéro **mesuré**, pas la sentinelle — avec un
constat ouvert. C'est le comportement attendu, et c'est ce qui déclenche la
présente escalade.

Deux hypothèses restent à départager, l'une comme l'autre mesurables :
`MANIFEST_g3.json` décrit un littoral que la chaîne ne produit plus, ou bien
`steps/02b_corrections_1400.py` a changé de sortie depuis que G3 a été joué sans
que G3 soit rejoué. Le `coastline_1400.json` dont G3 disposait n'est pas suivi
par git, donc seule la relecture de la chaîne peut trancher.

---

## 3. La décision

1. **G3 reste intouché.** Régénérer les cellules changerait probablement les
   596 cellules déjà consommées par `sim/` (briefs 012 et 018, jalon E2 clos).
   D16 continue de mettre les artefacts G3 en lecture seule.
2. **L'égalité n'est pas auto-accordée.** Le compteur reste le `0` mesuré. Ni un
   `1` maquillé, ni une comparaison retargetée vers `MANIFEST_g2b.json` pour
   « toucher » SC7, ni la sentinelle `-1`.
3. **L'escalade annoncée par D2 est reçue** : la table des dérogations du brief
   reçoit la ligne d'impossibilité qu'elle promettait, avec une commande
   rejouable et le message d'erreur exigé, l'un et l'autre **sans aucune valeur
   hexadécimale** (règles n° 9 et n° 12 tenues ensemble). Ce n'est pas le
   déplacement d'une borne après mesure : c'est l'ouverture de la porte que le
   brief avait annoncée et oublié de percer.
4. **La réparation de la provenance de G3 est un brief ultérieur dédié**, hors
   019. Elle devra trancher lequel des deux artefacts committés est faux, et à
   quel prix pour les consommateurs de la maille actuelle. Ce n'est **pas** une
   instruction pour le Générateur de ce lot.

---

## 4. Ce que SC7 exige désormais

SC7 devient une condition **à deux branches**, dont une seule est à satisfaire.

**Branche égale (monde unique).** `empreinte_terre_g4_egale_entree_g3` vaut `1` :
l'empreinte du littoral employé par G4, calculée à l'exécution, égale l'entrée
déclarée par `MANIFEST_g3.json`. Rien d'autre à faire.

**Branche escalade (chaîne amont incohérente).** Le compteur vaut `0`, et toutes
les exigences suivantes valent **ensemble** :

- ce `0` est une **mesure**, jamais la sentinelle `-1` ;
- la dérogation d'escalade de la table des dérogations est invoquée, avec sa
  commande rejouable et son message d'erreur, dépourvus de toute valeur
  hexadécimale, le message nommant ses **deux** sources ;
- `empreinte_terre_g4_egale_sortie_declaree_g2b` vaut `1` : le littoral relu est
  bien celui que l'étape qui le produit déclare, ce qui situe l'écart en amont
  du lot et non dans le lot ;
- aucun artefact G3 n'est réécrit, régénéré ni retouché ;
- la comparaison exigée par le compteur n'est **pas** retargetée : elle reste
  celle du littoral relu contre `MANIFEST_g3.json` ;
- le constat est **ouvert** — nommé dans le journal de preuve, dans
  `deliverables/generator-log.md` et dans `pipeline/geo/README.md`.

Cette branche **satisfait SC7 pour ce lot**. Elle ne vaut pas égalité : elle
n'autorise en aucun cas à écrire, dans un document ou dans un artefact, que la
mer et les cellules décrivent le même monde. Elle dit exactement le contraire,
et le dit à l'endroit prévu.

---

## 5. Ce qui reste disqualifiant

L'inégalité **mesurée et escaladée** n'est plus disqualifiante pour ce lot.
Reste disqualifiante l'inégalité **non documentée**, sous chacune de ces formes :

- `empreinte_terre_g4_egale_entree_g3` déclaré à `1` alors que la comparaison
  mesurée dit le contraire ;
- la comparaison retargetée vers `MANIFEST_g2b.json` pour faire dire `1` au
  compteur — renommer la cible pour la toucher ;
- un artefact G3 réécrit, régénéré ou aligné de quelque façon ;
- une valeur hexadécimale d'empreinte recopiée dans un document, un test, un
  commentaire ou le champ d'erreur d'une dérogation ;
- la sentinelle `-1` à la place du `0` mesuré, ou l'inverse ;
- l'écart passé sous silence : pas de dérogation invoquée, ou aucun constat
  ouvert dans le journal et le README.

---

## 6. Interdit au Générateur, explicitement

- Réécrire, régénérer ou « réparer » `MANIFEST_g3.json`, `cells_g3.json`,
  `stats_g3.json` ou `adjacency_g3.json`.
- Retargeter le compteur vers `MANIFEST_g2b.json`, ou rappeler `run_corrections`
  avec d'autres paramètres jusqu'à retomber sur l'empreinte que G3 déclare.
- Recopier une valeur hexadécimale d'empreinte, où que ce soit — y compris dans
  le champ `error` d'une dérogation du manifeste, dont la commande doit désormais
  produire un message qui n'en contient aucune.
- Déplacer une borne de `pipeline/geo/constants.py`, dans quelque sens que ce
  soit, pour quelque motif que ce soit.

---

## 7. Ce que cet amendement ne touche pas

- **Les horodatages `Authored` de `brief.md` (`2026-08-14T08:50:00Z`) et de
  `eval-rubric.md` (`2026-08-14T08:49:00Z`) sont inchangés.** La porte mécanique
  les compare aux dates de modification des livrables
  (`mtime_after_brief`, `rubric_predates_deliverables`) : les avancer ferait
  échouer la porte sur un lot dont la forme est saine.
- `pipeline/geo/constants.py` : aucune valeur, aucune borne. L'observation n° 1
  de l'Évaluateur — le semis qui sature sur `SEA_ZONE_COUNT_MAX` parce que les
  rayons ont été calibrés sur une fenêtre plus petite que celle du dépôt — est
  réelle et relève d'un brief ultérieur, pas de celui-ci.
- Le semis, les rayons, la fourchette de comptage : rien n'est recalibré.
- Le compteur `plans_eau_exclus_lacs` lui-même : seule la **formulation** de son
  dénominateur est corrigée dans le brief (observation n° 2 : le dénominateur
  écrit était plus petit que le compteur). Le compteur exigé, sa nature de fait
  mesuré et son absence de seuil ne changent pas, et rien n'est à corriger dans
  le code de mesure.
- Le périmètre de fichiers de D16 : inchangé. `pipeline.py`, `qa/checks.py`, les
  artefacts G3, `sim/` et `unity/` restent hors d'atteinte du Générateur.
- `verdict.md`, `feedback/` et `deliverables/` : non modifiés par cet amendement.
  Les seuls fichiers touchés sont `brief.md`, `eval-rubric.md` et celui-ci.

---

## 8. Attribution

C'est le Planificateur qui referme un trou de sa propre planification, à
l'endroit que l'Évaluateur a désigné et attribué au brief plutôt qu'au travail.
Aucune recevabilité n'est prononcée ici : la prochaine itération est jugée par
l'Évaluateur contre le `brief.md` et l'`eval-rubric.md` ainsi amendés.
