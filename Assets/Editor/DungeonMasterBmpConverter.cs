using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DungeonMasterBmpConverter
{
  // Dungeon Master's bright-orange transparency color.
  private const byte TransparentRed = 218;
  private const byte TransparentGreen = 145;
  private const byte TransparentBlue = 109;

  [MenuItem("Tools/Dungeon Master/Convert BMP Folder to Transparent PNG")]
  private static void ConvertFolder()
  {
    string sourceFolder = EditorUtility.OpenFolderPanel(
        "Select folder containing Dungeon Master BMP files",
        "",
        ""
    );

    if (string.IsNullOrEmpty(sourceFolder))
    {
      return;
    }

    string outputFolder = EditorUtility.OpenFolderPanel(
        "Select folder for converted PNG files",
        Application.dataPath,
        ""
    );

    if (string.IsNullOrEmpty(outputFolder))
    {
      return;
    }

    string[] bmpFiles = Directory.GetFiles(
        sourceFolder,
        "*.bmp",
        SearchOption.AllDirectories
    );

    if (bmpFiles.Length == 0)
    {
      EditorUtility.DisplayDialog(
          "Dungeon Master BMP Converter",
          "No BMP files were found in the selected folder.",
          "OK"
      );

      return;
    }

    int convertedCount = 0;
    int failedCount = 0;

    try
    {
      for (int index = 0; index < bmpFiles.Length; index++)
      {
        string bmpPath = bmpFiles[index];

        float progress = (float)index / bmpFiles.Length;

        EditorUtility.DisplayProgressBar(
            "Converting Dungeon Master BMP files",
            Path.GetFileName(bmpPath),
            progress
        );

        try
        {
          string relativePath = GetRelativePath(
              sourceFolder,
              Path.GetDirectoryName(bmpPath)
          );

          string destinationDirectory = outputFolder;

          if (!string.IsNullOrEmpty(relativePath))
          {
            destinationDirectory = Path.Combine(
                outputFolder,
                relativePath
            );
          }

          Directory.CreateDirectory(destinationDirectory);

          string outputFilename =
              Path.GetFileNameWithoutExtension(bmpPath) + ".png";

          string outputPath = Path.Combine(
              destinationDirectory,
              outputFilename
          );

          ConvertBmpToPng(bmpPath, outputPath);
          convertedCount++;
        }
        catch (Exception exception)
        {
          failedCount++;

          Debug.LogError(
              "Could not convert BMP file:\n" +
              bmpPath +
              "\n\n" +
              exception.Message
          );
        }
      }
    }
    finally
    {
      EditorUtility.ClearProgressBar();
    }

    AssetDatabase.Refresh();

    ConfigureGeneratedPngImportSettings(outputFolder);

    EditorUtility.DisplayDialog(
        "Dungeon Master BMP Converter",
        "Conversion finished.\n\n" +
        "Converted: " + convertedCount + "\n" +
        "Failed: " + failedCount,
        "OK"
    );
  }

  private static void ConvertBmpToPng(
      string bmpPath,
      string outputPath
  )
  {
    using FileStream stream = File.OpenRead(bmpPath);
    using BinaryReader reader = new BinaryReader(stream);

    byte signatureB = reader.ReadByte();
    byte signatureM = reader.ReadByte();

    if (signatureB != 'B' || signatureM != 'M')
    {
      throw new InvalidDataException(
          "The file is not a valid BMP image."
      );
    }

    // File size.
    reader.ReadUInt32();

    // Reserved fields.
    reader.ReadUInt16();
    reader.ReadUInt16();

    uint pixelDataOffset = reader.ReadUInt32();

    uint dibHeaderSize = reader.ReadUInt32();

    if (dibHeaderSize < 40)
    {
      throw new NotSupportedException(
          "This BMP uses an unsupported header format."
      );
    }

    int width = reader.ReadInt32();
    int signedHeight = reader.ReadInt32();

    bool isTopDown = signedHeight < 0;
    int height = Math.Abs(signedHeight);

    ushort planes = reader.ReadUInt16();
    ushort bitsPerPixel = reader.ReadUInt16();
    uint compression = reader.ReadUInt32();

    // Image size.
    reader.ReadUInt32();

    // Horizontal and vertical resolution.
    reader.ReadInt32();
    reader.ReadInt32();

    uint colorsUsed = reader.ReadUInt32();

    // Important colors.
    reader.ReadUInt32();

    if (width <= 0 || height <= 0)
    {
      throw new InvalidDataException(
          "The BMP has invalid dimensions."
      );
    }

    if (planes != 1)
    {
      throw new NotSupportedException(
          "The BMP uses an unsupported number of color planes."
      );
    }

    if (bitsPerPixel != 8)
    {
      throw new NotSupportedException(
          "Only original 8-bit Dungeon Master BMP files are supported. " +
          "This file uses " + bitsPerPixel + " bits per pixel."
      );
    }

    if (compression != 0)
    {
      throw new NotSupportedException(
          "Compressed BMP files are not supported."
      );
    }

    // Skip additional DIB-header bytes when the header is larger than 40.
    long additionalHeaderBytes = dibHeaderSize - 40;

    if (additionalHeaderBytes > 0)
    {
      reader.BaseStream.Seek(
          additionalHeaderBytes,
          SeekOrigin.Current
      );
    }

    int paletteColorCount =
        colorsUsed > 0 ? (int)colorsUsed : 256;

    Color32[] palette = new Color32[paletteColorCount];

    for (int i = 0; i < paletteColorCount; i++)
    {
      byte blue = reader.ReadByte();
      byte green = reader.ReadByte();
      byte red = reader.ReadByte();

      // Reserved palette byte.
      reader.ReadByte();

      byte alpha = IsTransparencyColor(red, green, blue)
          ? (byte)0
          : (byte)255;

      palette[i] = new Color32(
          red,
          green,
          blue,
          alpha
      );
    }

    reader.BaseStream.Seek(
        pixelDataOffset,
        SeekOrigin.Begin
    );

    Color32[] pixels = new Color32[width * height];

    // Every BMP row is padded to a multiple of four bytes.
    int paddedRowSize = ((width + 3) / 4) * 4;

    byte[] rowData = new byte[paddedRowSize];

    for (int fileRow = 0; fileRow < height; fileRow++)
    {
      int bytesRead = reader.Read(
          rowData,
          0,
          paddedRowSize
      );

      if (bytesRead != paddedRowSize)
      {
        throw new EndOfStreamException(
            "The BMP pixel data ended unexpectedly."
        );
      }

      int textureY = isTopDown
          ? height - 1 - fileRow
          : fileRow;

      for (int x = 0; x < width; x++)
      {
        int paletteIndex = rowData[x];

        if (paletteIndex >= palette.Length)
        {
          throw new InvalidDataException(
              "The BMP contains an invalid palette index."
          );
        }

        pixels[(textureY * width) + x] =
            palette[paletteIndex];
      }
    }

    Texture2D texture = new Texture2D(
        width,
        height,
        TextureFormat.RGBA32,
        false
    );

    try
    {
      texture.SetPixels32(pixels);
      texture.Apply(false, false);

      byte[] pngData = texture.EncodeToPNG();

      Directory.CreateDirectory(
          Path.GetDirectoryName(outputPath)
      );

      File.WriteAllBytes(outputPath, pngData);
    }
    finally
    {
      UnityEngine.Object.DestroyImmediate(texture);
    }
  }

  private static bool IsTransparencyColor(
      byte red,
      byte green,
      byte blue
  )
  {
    return red == TransparentRed &&
           green == TransparentGreen &&
           blue == TransparentBlue;
  }

  private static string GetRelativePath(
      string rootFolder,
      string currentFolder
  )
  {
    if (string.IsNullOrEmpty(currentFolder))
    {
      return "";
    }

    Uri rootUri = new Uri(
        EnsureTrailingDirectorySeparator(rootFolder)
    );

    Uri currentUri = new Uri(
        EnsureTrailingDirectorySeparator(currentFolder)
    );

    string relativePath = Uri.UnescapeDataString(
        rootUri.MakeRelativeUri(currentUri).ToString()
    );

    return relativePath
        .Replace('/', Path.DirectorySeparatorChar)
        .TrimEnd(Path.DirectorySeparatorChar);
  }

  private static string EnsureTrailingDirectorySeparator(
      string path
  )
  {
    if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
    {
      path += Path.DirectorySeparatorChar;
    }

    return path;
  }

  private static void ConfigureGeneratedPngImportSettings(
      string outputFolder
  )
  {
    string normalizedOutputFolder =
        Path.GetFullPath(outputFolder)
            .Replace('\\', '/');

    string normalizedAssetsFolder =
        Path.GetFullPath(Application.dataPath)
            .Replace('\\', '/');

    // Import settings can only be changed when the PNGs are inside Assets.
    if (!normalizedOutputFolder.StartsWith(
            normalizedAssetsFolder,
            StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    string[] pngFiles = Directory.GetFiles(
        outputFolder,
        "*.png",
        SearchOption.AllDirectories
    );

    foreach (string pngFile in pngFiles)
    {
      string normalizedPngPath =
          Path.GetFullPath(pngFile)
              .Replace('\\', '/');

      string assetPath =
          "Assets" +
          normalizedPngPath.Substring(
              normalizedAssetsFolder.Length
          );

      TextureImporter importer =
          AssetImporter.GetAtPath(assetPath)
          as TextureImporter;

      if (importer == null)
      {
        continue;
      }

      importer.textureType = TextureImporterType.Sprite;
      importer.spriteImportMode = SpriteImportMode.Single;
      importer.filterMode = FilterMode.Point;
      importer.textureCompression =
          TextureImporterCompression.Uncompressed;
      importer.mipmapEnabled = false;
      importer.alphaIsTransparency = true;
      importer.wrapMode = TextureWrapMode.Clamp;

      importer.SaveAndReimport();
    }
  }
}