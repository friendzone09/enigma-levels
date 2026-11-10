using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Genera las dos escenas didácticas usando la paleta de tiles existente.
/// Nivel 1: sala con puerta de entrada, escaleras y portal de salida.
/// Nivel 2: sala vacía a la que se llega al cruzar el portal.
/// </summary>
public static class LevelSceneBuilder
{
    private const string Level1Path = "Assets/Scenes/Nivel01_Aprender.unity";
    private const string Level2Path = "Assets/Scenes/Nivel02_SalaVacia.unity";

    // La paleta fue importada a 16 px por unidad y sus tiles miden 32x32 px,
    // así que cada tile ocupa 2x2 unidades. Usamos una celda de 2x2 para que
    // encajen sin solaparse.
    private const int RoomWidth = 12;   // celdas
    private const int RoomHeight = 7;   // celdas
    private const float CellSize = 2f;

    private static TileBase[] stoneTiles;
    private static TileBase[] brickTiles;
    private static TileBase[] ledgeTiles;
    private static Sprite playerSprite;
    private static Sprite doorEntranceSprite;
    private static Sprite doorExitSprite;
    private static Font uiFont;

    [MenuItem("Herramientas/Nivel Olvidado/Construir niveles didácticos")]
    public static void BuildAll()
    {
        Directory.CreateDirectory("Assets/Scenes");
        EnsureFolder("Assets/Sprites", "Characters");
        EnsureCharacterSprite();
        LoadResources();

        BuildLevel1();
        BuildLevel2();
        SetupBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("LEVELBUILDER: escenas generadas -> " + Level1Path + " y " + Level2Path);
        ValidateScenes();
        Debug.Log("LEVELBUILDER: DONE");
    }

    public static void BuildAllWithPreviews()
    {
        BuildAll();
        try
        {
            RenderPreviews();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("LEVELBUILDER: no se pudieron renderizar las previsualizaciones: " + exception);
        }
    }

    // ------------------------------------------------------------------
    // Recursos
    // ------------------------------------------------------------------

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static void EnsureCharacterSprite()
    {
        const string path = "Assets/Sprites/Characters/jugador.png";

        if (!File.Exists(path))
        {
            Debug.LogError("LEVELBUILDER: falta " + path);
            return;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }

    private static void LoadResources()
    {
        playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Characters/jugador.png");

        const string grandes = "Assets/Sprites/nivel1_olvidado_sprites_grandes.png";
        doorEntranceSprite = FindSprite(grandes, "nivel1_olvidado_sprites_grandes_0");
        doorExitSprite = FindSprite(grandes, "nivel1_olvidado_sprites_grandes_1");

        stoneTiles = Enumerable.Range(0, 24)
            .Select(i => AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/tiles/nivel1_olvidado_tileset_" + i + ".asset"))
            .Where(t => t != null)
            .ToArray();

        brickTiles = Enumerable.Range(32, 6)
            .Select(i => AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/tiles/nivel1_olvidado_tileset_" + i + ".asset"))
            .Where(t => t != null)
            .ToArray();

        ledgeTiles = Enumerable.Range(24, 4)
            .Select(i => AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Sprites/tiles/nivel1_olvidado_tileset_" + i + ".asset"))
            .Where(t => t != null)
            .ToArray();

        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = AssetDatabase.GetBuiltinExtraResource<Font>("Arial.ttf");

        if (playerSprite == null || doorExitSprite == null || stoneTiles.Length == 0 || brickTiles.Length == 0)
            Debug.LogError("LEVELBUILDER: no se pudieron cargar todos los recursos de la paleta.");
    }

    private static Sprite FindSprite(string texturePath, string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
    }

    // ------------------------------------------------------------------
    // Nivel 1
    // ------------------------------------------------------------------

    private static void BuildLevel1()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupSceneBasics();

        var grid = CreateGrid();
        var terrain = CreateRoom(grid.transform, 1337, withSteps: true);

        // Puerta de entrada (decorativa, es donde aparece el personaje).
        CreateDoor(grid.transform, "Puerta de entrada", doorEntranceSprite,
            CellBottomCenter(1, 1), interactive: false, targetScene: null, withLight: false);

        // Portal azul de salida, sobre la última escalera.
        CreateDoor(grid.transform, "Portal de salida", doorExitSprite,
            CellTopCenter(9, 3), interactive: true, targetScene: "Nivel02_SalaVacia", withLight: true);

        CreatePlayer(grid.transform, new Vector3(4.6f, 3.2f, 0f));
        CreateLevelUI(
            "NIVEL 1 · APRENDE A CAMINAR Y SALTAR",
            "La sala olvidada",
            "A / D   o   ← / →      Caminar\n" +
            "ESPACIO                     Saltar\n\n" +
            "Objetivo: sube las escaleras y entra en el portal azul.",
            "Nivel 1 / 2");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Level1Path);
    }

    // ------------------------------------------------------------------
    // Nivel 2
    // ------------------------------------------------------------------

    private static void BuildLevel2()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupSceneBasics();

        var grid = CreateGrid();
        CreateRoom(grid.transform, 4242, withSteps: false);

        CreateDoor(grid.transform, "Puerta de entrada", doorEntranceSprite,
            CellBottomCenter(1, 1), interactive: false, targetScene: null, withLight: false);

        CreateDoor(grid.transform, "Portal de vuelta", doorExitSprite,
            CellBottomCenter(10, 1), interactive: true, targetScene: "Nivel01_Aprender", withLight: true);

        CreatePlayer(grid.transform, new Vector3(4.6f, 3.2f, 0f));
        CreateLevelUI(
            "NIVEL 2 · SALA VACÍA",
            "Has cruzado el portal: el personaje avanza de nivel.",
            "A / D   o   ← / →      Caminar\n" +
            "ESPACIO                     Saltar\n\n" +
            "Entra en el portal azul para volver al Nivel 1.",
            "Nivel 2 / 2");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, Level2Path);
    }

