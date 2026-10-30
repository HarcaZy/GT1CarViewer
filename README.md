# GT1 Car Viewer

A multilingual Windows viewer for *Gran Turismo* cars on the PlayStation.
It directly opens raw 2352-byte/sector BIN images, 2048-byte ISO images,
and `CAR.DAT` / `CARCADE.DAT` archives without modifying the image or requiring
prior extraction of the car files.

## Usage

The interface is available in English, French, Spanish, and Japanese. The 
language matches your Windows language on first launch and can be changed in 
the sidebar; your choice is saved for subsequent launches.

To create the standalone Windows x64 installer, install Inno Setup 6 and run:

```powershell
.\build-installer.ps1
```

The `installer\Output\GT1CarViewer-Setup-1.0.0.exe` file installs the 
application for the current user, creates a Start Menu shortcut, and offers 
a desktop shortcut. No separate .NET runtime is required. The executables 
use `GT1CarViewerIcon.ico`.

To publish the application without the installer:

```powershell
dotnet publish .\GT1CarViewer.App\GT1CarViewer.App.csproj -c Release -p:EnableWindowsTargeting=true
```

This classic publication requires the .NET Desktop 10 runtime.

To run it directly from the project folder:

```powershell
dotnet run --project .\GT1CarViewer.App -- "C:\(\path\to\Gran\) Turismo.bin"
```

If no path is provided as an argument, select **Open an image** or drag and drop 
a BIN/DAT file into the window. `Ctrl+O` opens the file picker.

The list allows you to browse archives and search for a specific car number. 
In the preview pane, click and drag to rotate, use the scroll wheel to zoom, 
and select a color palette. Controls are available to toggle textures, wireframe, 
and auto-rotation. The **UV ↑** setting shifts faces upward in pixels, which 
also applies to exported models.

**Export OBJ** creates the three files of a Wavefront model (`.obj`, `.mtl`, 
and a `.bmp` texture atlas containing the 16 palettes) in your chosen folder. 
Car names are displayed by their in-game entry number.

## Building and Testing

```powershell
dotnet build .\GT1CarViewer.App\GT1CarViewer.App.csproj -p:EnableWindowsTargeting=true
dotnet run --project .\GT1CarViewer.Tests
```

To verify the complete disc image, set the `GT1_TEST_BIN` environment 
variable to its file path before running the tests. The test suite reads 
both archives and decodes every model/texture pair.

## Supported Formats

- PlayStation disc images in ISO9660 format, raw Mode 1 or Mode 2/Form 1 sectors, 
  and 2048-byte/sector ISO9660 files.
- `GT-ARC` archives containing ordered `GT-CTEX` texture / `GT-CAR` model pairs, 
  compressed using `GT-ZIP`.
- Levels of detail (LOD), vertices, triangles/quads, textured faces, UV 
  coordinates, colors, and BGR555 palettes.
- OBJ export of geometry and texture atlas. Native normals are not included 
  in this export.

## Project Structure

- `GT1CarViewer.Core`: ISO9660 reader, GT-ZIP decompressor, GT-ARC archives, 
  GT1 models/textures, and Wavefront export.
- `GT1CarViewer.App`: Windows Forms interface and interactive 3D preview.
- `GT1CarViewer.Tests`: Tests for formats, export functionality, and the provided disc image.
