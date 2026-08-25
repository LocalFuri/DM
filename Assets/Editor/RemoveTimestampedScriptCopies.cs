using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>
/// Deletes timestamped script copies such as
/// ScriptName(20260825-180558).cs so Unity does not compile duplicates.
/// </summary>
internal sealed class RemoveTimestampedScriptCopies : AssetPostprocessor
{
  private static readonly Regex TimestampedCopy = new Regex(
      @"\([^/\\]*\d{8}-\d{6}\)\.cs$",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
  );

  private static void OnPostprocessAllAssets(
      string[] importedAssets,
      string[] deletedAssets,
      string[] movedAssets,
      string[] movedFromAssetPaths)
  {
    for (int i = 0; i < importedAssets.Length; i++)
    {
      string assetPath = importedAssets[i];
      if (string.IsNullOrEmpty(assetPath) || !TimestampedCopy.IsMatch(assetPath))
        continue;

      string directory = Path.GetDirectoryName(assetPath);
      string fileName = Path.GetFileName(assetPath);
      int paren = fileName.IndexOf('(');
      if (paren <= 0 || directory == null)
        continue;

      string originalName = fileName.Substring(0, paren) + ".cs";
      string originalPath = Path.Combine(directory, originalName)
          .Replace('\\', '/');
      if (!File.Exists(originalPath))
        continue;

      string pathToDelete = assetPath;
      EditorApplication.delayCall += () =>
      {
        if (File.Exists(pathToDelete))
          AssetDatabase.DeleteAsset(pathToDelete);
      };
    }
  }
}
