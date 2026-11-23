# Validation 1.2.0-preview.5

19 novembre 2026. Ajout de FalconAxisAlignment et du Playground 1.1.

- `dotnet run --project Tests/Falcon.Core.Tests` : 27 scénarios PASS. Les nouveaux scénarios parcourent les 48 permutations signées, vérifient droite/haut/profondeur, inverse et conservation du travail. Gestes bruités acceptés ; gestes courts, confondus, non finis ou profondeur mélangée refusés.
- `dotnet build Tests/Unity.ApiCheck --no-restore` : 0 erreur, 0 avertissement.
- Import du SDK embarqué et construction de scène dans Unity 6000.3.24f1 : PASS.
- Play : trois gestes simulant une permutation Y/Z/X, activation refusée avant confirmation, projection caméra, profils et JSON v2 vérifiés ; six faces, capture, friction et cycle de session vérifiés. PASS.
- Player Windows x64 Mono construit et test autonome réussi en simulation.

[Compte rendu complet du projet](../Unity/FalconSdkPlayground-1.1/VALIDATION.md) · [Audit](../Unity/FalconSdkPlayground-1.1/AUDIT-UI-UX.md).

Aucune commande matérielle exécutée pour cette livraison. Les gestes et efforts sur le Falcon restent à confirmer ; ces contrôles ne certifient pas une cadence temps réel ni une force mesurée.