    // ------------------------------------------------------------------
    // Construcción de la sala
    // ------------------------------------------------------------------

    private static void SetupSceneBasics()
    {
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        cameraGO.transform.position = new Vector3(RoomWidth * CellSize * 0.5f, 7.1f, -10f);

        var camera = cameraGO.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f, 1f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        cameraGO.AddComponent<AudioListener>();
        var cameraData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = false;

        var lightGO = new GameObject("Luz global 2D");
        var light = lightGO.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(1f, 0.98f, 0.94f, 1f);
        light.intensity = 1.05f;
    }

    private static Grid CreateGrid()
    {
        var gridGO = new GameObject("Grid");
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(CellSize, CellSize, 0f);
        return grid;
    }

    private static GameObject CreateRoom(Transform parent, int seed, bool withSteps)
    {
        var backGO = new GameObject("Muro de fondo");
        backGO.transform.SetParent(parent, false);
        var back = backGO.AddComponent<Tilemap>();
        var backRenderer = backGO.GetComponent<TilemapRenderer>();
        if (backRenderer == null)
            backRenderer = backGO.AddComponent<TilemapRenderer>();
        backRenderer.sortingOrder = -20;

        var terrainGO = new GameObject("Terreno");
        terrainGO.transform.SetParent(parent, false);
        var terrain = terrainGO.AddComponent<Tilemap>();
        var terrainRenderer = terrainGO.GetComponent<TilemapRenderer>();
        if (terrainRenderer == null)
            terrainRenderer = terrainGO.AddComponent<TilemapRenderer>();
        terrainRenderer.sortingOrder = -5;

        var rng = new System.Random(seed);

        // Muro de piedra de fondo en toda la sala.
        for (int y = 0; y < RoomHeight; y++)
        {
            for (int x = 0; x < RoomWidth; x++)
            {
                TileBase tile;
                if (y >= RoomHeight - 1)
                    tile = stoneTiles[Next(rng, 0, 8)];       // remate superior
                else if (y <= 1)
                    tile = stoneTiles[Next(rng, 16, 21)];     // base del muro
                else
                    tile = stoneTiles[Next(rng, 8, 16)];     // cuerpo del muro

                back.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        // Suelo.
        for (int x = 0; x < RoomWidth; x++)
            terrain.SetTile(new Vector3Int(x, 0, 0), RandomBrick(rng));

        // Paredes laterales.
        for (int y = 1; y < RoomHeight; y++)
        {
            terrain.SetTile(new Vector3Int(0, y, 0), RandomBrick(rng));
            terrain.SetTile(new Vector3Int(RoomWidth - 1, y, 0), RandomBrick(rng));
        }

        // Techo.
        for (int x = 1; x < RoomWidth - 1; x++)
            terrain.SetTile(new Vector3Int(x, RoomHeight - 1, 0), RandomBrick(rng));

        // Escaleras del nivel 1 (cada escalón sube una celda = 2 unidades).
        if (withSteps)
        {
            FillBlock(terrain, rng, 4, 5, 1, 1);
            FillBlock(terrain, rng, 6, 7, 1, 2);
            FillBlock(terrain, rng, 8, 10, 1, 3);
        }

        // Remate de piedra clara en la superficie de cada zona pisable.
        var ledgeGO = new GameObject("Bordes pisables");
        ledgeGO.transform.SetParent(parent, false);
        var ledge = ledgeGO.AddComponent<Tilemap>();
        var ledgeRenderer = ledgeGO.GetComponent<TilemapRenderer>();
        if (ledgeRenderer == null)
            ledgeRenderer = ledgeGO.AddComponent<TilemapRenderer>();
        ledgeRenderer.sortingOrder = -4;
        // Alinea el borde de 13 px con la parte superior de la celda.
        ledge.tileAnchor = new Vector3(0.5f, 0.796875f, 0f);

        for (int x = 1; x < RoomWidth - 1; x++)
            ledge.SetTile(new Vector3Int(x, 0, 0), RandomLedge(rng));

        if (withSteps)
        {
            AddLedgeRow(ledge, rng, 4, 5, 1);
            AddLedgeRow(ledge, rng, 6, 7, 2);
            AddLedgeRow(ledge, rng, 8, 10, 3);
        }

        AddTilemapCollider(terrainGO);
        return terrainGO;
    }

    private static void AddLedgeRow(Tilemap ledge, System.Random rng, int xMin, int xMax, int y)
    {
        for (int x = xMin; x <= xMax; x++)
            ledge.SetTile(new Vector3Int(x, y, 0), RandomLedge(rng));
    }

    private static TileBase RandomLedge(System.Random rng) => ledgeTiles[Next(rng, 0, ledgeTiles.Length)];

    private static void FillBlock(Tilemap map, System.Random rng, int xMin, int xMax, int yMin, int yMax)
    {
        for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
                map.SetTile(new Vector3Int(x, y, 0), RandomBrick(rng));
    }

    private static TileBase RandomBrick(System.Random rng) => brickTiles[Next(rng, 0, brickTiles.Length)];

    private static int Next(System.Random rng, int minInclusive, int maxExclusive)
        => rng.Next(minInclusive, maxExclusive);

    private static void AddTilemapCollider(GameObject tilemapGO)
    {
        var tilemapCollider = tilemapGO.AddComponent<TilemapCollider2D>();

        // El nombre del ajuste cambió entre versiones de Unity; lo resolvemos
        // por reflexión para que la escena funcione en esta versión.
        var compositeProperty = typeof(TilemapCollider2D).GetProperty(
            "compositeOperation", BindingFlags.Public | BindingFlags.Instance);
        bool merged = false;
        if (compositeProperty != null)
        {
            try
            {
                object merge = Enum.Parse(compositeProperty.PropertyType, "Merge");
                compositeProperty.SetValue(tilemapCollider, merge);
                merged = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("LEVELBUILDER: compositeOperation no aplicado: " + exception.Message);
            }
        }

        if (!merged)
        {
            var legacyProperty = typeof(TilemapCollider2D).GetProperty(
                "usedByComposite", BindingFlags.Public | BindingFlags.Instance);
            if (legacyProperty != null)
                legacyProperty.SetValue(tilemapCollider, true);
        }

        var body = tilemapGO.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        var composite = tilemapGO.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
    }

    private static Vector3 CellBottomCenter(int cellX, int cellY)
        => new Vector3(cellX * CellSize + CellSize * 0.5f, cellY * CellSize, 0f);

    private static Vector3 CellTopCenter(int cellX, int topCellY)
        => new Vector3(cellX * CellSize + CellSize * 0.5f, (topCellY + 1) * CellSize, 0f);

    // ------------------------------------------------------------------
    // Objetos
    // ------------------------------------------------------------------

    private static void CreatePlayer(Transform parent, Vector3 position)
    {
        var player = new GameObject("Jugador");
        player.transform.SetParent(parent, false);
        player.transform.position = position;

        var renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = playerSprite;
        renderer.sortingOrder = 10;

        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 2.6f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = player.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Vertical;
        collider.size = new Vector2(0.9f, 2f);

        player.AddComponent<PlayerController2D>();
    }

    private static GameObject CreateDoor(
        Transform parent, string name, Sprite sprite, Vector3 bottomCenter,
        bool interactive, string targetScene, bool withLight)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = bottomCenter;

        var art = new GameObject("Arte");
        art.transform.SetParent(root.transform, false);
        var renderer = art.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;

        // Alinear la base del sprite con la base de la puerta,
        // independientemente del pivote de cada sprite.
        Bounds bounds = sprite.bounds;
        art.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, 0f);

        if (interactive)
        {
            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(root.transform, false);
            var box = trigger.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.6f, 3.6f);
            box.offset = new Vector2(0f, 1.8f);

            // El script debe ir en el mismo GameObject que el collider trigger:
            // OnTriggerEnter2D se envía al objeto del collider, no al padre.
            var door = trigger.AddComponent<LevelDoor>();
            door.targetScene = targetScene;
        }

