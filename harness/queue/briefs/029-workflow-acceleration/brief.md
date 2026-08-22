# Brief 029 — Accélération du workflow Hermes / ForgePilot / Cursor

**Risque : R2.** Ce lot touche l’orchestration, la CI, la production des
preuves et le traitement de données DEM. Il ne change pas les principes de
simulation ni le droit de fusion du propriétaire.

## Problème mesuré

Le workflow nominal est décrit comme adaptatif R0/R1/R2, mais l’exécutable
ForgePilot reste séquentiel et ne sait ni reprendre un lot, ni transmettre le
feedback d’une revue, ni publier une preuve liée au SHA. La CI d’une branche
ouverte s’exécute deux fois (`push` et `pull_request`). La preuve G6 effectue
des millions de lectures raster 1×1 et dure plus d’une heure sur le correctif
du lot 024. Les revues reçoivent des diffs comprenant des artefacts générés et
des conclusions du producteur.

## Autorité et frontières

- Hermes pilote et rend compte ; il ne juge pas.
- Claude planifie et évalue dans deux invocations distinctes en lecture seule.
- Cursor exécute dans un worktree `agent/*`.
- Le propriétaire conserve le veto de fusion.
- `sim/` reste l’unique simulation vivante ; Unity reste en veille.
- Une preuve absente, périmée ou visant un autre SHA ne vaut pas vert.
- Ce lot ne livre aucune nouvelle mécanique de monde et aucun client visuel.

## SC0 — État de référence

La branche part de `origin/master` à jour. Les suites `control-plane`,
`harness` et `sim` sont mesurées avant modification. Tout échec préexistant est
distingué d’une régression du lot.

## SC1 — Politique de workflow autoritaire

Un fichier versionné unique sous `control-plane/` décrit les profils R0, R1 et
R2, avec pour chaque rôle le backend, le modèle éventuel, l’effort éventuel,
le profil de tests et les délais. Le code charge et valide ce fichier.

Le classement mécanique peut augmenter le risque demandé, jamais le diminuer.
Les chemins de gouvernance, sécurité, provenance, sources DEM, données massives
et contrôles fondamentaux imposent R2. R0 est une allowlist documentaire
étroite. Tout autre changement est R1.

`forgepilot doctor` et l’aperçu d’un lot affichent la politique effective.
Une configuration invalide ou un backend incompatible est refusé avant tout
agent.

## SC2 — Exécution durable et reprenable

Chaque lot possède sous `.forgepilot/runs/` un identifiant stable et un état
JSON atomique contenant au minimum : base SHA, head SHA si disponible, risque,
étape, rôle actif, modèles effectifs, timestamps, durées, worktree, branche,
PR, preuves et erreur éventuelle.

ForgePilot expose des commandes pour démarrer, inspecter et reprendre une
exécution. Une reprise ne recrée ni branche ni worktree existants et repart de
la première étape incomplète. Un plan `blocked: true` arrête l’exécution avant
Cursor. Les écritures d’état sont atomiques et testées contre interruption.

## SC3 — Sorties observables et délais par rôle

Les processus agents peuvent produire un flux JSON ligne par ligne sans garder
toute leur sortie en mémoire. Chaque changement d’étape est persisté avant
l’effet suivant. Les délais du planificateur, de l’exécuteur, du reviewer et
des preuves sont distincts ; aucune preuve déterministe longue ne dépend du
délai d’une invocation LLM.

Les secrets et le corps intégral des prompts ne sont jamais écrits dans l’état
ou les journaux. Un résultat incomplet ou un JSON invalide est un refus visible.

## SC4 — Itération dirigée par le feedback

Une revue en échec produit un fichier de feedback structuré et lisible. La
commande d’itération le transmet à Cursor et reprend, lorsque le CLI le permet,
la session de l’exécutant. Elle ne rejoue pas silencieusement le plan initial.

Après correction, les tests ciblés précèdent la publication. Le reviewer juge
le delta et la résolution de ses constats ; une revue complète ne repart que
si l’approche a changé. Deux itérations sans amélioration arrêtent le lot.

## SC5 — Périmètre de publication et revue indépendante

Avant commit, ForgePilot compare les chemins modifiés à
`files_allowed_to_change` et refuse tout écart. Il n’emploie pas `git add -A`.

Le bundle de revue contient les SHA, le plan, le diff des fichiers écrits à la
main, la liste et les empreintes des artefacts générés, et les résultats
mécaniques synthétiques. Les conclusions du producteur sont exclues de la
première lecture. Le bundle est borné ; un dépassement demande une scission ou
un accès ciblé, jamais une troncature silencieuse.

La sortie du reviewer est archivable comme preuve liée au head SHA et peut être
rendue visible sur la PR. ForgePilot sait produire le matériau d’un
`verdict.md` sans permettre au reviewer de modifier le code.

## SC6 — Profils de tests fast / pr / certify

Un routeur déterministe choisit des commandes de tests depuis les chemins
modifiés :

