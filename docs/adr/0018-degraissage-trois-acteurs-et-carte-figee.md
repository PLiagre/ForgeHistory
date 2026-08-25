# ADR-0018: le dégraissage — trois acteurs, carte figée, vraisemblable plutôt que véridique

**Date**: 2026-08-25
**Status**: accepted
**Deciders**: le propriétaire (décision du 2026-08-25), Claude (rédaction)

Amende ADR-0001, ADR-0002, ADR-0005 à ADR-0017. Ne remplace pas VISION.md,
ni les trois principes non négociables, ni la règle « celui qui produit ne
prononce pas la recevabilité de son propre travail ».

## Contexte

Le dépôt a été mesuré le 2026-08-25, sur `master` (`af39595`) :

| ce qui a été compté | valeur |
|---|---|
| outillage Python (`harness/` + `control-plane/`) | 28 774 lignes |
| le jeu (`sim/`) | 5 041 lignes |
| pipeline carte (`pipeline/geo/`) | 20 391 lignes, dont 2 697 de contrôles qualité |
| tests | 72 fichiers, 16 411 lignes — 35 fichiers testent le harnais, 23 le jeu |
| workflows GitHub | 12, soit 1 476 lignes de YAML |
| `unity/` en veille | 932 fichiers, 179 Mo |
| audits et contre-audits (`architecture/`) | 112 fichiers |

L'outillage pèse près de six fois le jeu.

Trois causes, constatées et non supposées :

1. **Les décisions se sont empilées sans jamais rien retirer.** ADR-0013 à
   ADR-0017 ont chacune changé l'organisation du travail ; aucune n'a
   supprimé le code de la précédente. Le dépôt porte quatre organisations
   superposées.
2. **Le harnais protège un flux qui n'existe plus.** ADR-0017 a sorti Claude
   du chemin quotidien. Les trois agents Claude, la commande `/forge-run`,
   le comptage de jetons Claude, le budget d'exécution et les sept commandes
   d'audit sont pourtant restés, maintenus et testés.
3. **L'exigence de source a transformé un décor en projet de recherche.**
   « Rien n'entre sans source vérifiable » a produit 1 110 tuiles d'altitude
   à télécharger, une cérémonie de preuve par étape géographique, et un
   relief calculé que `sim/` ne lit toujours pas.

## Décision

### 1. Trois acteurs, et rien d'autre

| acteur | ce qu'il fait | ce qu'il ne fait pas |
|---|---|---|
| **Hermes** (Sol 5.6, VPS) | tient la roadmap et le suivi ; **écrit les briefs** ; lance Cursor ; mesure et rend compte | ne code pas, ne fusionne pas, ne juge pas un lot |
| **Cursor** (Grok 4.6 pour le plan, Composer pour le code) | exécute le brief, ouvre la PR, se relit une fois dans une invocation neuve, itère jusqu'au vert | ne décide pas de ce qui est recevable |
| **Claude** (à la demande) | **architecte du modèle** : tient `sim/MODELE.md` ; **regard de dernier recours** quand un lot ne converge pas en trois itérations | n'a plus d'agent, plus de cron, plus de rôle dans le harnais |

**Hermes écrit désormais les briefs.** C'est le point qui change tout :
ADR-0016 le lui interdisait, et cette interdiction était la seule raison de
garder un rôle Planificateur séparé. Ce rôle disparaît.

Les rôles Planificateur / Générateur / Évaluateur d'ADR-0001 ne sont plus
trois agents. Il ne reste que la règle de fond, qui elle est conservée :
**celui qui produit ne prononce pas la recevabilité de son propre travail.**
Elle est tenue par la porte mécanique (`harness/verdict_audit.py`) et par la
relecture Cursor en invocation neuve.

Le processus complet tient en une ligne :

> Hermes écrit un brief → Cursor l'exécute et ouvre une PR → les tests
> passent et la porte mécanique vérifie le compte-rendu → le propriétaire
> fusionne.

### 2. Vraisemblable, pas véridique

La règle implicite « rien n'entre sans source » est remplacée par trois
niveaux explicites. Tout brief touchant au monde cite ce paragraphe.

- **Niveau 1 — juste dans les grandes lignes. Obligatoire.**
  La Méditerranée est là où elle est ; les Alpes sont des montagnes ;
  l'Égypte a un fleuve ; Paris, Venise et Constantinople existent, sont au
  bon endroit et sont grandes en 1400. Vérifié par une poignée de points de
  repère, pas par une campagne de contrôle qualité.

- **Niveau 2 — plausible. Généré, jamais sourcé.**
  Rendement des sols, gisements secondaires, population des villages, climat
  local, réseau de chemins. Produits par génération procédurale contrainte
  par le relief et la latitude. **Une anomalie à ce niveau n'est pas un
  défaut** et ne justifie ni correctif, ni audit, ni brief.

- **Niveau 3 — pas simulé.**
  Ce qui a besoin d'une source pour exister n'entre pas dans le jeu. On
  n'ouvre pas de campagne de collecte pour faire exister une donnée.

