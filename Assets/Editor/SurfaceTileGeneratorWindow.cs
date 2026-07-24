using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Tools > Surfaces > SurfaceTile Generator (Floors, 2026-07-23): the
// time-saver for the floor-audio pass. Unity's built-in "generate tiles from
// spritesheet" makes PLAIN UnityEngine.Tilemaps.Tile assets, which carry no
// SurfaceType - so any floor painted with them falls back to concrete in
// PlayerAudio/SurfaceDetector. This window batch-creates SurfaceTile assets
// (which DO carry a SurfaceType) from whatever sprites you have selected.
//
// Workflow: in the Project window select a sliced texture (grabs all its
// sub-sprites) OR expand it and multi-select just the sprites that share one
// material, pick the SurfaceType, hit Generate. Repeat per material. One asset
// per sprite lands in the output folder, named after the sprite so it lines up
// 1:1 with the source sheet. Re-running with Overwrite on re-tags existing
// SurfaceTiles in place (keeps their GUID, so palettes/tilemaps that already
// reference them survive). Then drag the generated tiles into a Tile Palette.
//
// SurfaceType labels are code-coupled to the FMOD "SurfaceType" labeled param
// (see CLAUDE.md Known Gotchas) - this tool only ever writes enum values, so
// the coupling can't drift here.
public class SurfaceTileGeneratorWindow : EditorWindow
{
  private const string DefaultOutputFolder = "Assets/Tiles/SurfaceTiles/Generated";

  private SurfaceType surfaceType = SurfaceType.concrete;
  private Tile.ColliderType colliderType = Tile.ColliderType.None;
  private string outputFolder = DefaultOutputFolder;
  private bool overwrite = true;

  private Vector2 scroll;
  private readonly List<string> report = new List<string>();

  [MenuItem("Tools/Surfaces/SurfaceTile Generator")]
  private static void Open()
  {
    GetWindow<SurfaceTileGeneratorWindow>("SurfaceTiles");
  }

  private void OnGUI()
  {
    EditorGUILayout.HelpBox(
      "Batch-creates SurfaceTile assets (which carry a SurfaceType for footstep audio) from the sprites you have " +
      "selected in the Project window. Select a sliced texture to grab all its sub-sprites, or multi-select just " +
      "the sprites that share one material. One .asset per sprite, named after the sprite. Plain Tiles from Unity's " +
      "default generator have NO surface data - use this instead so painted floors sound right.",
      MessageType.Info);

    List<Sprite> sprites = CollectSelectedSprites();
    EditorGUILayout.LabelField("Sprites in selection", sprites.Count.ToString());

    surfaceType = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", surfaceType);
    colliderType = (Tile.ColliderType)EditorGUILayout.EnumPopup(
      new GUIContent("Collider Type", "Floors should stay None so the player doesn't collide with the ground."),
      colliderType);
    overwrite = EditorGUILayout.Toggle(
      new GUIContent("Overwrite / Re-tag", "If a SurfaceTile of the same name exists, update its sprite + type in place (keeps its GUID)."),
      overwrite);

    using (new EditorGUILayout.HorizontalScope())
    {
      outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
      if (GUILayout.Button("...", GUILayout.Width(28)))
      {
        string picked = EditorUtility.OpenFolderPanel("Output folder for SurfaceTiles", outputFolder, "");
        if (!string.IsNullOrEmpty(picked))
        {
          outputFolder = ToProjectRelative(picked) ?? outputFolder;
        }
      }
    }

    using (new EditorGUI.DisabledScope(sprites.Count == 0))
    {
      if (GUILayout.Button($"Generate {sprites.Count} SurfaceTile(s)", GUILayout.Height(30)))
      {
        Generate(sprites);
      }
    }

    if (report.Count > 0)
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("Last run", EditorStyles.boldLabel);
      using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(140)))
      {
        scroll = sv.scrollPosition;
        foreach (string line in report)
        {
          EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
        }
      }
    }
  }

  // Sprites from the current selection: a selected Sprite counts directly; a
  // selected texture (or any asset) contributes all its sub-sprite
  // representations. De-duped, then natural-sorted so _0.._9.._10 stay in sheet
  // order rather than lexical (_1, _10, _11, _2...).
  private static List<Sprite> CollectSelectedSprites()
  {
    var sprites = new List<Sprite>();
    var seen = new HashSet<Sprite>();

    foreach (Object obj in Selection.objects)
    {
      if (obj is Sprite direct)
      {
        if (seen.Add(direct)) sprites.Add(direct);
        continue;
      }

      string path = AssetDatabase.GetAssetPath(obj);
      if (string.IsNullOrEmpty(path)) continue;

      foreach (Object rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
      {
        if (rep is Sprite sub && seen.Add(sub)) sprites.Add(sub);
      }
    }

    sprites.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
    return sprites;
  }

  private void Generate(List<Sprite> sprites)
  {
    report.Clear();

    if (!EnsureFolder(outputFolder))
    {
      report.Add($"ERROR: could not create output folder '{outputFolder}'.");
      return;
    }

    int created = 0;
    int retagged = 0;
    int skipped = 0;
    var touched = new List<Object>();

    try
    {
      AssetDatabase.StartAssetEditing();

      foreach (Sprite sprite in sprites)
      {
        string assetPath = $"{outputFolder}/{sprite.name}.asset";
        SurfaceTile existing = AssetDatabase.LoadAssetAtPath<SurfaceTile>(assetPath);

        if (existing != null)
        {
          if (!overwrite)
          {
            skipped++;
            continue;
          }

          existing.sprite = sprite;
          existing.surfaceType = surfaceType;
          existing.colliderType = colliderType;
          EditorUtility.SetDirty(existing);
          touched.Add(existing);
          retagged++;
          continue;
        }

        // A non-SurfaceTile asset already sits here (e.g. a plain Tile from the
        // built-in generator). Don't clobber it silently - a delete would
        // change the GUID and break anything referencing it.
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
        {
          skipped++;
          report.Add($"SKIP (non-SurfaceTile asset exists): {assetPath}");
          continue;
        }

        SurfaceTile tile = CreateInstance<SurfaceTile>();
        tile.name = sprite.name;
        tile.sprite = sprite;
        tile.surfaceType = surfaceType;
        tile.colliderType = colliderType;
        tile.color = Color.white;
        AssetDatabase.CreateAsset(tile, assetPath);
        touched.Add(tile);
        created++;
      }
    }
    finally
    {
      AssetDatabase.StopAssetEditing();
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
    }

    report.Add($"Done: {created} created, {retagged} re-tagged, {skipped} skipped -> {surfaceType}");
    if (touched.Count > 0)
    {
      Selection.objects = touched.ToArray();
      EditorGUIUtility.PingObject(touched[0]);
    }
  }

  private static bool EnsureFolder(string folder)
  {
    folder = folder.Replace('\\', '/').TrimEnd('/');
    if (AssetDatabase.IsValidFolder(folder)) return true;
    if (!folder.StartsWith("Assets")) return false;

    string[] parts = folder.Split('/');
    string current = parts[0];
    for (int i = 1; i < parts.Length; i++)
    {
      string next = $"{current}/{parts[i]}";
      if (!AssetDatabase.IsValidFolder(next))
      {
        AssetDatabase.CreateFolder(current, parts[i]);
      }
      current = next;
    }
    return AssetDatabase.IsValidFolder(folder);
  }

  private static string ToProjectRelative(string absolute)
  {
    absolute = absolute.Replace('\\', '/');
    string root = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
    if (absolute.StartsWith(root + "/Assets"))
    {
      return absolute.Substring(root.Length + 1);
    }
    return null;
  }
}
