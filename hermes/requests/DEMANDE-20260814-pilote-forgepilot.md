---
author: hermes
kind: demande
created_at: 2026-08-14T12:00:00Z
concerns: projet
status: REFLECTED_IN_ROADMAP
---
# Tester un workflow Hermes, Grok et Cursor simplifié

Le propriétaire valide la simplification suivante : Hermes pilote sans modèle
local lourd, Grok Build distant prépare le plan et relit le résultat, Cursor
est le seul agent qui modifie le code, les tests mécaniques restent
autoritaires et la fusion reste humaine pendant le pilote.

Le pilote doit être neuf, lisible et réversible. L'ancien full-auto passe en
mode manuel. L'observateur Windows local est suspendu. Aucun cron et aucun
auto-merge ne sont ajoutés avant un bilan de trois lots réels.

La possibilité de piloter Grok par ACP doit être vérifiée avant le choix du
transport. Résultat : Hermes sait exposer un serveur ACP mais ne possède pas
encore de client ACP générique ; Grok est donc lancé avec son CLI headless.
