# GT1 Car Viewer

Visionneuse Windows multilingue pour les voitures de *Gran Turismo* sur PlayStation.
Elle ouvre les images BIN brutes de 2352 octets par secteur, les images ISO
2048 et les archives `CAR.DAT` / `CARCADE.DAT` directement, sans modifier
l’image et sans extraire les voitures au préalable.

## Utilisation

L’interface est disponible en français, anglais, espagnol et japonais. La
langue suit celle de Windows au premier démarrage et peut être modifiée dans
la barre latérale ; le choix est conservé pour les démarrages suivants.

Pour créer l’installateur Windows x64 autonome, installez Inno Setup 6 puis
exécutez :

```powershell
.\build-installer.ps1
```

Le fichier `installer\Output\GT1CarViewer-Setup-1.0.0.exe` installe
l’application pour l’utilisateur courant, crée un raccourci dans le menu
Démarrer et propose un raccourci sur le bureau. Aucun runtime .NET séparé
n’est requis. Les exécutables utilisent `GT1CarViewerIcon.ico`.

Pour publier l’application sans l’installateur :

```powershell
dotnet publish .\GT1CarViewer.App\GT1CarViewer.App.csproj -c Release -p:EnableWindowsTargeting=true
```

Cette publication classique nécessite le runtime .NET Desktop 10.

Pour la lancer directement depuis le dossier du projet :

```powershell
dotnet run --project .\GT1CarViewer.App -- "C:\chemin\Gran Turismo.bin"
```

Sans chemin en argument, choisissez **Ouvrir une image** ou déposez un fichier
BIN/DAT dans la fenêtre. `Ctrl+O` ouvre le sélecteur de fichiers.

La liste permet de parcourir les archives et de rechercher un numéro de voiture.
Dans l’aperçu, faites glisser la souris pour tourner, utilisez la molette pour
zoomer et sélectionnez une teinte. Les commandes permettent d’activer ou de
désactiver les textures, le maillage et la rotation automatique. Le réglage
**UV ↑** déplace les faces vers le haut, en pixels, et s’applique aussi à l’export.

**Exporter OBJ** crée les trois fichiers d’un modèle Wavefront : `.obj`, `.mtl`
et une texture atlas `.bmp` contenant les 16 palettes, dans le dossier choisi.
Les noms de voiture sont affichés par numéro d’entrée du jeu.

## Compilation et tests

```powershell
dotnet build .\GT1CarViewer.App\GT1CarViewer.App.csproj -p:EnableWindowsTargeting=true
dotnet run --project .\GT1CarViewer.Tests
```

Pour vérifier l’image complète, définissez la variable d’environnement
`GT1_TEST_BIN` avec son chemin avant d’exécuter les tests. La suite lit les
deux archives et décode chaque paire modèle/texture.

## Formats pris en charge

- Images de disque PlayStation au format ISO9660, secteurs bruts Mode 1 ou
  Mode 2/Form 1, et fichiers ISO9660 à secteurs de 2048 octets.
- Archives `GT-ARC` contenant des paires ordonnées texture `GT-CTEX` /
  modèle `GT-CAR`, compressées en `GT-ZIP`.
- Niveaux de détail, sommets, triangles/quadrilatères, faces texturées,
  coordonnées UV, couleurs et palettes BGR555.
- Export OBJ de la géométrie et de l’atlas de textures. Les normales natives
  ne sont pas incluses dans cet export.

## Structure

- `GT1CarViewer.Core` : lecteur ISO9660, décompresseur GT-ZIP, archives GT-ARC,
  modèles/textures GT1 et export Wavefront.
- `GT1CarViewer.App` : interface Windows Forms et aperçu 3D manipulable.
- `GT1CarViewer.Tests` : tests des formats, de l’export et de l’image fournie.
