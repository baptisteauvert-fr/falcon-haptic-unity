# Friction dynamique — validation de 1.2.0-preview.4

Exécutée le 19 novembre 2026 sur Windows x64, sans ouvrir ni commander un Falcon.

| Contrôle | Résultat |
| --- | --- |
| Suite noyau et concurrence | 25 scénarios réussis, incluant les régressions précédentes et 100 créations/arrêts de worker |
| Direction et puissance de friction | 10 000 pas sur un plan oblique, changements de direction et inversions ; force tangentielle et puissance non positive aux tolérances numériques testées |
| Budget de force | Borne de Coulomb, plafond vectoriel partagé et priorité normale vérifiés |
| Faible vitesse et arrêt | Force croissante avec la vitesse dans les cas testés, zéro au repos, à gain nul et hors contact |
| Compatibilité | Coefficient nul par défaut, aucune force tangentielle dans les cas sans friction |
| Paramètres | Valeurs négatives/non finies et vitesse nulle refusées ; mutation de friction désarmée |
| Diagnostic de pic | Pic enregistré après une commande de friction et remis à zéro après désarmement |
| Compilation | Runtime, outils éditeur, exemples et diagnostic Windows compilés sans avertissement ; C# 9 / .NET Standard 2.1 contre Unity 6000.3.24f1 pour le package |
| Unity réel | Export du coefficient et de la vitesse sur les six faces ; quatre configurations Play Mode réussies, dont désarmement via `SetFriction` |
| Import UPM final | Tarball 1.2.0-preview.4 utilisé pour les quatre configurations Play Mode |
| Scène distribuée | Références, absence de scripts manquants, sélection de Simulation et coefficient 0,5 vérifiés à l’ouverture |
| Archive | 56 fichiers comparés par SHA-256 aux sources, tous identiques |

Journaux locaux dans `.validation/FalconConsolidation` : `friction-validation.log`, `friction-tarball-validation.log` et `friction-demo-import.log`. Les trois exécutions Unity ont terminé avec le code zéro. Les journaux Play contiennent les exceptions internes à `UnityEditor.Search.SearchDatabase` et messages Mono de fermeture déjà observés aux étapes précédentes ; ils ne sont pas entièrement exempts d’erreurs d’éditeur.

Archive : `Distribution/com.falcon.haptics-1.2.0-preview.4.tgz`.

SHA-256 : `1160A027980E98F17B753306B918A4C5E1C9B3683DAE8B6B1537B99472F098F6`.

## Limites

Le modèle ajoute uniquement une friction dynamique régularisée. Il n’y a ni adhérence statique, ni proxy contraint, ni contacts multiples. La priorité normale peut réduire la friction à zéro si elle utilise tout le budget. La force tangentielle peut être supprimée immédiatement à l’arrêt, lors d’une inversion ou d’une réduction de charge ; la montée limitée ne garantit pas la continuité de tout le vecteur.

Les tests de puissance concernent les calculs et déplacements échantillonnés, pas la passivité du système mécanique réel avec ses délais. Les sensations, le bruit, la stabilité, les axes, les coupures et les paramètres matériels restent à qualifier sur un Falcon. L’armement interactif dans Game et les Players Mono/IL2CPP de cette préversion n’ont pas été validés ici. Le pic représente une commande calculée envoyée au backend, pas une mesure de force physique.
