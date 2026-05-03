BMDTool
BMDTool (BMDEditor) is a desktop .bmd editor for Cultures series games.
The app runs on Windows (WinForms) and supports:

Cultures 1 (C1)
Cultures 2 (C2)
Key Features
Open and save .bmd files (C1/C2).
Frame preview using a 256-color palette.
Import a single frame from PNG.
Replace the selected frame from PNG.
Export a single frame to PNG.
Export/Import full workspace (PNG + metadata.csv).
Edit anchors (OffsetX, OffsetY) for single or multiple frames.
Batch anchor editing (Add / Set).
Reorder frames (up/down), delete selected, or delete all.
Animation playback for multi-frame selection (Play/Stop, FPS).
Load palette from PCX and edit palette colors manually.
Auto-palette detection based on mod files (when folder structure matches).
Requirements
Windows 10/11
.NET SDK 10.0 (target: net10.0-windows)
Build and Run
From the repository root:

dotnet restore .\BmdStudio\BMDEditor.csproj
dotnet build .\BmdStudio\BMDEditor.csproj -c Release
dotnet run --project .\BmdStudio\BMDEditor.csproj
Quick Workflow
Launch the application.
Open a .bmd file or create a new document.
Load a PCX palette (or use auto-palette).
Import/replace frames using PNG files.
Set anchors (OffsetX, OffsetY) manually or in batch.
Preview animation (select 2+ frames and click Play).
Save .bmd or export a workspace.
Workspace Import/Export
Workspace export creates:

metadata.csv
0000.png, 0001.png, ... for non-empty frames
During import:

If metadata.csv exists, frame types and anchors are loaded from it.
If metadata.csv is missing, the app imports PNG files only and applies default frame types for the selected game format.
metadata.csv Format
First lines:

format,c1
index,type,x,y
Row fields:

index - frame number
type - frame type (0..4)
x - OffsetX
y - OffsetY
Auto Palette (Optional)
For auto-palette to work, the app looks for mod data in paths such as:

Note
The repository currently includes bin/ and obj/ build artifacts. For day-to-day development, it is recommended to ignore them in .gitignore.
