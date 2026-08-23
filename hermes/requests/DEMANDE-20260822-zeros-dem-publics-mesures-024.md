---
author: hermes
kind: demande
created_at: 2026-08-22T12:00:00Z
concerns: brief 024, relief G6
status: CLOSED
---
# Accepter les zéros DEM publics et mesurés du lot 024

## Constat

La correction A2 du relief G6 a supprimé les altitudes fabriquées par lecture raster hors bornes. Une reconstruction indépendante trouve encore neuf pixels valides à `0 m` dans trois cellules que le graphe G5 ne classe pas comme littorales :

- cellule `1492` — Sivach, Crimée ;
- cellule `10189` — basse plaine anglaise ;
- cellule `10427` — plaine et zone tidale autour d'Anvers.

Pour chaque lecture, les artefacts publient la longitude, la latitude, la tuile Copernicus publique, les indices de ligne et de colonne et la valeur brute. Les objets publics répondent et les lectures sont dans les bornes des rasters. Ces zéros ne proviennent ni d'une tuile synthétique, ni d'un repli, ni d'un `nodata` converti.

## Décision propriétaire

Le propriétaire accepte ces neuf valeurs `0 m` comme **mesures DEM réelles**. Elles ne sont ni supprimées, ni converties en `nodata`, ni remplacées par une autre altitude.

Cette décision vaut uniquement si les trois conditions restent mécaniquement prouvées :

1. chaque valeur provient d'un pixel indexable d'une tuile publique dont les octets sont liés au cache ;
2. les trois cellules et les neuf lectures restent toutes nommées dans les artefacts et le journal ;
3. aucune nouvelle cellule non littorale à zéro n'est absorbée silencieusement — toute nouvelle exception exige une nouvelle escalade.

Cette décision ne rend pas le lot recevable à elle seule. Tous les autres constats du reviewer indépendant restent à fermer et la fusion demeure au propriétaire.
