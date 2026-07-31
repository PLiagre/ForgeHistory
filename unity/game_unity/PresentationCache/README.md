# PresentationCache

Répertoire de présentation **distinct** de `Assets/Art/**` (périmètre Codex).

- `Sprites/` — pré-rendus déterministes (PNG + stamp). Régénérés seulement si
  l'empreinte modèle (taille/mtime) ou le stem change.
- `ModelsReadOnly/` — copie optionnelle en lecture seule des FBX Codex pour
  inventaire local. Ne jamais écrire dans `Assets/Art/**`.

Les cinq navires sans `.meta` (frigate, ship_of_line, man_of_war, steam_frigate,
ironclad) sont signalés et écartés — Unity génère les `.meta`, jamais à la main.