        if (withLight)
        {
            var lightGO = new GameObject("Luz del portal");
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 2f, 0f);

            var light = lightGO.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(0.35f, 0.75f, 1f, 1f);
            light.intensity = 1.6f;
            light.pointLightInnerRadius = 0.8f;
            light.pointLightOuterRadius = 9f;
            light.pointLightInnerAngle = 360f;
            light.pointLightOuterAngle = 360f;
        }

        return root;
    }

    // ------------------------------------------------------------------
    // Interfaz
    // ------------------------------------------------------------------

    private static void CreateLevelUI(string title, string subtitle, string controls, string levelLabel)
    {
        var canvasGO = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateText(canvas.transform, "Titulo", title,
            new Vector2(52f, -40f), new Vector2(1600f, 70f), 40, FontStyle.Bold,
            new Color(0.96f, 0.90f, 0.74f), top: true);

        CreateText(canvas.transform, "Subtitulo", subtitle,
            new Vector2(54f, -108f), new Vector2(1600f, 50f), 24, FontStyle.Italic,
            new Color(0.69f, 0.78f, 0.88f), top: true);

        CreateText(canvas.transform, "Controles", controls,
            new Vector2(54f, -172f), new Vector2(1300f, 140f), 25, FontStyle.Normal,
            new Color(0.82f, 0.89f, 0.96f), top: true);

        CreateText(canvas.transform, "Etiqueta nivel", levelLabel,
            new Vector2(-52f, -42f), new Vector2(400f, 50f), 24, FontStyle.Bold,
            new Color(0.55f, 0.74f, 0.9f), top: true, right: true);
    }

    private static Text CreateText(
        Transform parent, string name, string content, Vector2 position, Vector2 size,
        int fontSize, FontStyle style, Color color, bool top, bool right = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        if (top)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            rect.pivot = new Vector2(right ? 1f : 0f, 1f);
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
        }

        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = go.GetComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAnchor.UpperLeft;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    // ------------------------------------------------------------------
    // Build Settings / validación
    // ------------------------------------------------------------------

    private static void SetupBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Level1Path, true),
            new EditorBuildSettingsScene(Level2Path, true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", false),
        };
    }

    private static void ValidateScenes()
    {
        foreach (string path in new[] { Level1Path, Level2Path })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            int paintedTiles = tilemaps.Sum(t => t.GetUsedTilesCount());
            var doors = UnityEngine.Object.FindObjectsByType<LevelDoor>(FindObjectsSortMode.None);
            var players = UnityEngine.Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            Debug.Log($"LEVELBUILDER: {scene.name} | tilemaps={tilemaps.Length} tiles={paintedTiles} " +
                      $"puertas={doors.Length} jugadores={players.Length} canvas={canvases.Length}");
        }
    }

    // ------------------------------------------------------------------
    // Previsualización (opcional, solo para revisar el resultado)
    // ------------------------------------------------------------------

    [MenuItem("Herramientas/Nivel Olvidado/Renderizar previsualizaciones")]
    public static void RenderPreviews()
    {
        RenderScene(Level1Path, Path.Combine(Path.GetTempPath(), "nivel1_preview.png"));
        RenderScene(Level2Path, Path.Combine(Path.GetTempPath(), "nivel2_preview.png"));
    }

    private static void RenderScene(string scenePath, string outputPath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogError("LEVELBUILDER: no hay cámara principal en " + scenePath);
            return;
        }

        const int width = 1280;
        const int height = 720;

        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        camera.aspect = (float)width / height;
        camera.targetTexture = renderTexture;
        camera.Render();

        RenderTexture.active = renderTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(renderTexture);

        Debug.Log("LEVELBUILDER: preview guardada en " + outputPath);
    }
}
