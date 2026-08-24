# Briefs abandonnés — ne plus exécuter

Décision : [ADR-0019](../../docs/adr/0019-geler-g6-reculer-le-scope.md).
Ce fichier n'est **pas** une instruction d'exécutant. Chaque brief
reste, s'il était relancé, sa propre source. La file dit : **ne pas
les relancer**.

| dossier | pourquoi il sort du quotidien |
|---|---|
| `briefs/024-geo-relief-g6/` | G6 : itérations chères, échec. Gelé. |
| `briefs/030-sim-lit-gisements-r1/` | consommerait R1 ; plus un objectif |
| `briefs/031-viewer-couche-gisements-r1/` | dépend de 030 |
| `briefs/032-qa-controles-partages/` | outillage geo, pas le moteur mince |

Les briefs geo déjà livrés (dont 025, 026) sont une **archive**. On ne
les rejoue pas. `sim/` lit encore G3. Le produit quotidien est
`python -m sim`.