Conséquence sur les constantes : la règle « pas de nombre magique dans le
code » est conservée — elle est bon marché et utile. L'exigence de justifier
chaque constante par une source est abandonnée ; « ordre de grandeur
plausible » en commentaire suffit.

### 3. La carte est un artefact figé

`data/world-1400.json` est produit une fois, versionné dans le dépôt, et lu
par `sim/`. C'est la seule entrée géographique du jeu.

`pipeline/geo/` devient `tools/map/` : un outil qu'on ressort si on refait la
carte, hors du chemin quotidien. Ses sources téléchargeables ne sont plus
versionnées. Sa qualité garde les **invariants physiques** (adjacence
symétrique, pas de terre isolée en plein océan, conservation de la masse,
même graine = même monde) et abandonne les contrôles de véracité historique.

Le relief est consommé en **cinq classes** — plaine, colline, montagne, haute
montagne, marais — et non en mètres d'altitude. Aucune cérémonie de preuve
n'est requise pour le produire.

### 4. Les tests

Un test existe s'il protège l'une de ces trois choses, et seulement :

1. un **invariant physique** (la masse se conserve, l'adjacence est symétrique) ;
2. une **règle de jeu visible** (on ne mange pas deux fois, on meurt de faim) ;
3. le **déterminisme** (même graine, même monde).

Un test qui protège une étape de processus, un compteur de coût ou un mode
d'automatisation n'a plus de raison d'exister.

### 5. Ce qui est supprimé

Le pipeline full-auto, la machine d'états d'audit, le budget d'exécution, le
comptage de jetons, le bot de fusion, l'aiguillage de risque et de tests, les
trois agents Claude, les commandes d'audit et le backend Codex sont
supprimés. Les workflows GitHub passent de douze à deux : les tests, et le
scan de sécurité.

`unity/` et `architecture/` sortent de l'arbre de travail sous le tag
`archive/2026-08`. Les briefs terminés sont archivés.

Quatre documents restent vivants : `VISION.md` (gelé), `ROADMAP.md` (Hermes),
`AGENTS.md` (les règles, pour tous les agents), `hermes/DASHBOARD.md`
(généré).

### 6. Ce qui n'est pas touché

Les trois principes non négociables restent intacts : une seule source de
vérité ; le moteur raisonne en termes de monde, jamais de gameplay ;
l'économie est physique. Ils ne coûtent rien et font l'identité du jeu. Ce
qui coûtait cher, c'était la cérémonie de preuve construite autour.

## Alternatives considérées

### Retirer Claude complètement du projet
- **Pour** : un acteur de moins, zéro coût Claude.
- **Contre** : la conception du modèle retombe sur Hermes, déjà chargé du
  pilotage ; plus aucun avis qui n'ait pas vu le code se faire.
- **Pourquoi non** : la conception du modèle est le seul endroit où une
  erreur coûte des mois — si la règle de survie ou le modèle de commerce est
  faux, tout ce qui est construit dessus est à refaire.

### Garder le harnais et n'alléger que la simulation
- **Pour** : aucun risque de perdre une garde utile.
- **Contre** : c'est l'outillage, pas le jeu, qui pèse six fois trop lourd.
- **Pourquoi non** : ne traite pas la cause mesurée.

### Réécrire l'historique git pour récupérer les 179 Mo d'Unity
- **Pour** : un dépôt réellement plus léger au clonage.
- **Contre** : casse tous les checkouts existants et toutes les références
  de commits des ADR.
- **Pourquoi non** : le gain visé est l'attention des agents, pas les octets.
  À reconsidérer seulement si la taille devient gênante.

## Conséquences

### Positives
- Un agent qui démarre lit quatre documents au lieu de vingt-huit.
- Environ 18 000 lignes d'outillage et 11 000 lignes de test en moins.
- Une anomalie historique de niveau 2 cesse d'être un défaut à corriger :
  elle n'ouvre plus de brief.
- `sim/` lit un fichier, pas une chaîne de six étapes à rejouer.

### Négatives
- On ne saura plus dire ce qu'a coûté un lot : le comptage de jetons
  disparaît. Contrepartie assumée — le budget Claude a cessé d'être un
  critère de pilotage le 2026-08-20.
- Les gardes `full_auto` disparaissent avec le mode qu'elles protégeaient.
  Réactiver un jour l'automatisation de fusion demandera de les réécrire.

### Risques
- **Sortir `unity/` de l'arbre ne réduit pas le dépôt cloné** : git garde
  l'historique, les 179 Mo restent. Le gain est l'attention des agents.
  Dit ici pour que personne ne le découvre comme une mauvaise surprise.
- **Perte d'une garde qui servait vraiment.** Atténuation : la porte
  mécanique et la règle de la source unique d'instruction sont conservées,
  avec leurs tests. Ce sont elles qui ont été payées par de vrais défauts
  (nourriture comptée deux fois, seuil de survie ignoré).
- **La carte figée peut devenir périmée** si `tools/map/` évolue sans
  regénérer `data/world-1400.json`. Atténuation : le fichier porte la
  version du pipeline qui l'a produit ; un test le vérifie.
