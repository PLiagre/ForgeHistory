# Vision — Moteur de simulation historique vivant

> **Où vit le produit aujourd'hui (2026-08-23).** Le moteur tourne dans
> `sim/` (`python -m sim`). `viewer/` est un regard mince sur un snapshot.
> Unity est en veille. Cette note de statut ne change pas les piliers
> ci-dessous.

> Ce document est la source de vérité de la vision produit. Il prime sur
> PROJECT_BRIEF.md en cas de conflit. Décisions d'architecture associées :
> [ADR-001](docs/adr/ADR-001-moteur-vivant-lod.md) (moteur multi-échelle) et
> [ADR-002](docs/adr/ADR-002-monde-amorce-historiquement.md) (le monde est
> amorcé historiquement, et le joueur agit en créant des conditions).
>
> ⚠️ Lire ADR-002 avant d'invoquer « la simulation ne connaît pas X » : le monde
> à t0 contient ce que l'histoire dit qu'il contient. L'émergence concerne ce qui
> arrive PENDANT la partie, pas l'amorçage.

## Ce que nous construisons

Nous ne développons pas un clone de Victoria 3. Nous développons un **moteur de
simulation historique** dont le gameplay émerge. Les mécaniques comparables à
Victoria, EU4, Manor Lords et, à terme, aux batailles tactiques type Total War,
doivent **émerger** de la simulation — pas être codées comme des règles de jeu.

## Principe fondateur : une seule simulation

- Le monde entier est simulé en permanence. Il n'existe qu'**une seule source de
  vérité**.
- Les interfaces (vue monde, province, ville, quartier, bataille) ne sont que
  des façons différentes d'**observer** cette simulation. Elles manipulent
  exactement les mêmes données.
- Aucune donnée n'est dupliquée pour satisfaire une interface. Jamais deux
  bases de données (stratégique vs tactique).

## Philosophie

Le moteur raisonne en termes de **monde**, jamais de gameplay.

Interdit : « si famine alors +20 % de criminalité ».
Exigé : les habitants ont faim → ils cherchent de la nourriture → certains
volent → la criminalité augmente.

Nous privilégions toujours : règles générales, comportements émergents,
systèmes réutilisables, données plutôt que scripts spécifiques. Les systèmes
remplis de cas particuliers sont de la dette.

**Test à chaque proposition** : est-ce un comportement émergent ou une règle
codée en dur ?

## Échelles de simulation

```
Monde → Pays → Province → Ville → Quartier → Bâtiment → Famille → Personne
```

Chaque niveau doit pouvoir être simulé indépendamment. Le moteur augmente ou
diminue le niveau de détail selon le contexte (voir ADR-001 : agrégation /
désagrégation conservatives).

## Les piliers

Géographie, climat, saisons, ressources naturelles, population, familles,
économie, commerce, infrastructures, politique, diplomatie, religion, culture,
technologie, armées, logistique, urbanisation, industrie. Chaque système évolue
indépendamment.

## Règles par domaine

- **Population** — cœur du moteur. Une personne appartient à une famille ; une
  famille possède habitation, patrimoine, revenus, culture, religion,
  profession. Villages, villes et États émergent des familles.
- **Économie** — entièrement **physique**. Chaque ressource a une origine, un
  transport, un stockage, une destination. Aucune ressource ne se téléporte.
  Toute rupture logistique produit des conséquences naturelles.
- **Armées** — les soldats proviennent de la population, consomment des
  ressources, utilisent les infrastructures, meurent, retournent à la vie
  civile. Les pertes ont un impact démographique et économique.
- **Urbanisation** — le joueur crée des conditions, il ne place pas chaque
  bâtiment. Les habitants construisent, investissent, déménagent. Les quartiers
  évoluent selon richesse, industrie, transports, sécurité, population.
- **Batailles** — couche supplémentaire de simulation sur les **mêmes
  données** : même terrain, mêmes armées ; les pertes reviennent directement
  dans la simulation mondiale.

## Architecture en couches

```
Simulation Core → Monde → Population → Économie → Politique → Militaire → Présentation (Unity)
```

Unity n'est jamais responsable de la logique métier (rendu, interactions,
animations, effets, interface uniquement). Le backend fonctionne sans Unity.

## Roadmap par couches

1. **Monde vivant** — carte, terrain, climat, ressources, population, économie
   locale, commerce.
2. **Villes** — urbanisation, entreprises, métiers, routes, infrastructures.
3. **États** — fiscalité, lois, diplomatie, technologies, culture, religion.
4. **Armées** — recrutement, logistique, ravitaillement, stratégie.
5. **Batailles tactiques.**

## Mesure du succès

Pas le nombre de fonctionnalités : la capacité du moteur à faire émerger
naturellement des situations complexes, crédibles et intéressantes pour le
joueur.
