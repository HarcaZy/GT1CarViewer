using System.Globalization;

namespace GT1CarViewer.App;

internal enum AppLanguage
{
    French,
    English,
    Spanish,
    Japanese
}

internal static class AppText
{
    private static readonly IReadOnlyDictionary<AppLanguage, CultureInfo> Cultures =
        new Dictionary<AppLanguage, CultureInfo>
        {
            [AppLanguage.French] = CultureInfo.GetCultureInfo("fr-FR"),
            [AppLanguage.English] = CultureInfo.GetCultureInfo("en-US"),
            [AppLanguage.Spanish] = CultureInfo.GetCultureInfo("es-ES"),
            [AppLanguage.Japanese] = CultureInfo.GetCultureInfo("ja-JP")
        };

    static AppText()
    {
        string[] expectedKeys = Translations[AppLanguage.French].Keys.Order().ToArray();
        foreach (var (language, translations) in Translations)
        {
            if (!translations.Keys.Order().SequenceEqual(expectedKeys))
            {
                throw new InvalidOperationException($"The {language} translation is missing or has extra UI text.");
            }

            foreach (string format in translations.Values)
            {
                _ = string.Format(Cultures[language], format, 1, 2, 3, 4);
            }
        }
    }

    private static readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> Translations =
        new Dictionary<AppLanguage, IReadOnlyDictionary<string, string>>
        {
            [AppLanguage.French] = new Dictionary<string, string>
            {
                ["WindowTitle"] = "GT1 Car Viewer",
                ["OpenImage"] = "Ouvrir une image",
                ["ExportObj"] = "Exporter OBJ",
                ["Garage"] = "Votre garage",
                ["GarageHint"] = "Ouvrez une image pour afficher les voitures",
                ["SearchCars"] = "Rechercher une voiture...",
                ["Models"] = "MODÈLES",
                ["CarList"] = "Liste des voitures",
                ["Textures"] = "Textures",
                ["Wireframe"] = "Contours",
                ["AutoRotate"] = "Rotation auto",
                ["UvCorrection"] = "Correction verticale des UV en pixels",
                ["Language"] = "Langue",
                ["ChooseCar"] = "Choisissez une voiture",
                ["NoResults"] = "Aucun résultat",
                ["CarNumber"] = "Voiture {0:000}",
                ["ArchiveCaption"] = "{0}   ·   {1:N0} voitures",
                ["CarsInArchive"] = "{0:N0} voitures • {1}",
                ["Ready"] = "Prêt — ouvrez une image BIN ou un CAR.DAT",
                ["CarMetadataHint"] = "Les modèles et textures sont lus depuis l’image, sans extraction préalable.",
                ["ViewportDescription"] = "Faites glisser la souris pour faire tourner la voiture. La molette règle le zoom.",
                ["ViewportName"] = "Aperçu 3D de la voiture",
                ["ViewportHelp"] = "Glisser pour tourner  |  Molette pour zoomer",
                ["EmptyGarage"] = "Votre garage Gran Turismo",
                ["EmptyGarageHint"] = "Ouvrez une image BIN ou un fichier CAR.DAT pour commencer.",
                ["OpenDialogTitle"] = "Ouvrir une image Gran Turismo ou une archive de voitures",
                ["OpenFilter"] = "Gran Turismo (BIN, DAT)|*.bin;*.dat|Images BIN|*.bin|Archives CAR.DAT|*.dat|Tous les fichiers|*.*",
                ["ReadingImage"] = "Lecture de l’image et des archives voiture...",
                ["NoArchivesFound"] = "Aucune archive CAR.DAT ou CARCADE.DAT n’a été trouvée.",
                ["ImageReady"] = "Image prête • {0} archive(s) chargée(s)",
                ["OpenFailed"] = "L’ouverture a échoué.",
                ["OpenFailedTitle"] = "Impossible d’ouvrir cette archive",
                ["DecodingCar"] = "Décodage de la voiture {0}...",
                ["ColorNumber"] = "Teinte {0:X2}",
                ["LodMetadata"] = "LOD 1 • {0:N0} sommets • {1:N0} faces",
                ["SkippedFaces"] = " • {0:N0} faces ignorées",
                ["ModelStatus"] = "{0} • {1} teinte(s) • {2}/{3} niveau(x) de détail",
                ["ModelDecodeFailed"] = "Ce modèle n’a pas pu être décodé.",
                ["ModelReadFailed"] = "Erreur de lecture du modèle.",
                ["ModelReadFailedTitle"] = "Impossible de lire {0}",
                ["ExportDialogTitle"] = "Exporter le modèle de la voiture",
                ["ObjFilter"] = "Modèle Wavefront OBJ|*.obj",
                ["ExportProgress"] = "Modèle, matériaux et textures exportés dans {0}",
                ["ExportSuccess"] = "Les fichiers OBJ, MTL et BMP sont prêts dans :\n{0}",
                ["ExportSuccessTitle"] = "Export terminé",
                ["ExportFailed"] = "L’export a échoué.",
                ["ExportFailedTitle"] = "Impossible d’exporter ce modèle",
                ["LanguageSaveFailed"] = "La langue a été changée, mais le choix n’a pas pu être enregistré.",
                ["LanguageSaveFailedTitle"] = "Impossible d’enregistrer la langue",
                ["French"] = "Français",
                ["English"] = "English",
                ["Spanish"] = "Español",
                ["Japanese"] = "日本語"
            },
            [AppLanguage.English] = new Dictionary<string, string>
            {
                ["WindowTitle"] = "GT1 Car Viewer",
                ["OpenImage"] = "Open image",
                ["ExportObj"] = "Export OBJ",
                ["Garage"] = "Your garage",
                ["GarageHint"] = "Open an image to browse the cars",
                ["SearchCars"] = "Search cars...",
                ["Models"] = "CARS",
                ["CarList"] = "Car list",
                ["Textures"] = "Textures",
                ["Wireframe"] = "Wireframe",
                ["AutoRotate"] = "Auto-rotate",
                ["UvCorrection"] = "Vertical UV adjustment in pixels",
                ["Language"] = "Language",
                ["ChooseCar"] = "Choose a car",
                ["NoResults"] = "No results",
                ["CarNumber"] = "Car {0:000}",
                ["ArchiveCaption"] = "{0}   ·   {1:N0} cars",
                ["CarsInArchive"] = "{0:N0} cars • {1}",
                ["Ready"] = "Ready — open a BIN image or CAR.DAT file",
                ["CarMetadataHint"] = "Models and textures are read directly from the image.",
                ["ViewportDescription"] = "Drag to rotate the car. Use the mouse wheel to zoom.",
                ["ViewportName"] = "3D car preview",
                ["ViewportHelp"] = "Drag to rotate  |  Scroll to zoom",
                ["EmptyGarage"] = "Your Gran Turismo garage",
                ["EmptyGarageHint"] = "Open a BIN image or CAR.DAT file to get started.",
                ["OpenDialogTitle"] = "Open a Gran Turismo image or car archive",
                ["OpenFilter"] = "Gran Turismo (BIN, DAT)|*.bin;*.dat|BIN images|*.bin|CAR.DAT archives|*.dat|All files|*.*",
                ["ReadingImage"] = "Reading image and car archives...",
                ["NoArchivesFound"] = "No CAR.DAT or CARCADE.DAT archive was found.",
                ["ImageReady"] = "Image ready • {0} archive(s) loaded",
                ["OpenFailed"] = "Could not open the image.",
                ["OpenFailedTitle"] = "Unable to open this archive",
                ["DecodingCar"] = "Decoding car {0}...",
                ["ColorNumber"] = "Color {0:X2}",
                ["LodMetadata"] = "LOD 1 • {0:N0} vertices • {1:N0} faces",
                ["SkippedFaces"] = " • {0:N0} skipped faces",
                ["ModelStatus"] = "{0} • {1} color(s) • {2}/{3} detail level(s)",
                ["ModelDecodeFailed"] = "This model could not be decoded.",
                ["ModelReadFailed"] = "Error reading the model.",
                ["ModelReadFailedTitle"] = "Unable to read {0}",
                ["ExportDialogTitle"] = "Export car model",
                ["ObjFilter"] = "Wavefront OBJ model|*.obj",
                ["ExportProgress"] = "Model, materials, and textures exported to {0}",
                ["ExportSuccess"] = "The OBJ, MTL, and BMP files are ready in:\n{0}",
                ["ExportSuccessTitle"] = "Export complete",
                ["ExportFailed"] = "Export failed.",
                ["ExportFailedTitle"] = "Unable to export this model",
                ["LanguageSaveFailed"] = "The language was changed, but the preference could not be saved.",
                ["LanguageSaveFailedTitle"] = "Unable to save language",
                ["French"] = "Français",
                ["English"] = "English",
                ["Spanish"] = "Español",
                ["Japanese"] = "日本語"
            },
            [AppLanguage.Spanish] = new Dictionary<string, string>
            {
                ["WindowTitle"] = "GT1 Car Viewer",
                ["OpenImage"] = "Abrir imagen",
                ["ExportObj"] = "Exportar OBJ",
                ["Garage"] = "Tu garaje",
                ["GarageHint"] = "Abre una imagen para ver los coches",
                ["SearchCars"] = "Buscar coches...",
                ["Models"] = "COCHES",
                ["CarList"] = "Lista de coches",
                ["Textures"] = "Texturas",
                ["Wireframe"] = "Contornos",
                ["AutoRotate"] = "Rotación auto",
                ["UvCorrection"] = "Ajuste vertical UV en píxeles",
                ["Language"] = "Idioma",
                ["ChooseCar"] = "Elige un coche",
                ["NoResults"] = "Sin resultados",
                ["CarNumber"] = "Coche {0:000}",
                ["ArchiveCaption"] = "{0}   ·   {1:N0} coches",
                ["CarsInArchive"] = "{0:N0} coches • {1}",
                ["Ready"] = "Listo — abre una imagen BIN o un archivo CAR.DAT",
                ["CarMetadataHint"] = "Los modelos y las texturas se leen directamente de la imagen.",
                ["ViewportDescription"] = "Arrastra para girar el coche. Usa la rueda del ratón para acercar o alejar.",
                ["ViewportName"] = "Vista previa 3D del coche",
                ["ViewportHelp"] = "Arrastra para girar  |  Rueda para acercar",
                ["EmptyGarage"] = "Tu garaje de Gran Turismo",
                ["EmptyGarageHint"] = "Abre una imagen BIN o un archivo CAR.DAT para empezar.",
                ["OpenDialogTitle"] = "Abrir una imagen de Gran Turismo o un archivo de coches",
                ["OpenFilter"] = "Gran Turismo (BIN, DAT)|*.bin;*.dat|Imágenes BIN|*.bin|Archivos CAR.DAT|*.dat|Todos los archivos|*.*",
                ["ReadingImage"] = "Leyendo la imagen y los archivos de coches...",
                ["NoArchivesFound"] = "No se encontró ningún archivo CAR.DAT ni CARCADE.DAT.",
                ["ImageReady"] = "Imagen lista • {0} archivo(s) cargado(s)",
                ["OpenFailed"] = "No se pudo abrir la imagen.",
                ["OpenFailedTitle"] = "No se pudo abrir este archivo",
                ["DecodingCar"] = "Decodificando el coche {0}...",
                ["ColorNumber"] = "Color {0:X2}",
                ["LodMetadata"] = "LOD 1 • {0:N0} vértices • {1:N0} caras",
                ["SkippedFaces"] = " • {0:N0} caras omitidas",
                ["ModelStatus"] = "{0} • {1} color(es) • {2}/{3} nivel(es) de detalle",
                ["ModelDecodeFailed"] = "No se pudo decodificar este modelo.",
                ["ModelReadFailed"] = "Error al leer el modelo.",
                ["ModelReadFailedTitle"] = "No se pudo leer {0}",
                ["ExportDialogTitle"] = "Exportar modelo del coche",
                ["ObjFilter"] = "Modelo Wavefront OBJ|*.obj",
                ["ExportProgress"] = "Modelo, materiales y texturas exportados a {0}",
                ["ExportSuccess"] = "Los archivos OBJ, MTL y BMP están listos en:\n{0}",
                ["ExportSuccessTitle"] = "Exportación completada",
                ["ExportFailed"] = "La exportación ha fallado.",
                ["ExportFailedTitle"] = "No se pudo exportar este modelo",
                ["LanguageSaveFailed"] = "Se cambió el idioma, pero no se pudo guardar la preferencia.",
                ["LanguageSaveFailedTitle"] = "No se pudo guardar el idioma",
                ["French"] = "Français",
                ["English"] = "English",
                ["Spanish"] = "Español",
                ["Japanese"] = "日本語"
            },
            [AppLanguage.Japanese] = new Dictionary<string, string>
            {
                ["WindowTitle"] = "GT1 Car Viewer",
                ["OpenImage"] = "イメージを開く",
                ["ExportObj"] = "OBJ を出力",
                ["Garage"] = "ガレージ",
                ["GarageHint"] = "イメージを開いて車を表示",
                ["SearchCars"] = "車を検索...",
                ["Models"] = "車種",
                ["CarList"] = "車の一覧",
                ["Textures"] = "テクスチャ",
                ["Wireframe"] = "ワイヤーフレーム",
                ["AutoRotate"] = "自動回転",
                ["UvCorrection"] = "UV の垂直調整（ピクセル）",
                ["Language"] = "言語",
                ["ChooseCar"] = "車を選択",
                ["NoResults"] = "該当する車がありません",
                ["CarNumber"] = "車 {0:000}",
                ["ArchiveCaption"] = "{0}   ·   {1:N0} 台",
                ["CarsInArchive"] = "{0:N0} 台 • {1}",
                ["Ready"] = "準備完了 — BIN イメージまたは CAR.DAT を開いてください",
                ["CarMetadataHint"] = "モデルとテクスチャはイメージから直接読み込みます。",
                ["ViewportDescription"] = "ドラッグで車を回転し、マウスホイールで拡大・縮小します。",
                ["ViewportName"] = "車の 3D プレビュー",
                ["ViewportHelp"] = "ドラッグで回転  |  ホイールで拡大・縮小",
                ["EmptyGarage"] = "グランツーリスモのガレージ",
                ["EmptyGarageHint"] = "BIN イメージまたは CAR.DAT を開いてください。",
                ["OpenDialogTitle"] = "グランツーリスモのイメージまたは車のアーカイブを開く",
                ["OpenFilter"] = "Gran Turismo (BIN, DAT)|*.bin;*.dat|BIN イメージ|*.bin|CAR.DAT アーカイブ|*.dat|すべてのファイル|*.*",
                ["ReadingImage"] = "イメージと車のアーカイブを読み込み中...",
                ["NoArchivesFound"] = "CAR.DAT または CARCADE.DAT が見つかりません。",
                ["ImageReady"] = "イメージを読み込みました • アーカイブ数: {0}",
                ["OpenFailed"] = "イメージを開けませんでした。",
                ["OpenFailedTitle"] = "アーカイブを開けません",
                ["DecodingCar"] = "{0} をデコード中...",
                ["ColorNumber"] = "カラー {0:X2}",
                ["LodMetadata"] = "LOD 1 • 頂点 {0:N0} • 面 {1:N0}",
                ["SkippedFaces"] = " • スキップした面 {0:N0}",
                ["ModelStatus"] = "{0} • カラー {1} • 詳細レベル {2}/{3}",
                ["ModelDecodeFailed"] = "このモデルをデコードできませんでした。",
                ["ModelReadFailed"] = "モデルの読み込みエラー。",
                ["ModelReadFailedTitle"] = "{0} を読み込めません",
                ["ExportDialogTitle"] = "車のモデルを出力",
                ["ObjFilter"] = "Wavefront OBJ モデル|*.obj",
                ["ExportProgress"] = "モデル、マテリアル、テクスチャを {0} に出力しました",
                ["ExportSuccess"] = "OBJ、MTL、BMP ファイルを保存しました:\n{0}",
                ["ExportSuccessTitle"] = "出力完了",
                ["ExportFailed"] = "出力に失敗しました。",
                ["ExportFailedTitle"] = "モデルを出力できません",
                ["LanguageSaveFailed"] = "表示言語を変更しましたが、設定を保存できませんでした。",
                ["LanguageSaveFailedTitle"] = "言語を保存できません",
                ["French"] = "Français",
                ["English"] = "English",
                ["Spanish"] = "Español",
                ["Japanese"] = "日本語"
            }
        };

    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GT1CarViewer",
        "language.txt");

    public static AppLanguage Language { get; private set; } = AppLanguage.French;

    public static IReadOnlyList<AppLanguage> SupportedLanguages { get; } =
        [AppLanguage.French, AppLanguage.English, AppLanguage.Spanish, AppLanguage.Japanese];

    public static string Get(string key, params object?[] arguments)
    {
        string format = Translations[Language][key];
        return arguments.Length == 0
            ? format
            : string.Format(Cultures[Language], format, arguments);
    }

    public static string GetLanguageName(AppLanguage language) => Get(language.ToString());

    public static void LoadPreference()
    {
        if (File.Exists(PreferencePath) &&
            Enum.TryParse(File.ReadAllText(PreferencePath).Trim(), ignoreCase: true, out AppLanguage saved) &&
            Translations.ContainsKey(saved))
        {
            Language = saved;
            return;
        }

        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "en" => AppLanguage.English,
            "es" => AppLanguage.Spanish,
            "ja" => AppLanguage.Japanese,
            _ => AppLanguage.French
        };
    }

    public static void SetLanguage(AppLanguage language)
    {
        if (!Translations.ContainsKey(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }

        Language = language;
    }

    public static void SavePreference()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        File.WriteAllText(PreferencePath, Language.ToString());
    }
}
