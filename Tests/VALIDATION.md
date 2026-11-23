# Validation de 1.2.0-preview.1

Exécutée le 19 novembre 2026 sur le poste Windows de développement. Aucun Falcon n’a été ouvert et aucune force n’a été envoyée au matériel.

| Contrôle | Résultat |
| --- | --- |
| Suite noyau et concurrence (.NET 10) | 12 scénarios réussis, dont 100 créations/arrêts de worker |
| Nettoyage DHD avec exports simulés | Ordre zéro/désactivation/fermeture vérifié ; échecs injectés, DLL et réservation conservées si fermeture refusée |
| Compilation .NET Standard 2.1 / C# 9 contre Unity 6000.3.24f1 | Réussie, runtime + éditeur + moniteur |
| Compilation du diagnostic Windows | Réussie |
| Play Mode réel Unity 6000.3.24f1 | Quatre combinaisons domaine/scène réussies |
| Import UPM du tarball final | Réussi ; quatre combinaisons de Play Mode réussies à nouveau depuis le tarball |
| Contenu de l’archive | 33 fichiers comparés par SHA-256 aux sources, tous identiques |
| Entrées en simulation | XYZ/boutons, déconnexion, reconnexion désarmée et réactivation du composant vérifiés |
| Matériel réel et force physique | Non exécuté |
| Player Mono / IL2CPP de cette préversion | Non exécuté |
| Unity 2021.3 / 2022.3 et autres systèmes | Non exécuté |

Le test Play Mode est exécuté dans `.validation/FalconConsolidation`, d’abord avec les sources locales (`validation.log`), puis avec le tarball final (`tarball-validation.log`). Les quatre messages `FALCON PLAY PASS` et le message final de réussite sont présents dans chaque exécution ; Unity a quitté avec le code zéro. Les journaux comportent également des exceptions internes à `UnityEditor.Search.SearchDatabase` au démarrage de l’indexation, et des messages du runtime Mono à la fermeture. Ces messages ne proviennent pas des sources Falcon ; la validation ne signifie donc pas que les journaux de l’éditeur sont entièrement exempts d’erreurs.

Archive : `Distribution/com.falcon.haptics-1.2.0-preview.1.tgz`.

SHA-256 : `BE89C8DA000B36167A501FED38ED11B28BBFF9868C54EFB3783C3D2E1FC1260E`.

La préversion ne revendique ni une boucle à 1 kHz, ni une garantie de coupure lors d’un appel DHD bloqué. L’âge publié est celui de la lecture reçue par le logiciel. Les essais matériels, l’horodatage interne éventuel du contrôleur et le watchdog restent à qualifier.