- `fast` pour la boucle Cursor ;
- `pr` avant ou juste après publication ;
- `certify` une fois sur le SHA final pour les lots qui l’exigent.

Le routeur est testable sans lancer les suites lourdes. Il échoue fermé quand
un chemin sensible n’a aucune règle. Les commandes produisent un résumé JSON
avec code, durée et preuve ciblée.

## SC7 — CI sans doublons et checks non ambigus

Sur une branche avec PR, les workflows portables ne produisent qu’une série de
checks : `pull_request` sur la branche et `push` limité à `master`. Les noms de
jobs obligatoires sont uniques (`harness-tests`, `sim-tests`,
`forgepilot-tests`, `audit-schema`, `audit-check`, `actionlint`, `gitleaks`,
`risk-gate`).

Le check de risque confirme mécaniquement que le niveau déclaré n’est pas
inférieur au niveau dérivé. Les workflows historiques en mode manuel ne sont
pas réactivés. Aucun runner auto-hébergé générique n’est ajouté au dépôt
public.

## SC8 — Contexte Cursor borné

Un `.cursorignore` versionné exclut de l’index courant les captures Unity
gelées, les sorties visuelles générées et les anciennes preuves volumineuses,
sans masquer les règles, les briefs actifs, `sim/`, le code géographique ou le
code de contrôle.

## SC9 — Cache DEM partagé et honnête

Le cache DEM peut être placé hors du worktree par variable d’environnement ou
configuration, avec l’emplacement historique comme repli. Sa clé ou son
répertoire comprend l’empreinte des sources. Les téléchargements concurrents
sont verrouillés et les empreintes restent vérifiées. Aucun cache périmé ne
peut être accepté parce qu’il existe.

Les tests couvrent le repli historique, le chemin partagé, l’invalidation et
un fichier hors lock. Aucun raster DEM n’est committé.

## SC10 — Échantillonnage G6 groupé

La collecte des altitudes groupe les requêtes par tuile et évite une lecture
Rasterio 1×1 par point. Elle conserve exactement la règle de domaine, les
compteurs, les vrais zéros, nodata, les contrôles de bornes et l’ordre
déterministe.

Une petite preuve sentinelle couvre plusieurs reliefs, une côte, une plaine,
les frontières de degré, nodata et les vrais zéros connus. Elle est utilisable
dans `fast` ou `pr` sans parcourir toute l’Europe.

La certification complète peut figer une table de mesures dont la clé dérive
des empreintes de `sources.lock`, `cells_g3.json`, `adjacency_g5.json`, du code
d’échantillonnage et du pas. Deux passes de dérivation sur cette table prouvent
le déterminisme sans relire chaque pixel deux fois. Un changement de l’une des
entrées invalide la table.

## SC11 — Hermes et exploitation VPS

La skill et le runbook décrivent le nouvel état effectif sans recopier le
présent brief. Ils indiquent comment Hermes suit un lot durable, envoie les
transitions utiles à Discord et garde le jugement à Claude.

La veille quotidienne possède un mode script seul et silencieux quand tout va
bien. Elle mesure aussi l’espace disque, les worktrees et l’âge du cache sans
supprimer automatiquement une donnée utile.

Les tâches lourdes restent sérialisées sur le VPS 8 Gio. Les agents ne reçoivent
pas les identifiants GitHub ou Discord nécessaires au contrôleur.

## SC12 — Mesures et non-régression

Les tests ajoutés prouvent d’abord les refus et les chemins rouges : politique
invalide, risque abaissé, reprise incohérente, feedback absent, fichier hors
périmètre, bundle excessif, cache périmé et chemin sensible non routé.

Les contrôles finaux comprennent au minimum :

1. tests `control-plane` ;
2. tests `harness` ;
3. tests `sim` ;
4. preuve C1 ;
5. tests unitaires/sentinelles G6 sans exiger le téléchargement du cache DEM ;
6. `git diff --check` ;
7. vérification que les workflows YAML restent valides ;
8. arbre Git sans fichier généré ou secret inattendu.

La preuve G6 Europe complète est rejouée seulement si le cache DEM nécessaire
est disponible. Son absence est déclarée avec la commande et l’erreur ; elle
ne devient jamais un succès supposé.

## Livrables

- politique et état durable ForgePilot ;
- commandes CLI et tests correspondants ;
- routeur de tests et check de risque ;
- workflows CI dédoublés ;
- `.cursorignore` ;
- cache DEM partagé et échantillonnage G6 groupé avec tests sentinelles ;
- documentation opératoire Hermes/ForgePilot mise à jour ;
- journal factuel et manifeste du lot sous ce dossier.

## Hors portée

- fusion automatique sans confirmation propriétaire ;
- réactivation du pipeline historique `full_auto` ;
- nouveau comportement de simulation ;
- Unity ;
- déploiement d’un runner GitHub persistant sur le VPS ;
- exécution des briefs 026, 027 ou 028.
