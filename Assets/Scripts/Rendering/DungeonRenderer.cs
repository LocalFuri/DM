using System.Collections.Generic;
using DM.Dungeon;
using UnityEngine;
using UnityEngine.UI;

namespace DM.Rendering
{
  [RequireComponent(typeof(AudioSource))]
  public class DungeonRenderer : MonoBehaviour
  {
    private const int DefaultViewWidth = 320;
    private const int DefaultViewHeight = 200;
    private const int DungeonViewWidth = 224;
    private const int DungeonViewHeight = 136;

    [Header("Rendering")]
    [SerializeField] private Camera dungeonCamera;
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private RawImage dungeonViewport;

    [Header("Viewport Layout")]
    [SerializeField] private ViewportLayout layout;

    [Header("Graphics Database")]
    [SerializeField] private DungeonGraphics graphics;

    [Header("Entrance")]
    [SerializeField] private bool showEntranceScreen = true;
    [SerializeField] private int entranceDoorLeftX = 0;
    [SerializeField] private int entranceDoorLeftY = 0;
    [SerializeField] private int entranceDoorRightX = 0;
    [SerializeField] private int entranceDoorRightY = 0;
    [SerializeField]
    [Min(0.1f)]
    private float entranceDoorOpenDuration = 3.0f;
    [SerializeField]
    [Range(0.5f, 0.95f)]
    private float entranceDoorFinalMoveStart = 0.8f;

    [Header("Entrance Door Audio")]
    [SerializeField] private AudioSource entranceDoorAudioSource;
    [SerializeField] private AudioClip entranceDoorOpenSound;
    [Range(0f, 1f)]
    [SerializeField] private float entranceDoorSoundVolume = 1.0f;
    [SerializeField] private AudioClip entranceDoorLastMoveSound;
    [Range(0f, 1f)]
    [SerializeField] private float entranceDoorLastMoveVolume = 1.0f;

    [Header("Startup Viewport Debug")]
    [SerializeField] private bool debugStartupViewportFrames = true;
    [SerializeField]
    [Min(0.05f)]
    private float debugStartupFrameHoldSeconds = 2f;
    [SerializeField] private bool debugShowViewportChangeWarning = true;
    [SerializeField]
    [Min(0.05f)]
    private float debugViewportWarningDuration = 0.25f;

    private static readonly Rect EntranceUvRect = new Rect(0f, 0f, 1f, 1f);
    private static readonly Rect DungeonUvRect = new Rect(
        0f,
        0f,
        DungeonViewWidth / (float)DefaultViewWidth,
        DungeonViewHeight / (float)DefaultViewHeight
    );

    private const int EntranceDoorOpenStartLeftX = 0;
    private const int EntranceDoorOpenStartRightX = 105;
    private const int EntranceDoorOpenEndLeftX = -105;
    private const int EntranceDoorOpenEndRightX = 320;
    private const int EntranceDoorOpenY = 16;
    private const int EntranceDungeonOffsetY = 31;

    private Texture2D frameBuffer;
    private Color32[] framePixels;

    private int viewWidth;
    private int viewHeight;

    private DungeonMap currentMap;
    private bool frameDirty = true;

    private bool entranceDoorOpening;
    private bool entranceDoorOpened;
    private bool entranceDoorLastMovePlayed;
    private float entranceDoorOpenElapsed;
    private int animatedEntranceDoorLeftX;
    private int animatedEntranceDoorRightX;
    private int dungeonDrawOffsetY;

    private bool debugStartupActive;
    private int debugStartupFrame;
    private bool debugAwaitingSample;
    private bool debugHoldingFrame;
    private float debugHoldEndRealtime = -1f;
    private bool debugHasPreviousSnapshot;
    private ViewportDebugSnapshot debugPreviousSnapshot;
    private GameObject debugWarningRoot;
    private Text debugWarningText;
    private float debugWarningHideRealtime = -1f;
    private GameObject debugStatusRoot;
    private Text debugStatusText;
    private bool debugEditorUpdateSubscribed;

    private readonly List<string> visibleWallPieces = new();

    private const float DebugViewportCompareTolerance = 0.001f;
    private const int DebugStartupFrameCount = 10;

    // Final wall pieces drawn in the most recent frame.
    public IReadOnlyList<string> VisibleWallPieces => visibleWallPieces;

    public bool IsEntranceBlockingInput =>
        debugStartupActive
        || (showEntranceScreen && !entranceDoorOpened);

    private void Awake()
    {
      Debug.Log("DungeonRenderer Awake.");

      if (entranceDoorAudioSource == null)
        entranceDoorAudioSource = GetComponent<AudioSource>();

      if (dungeonViewport == null)
        dungeonViewport = FindDungeonViewport();

      CreateFrameBuffer();

      // Entrance RectTransform stays exactly as serialized in the scene.
      // Only ensure UV; never rewrite anchors/size (avoids canvas dirties).
      ApplyViewportPresentation();
      BeginStartupViewportDebug();
    }

    private void Reset()
    {
      entranceDoorAudioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
      Debug.Log("DungeonRenderer Start.");
    }

    private void LateUpdate()
    {
      UpdateStartupViewportDebug();
    }

    private void Update()
    {
      // Hold entrance simulation while the frame-step diagnostic is active.
      if (debugStartupActive)
        return;

      if (!entranceDoorOpening)
        return;

      entranceDoorOpenElapsed += Time.unscaledDeltaTime;
      UpdateEntranceDoorPositions();
      RequestRedraw();

      float t = Mathf.Clamp01(
          entranceDoorOpenElapsed / entranceDoorOpenDuration
      );

      if (t >= 1f)
      {
        entranceDoorOpening = false;
        entranceDoorOpened = true;
        animatedEntranceDoorLeftX = EntranceDoorOpenEndLeftX;
        animatedEntranceDoorRightX = EntranceDoorOpenEndRightX;

        // DoorLastMove is already playing via PlayOneShot; do not
        // Stop() or it would cut that final sound off.
        if (!entranceDoorLastMovePlayed)
          StopEntranceDoorSound();

        showEntranceScreen = false;
        ApplyViewportPresentation();
        Canvas.ForceUpdateCanvases();
        ApplyViewportPresentation();
        RequestRedraw();
      }
    }

    public void OpenEntranceDoor()
    {
      if (debugStartupActive)
        return;

      if (entranceDoorOpening || entranceDoorOpened)
        return;

      entranceDoorOpening = true;
      entranceDoorOpened = false;
      entranceDoorLastMovePlayed = false;
      entranceDoorOpenElapsed = 0f;
      animatedEntranceDoorLeftX = EntranceDoorOpenStartLeftX;
      animatedEntranceDoorRightX = EntranceDoorOpenStartRightX;

      if (entranceDoorAudioSource != null
          && entranceDoorOpenSound != null)
      {
        entranceDoorAudioSource.clip = entranceDoorOpenSound;
        entranceDoorAudioSource.loop = true;
        entranceDoorAudioSource.volume = entranceDoorSoundVolume;
        entranceDoorAudioSource.Play();
      }

      RequestRedraw();
    }

    private void StopEntranceDoorSound()
    {
      if (entranceDoorAudioSource == null)
        return;

      entranceDoorAudioSource.Stop();
      entranceDoorAudioSource.loop = false;
    }

    private bool TryGetEntranceDoorFinalMoveTiming(
        out float finalMoveStartTime,
        out float finalMoveDuration)
    {
      if (entranceDoorLastMoveSound == null
          || entranceDoorLastMoveSound.length <= 0f)
      {
        finalMoveDuration =
            entranceDoorOpenDuration
            * (1f - entranceDoorFinalMoveStart);
        finalMoveStartTime =
            entranceDoorOpenDuration - finalMoveDuration;
        return false;
      }

      finalMoveDuration = Mathf.Min(
          entranceDoorLastMoveSound.length,
          entranceDoorOpenDuration
      );
      finalMoveStartTime =
          entranceDoorOpenDuration - finalMoveDuration;
      return true;
    }

    private int GetEntranceDoorPhase1EndLeftX()
    {
      return Mathf.RoundToInt(
          Mathf.Lerp(
              EntranceDoorOpenStartLeftX,
              EntranceDoorOpenEndLeftX,
              entranceDoorFinalMoveStart
          )
      );
    }

    private int GetEntranceDoorPhase1EndRightX()
    {
      return Mathf.RoundToInt(
          Mathf.Lerp(
              EntranceDoorOpenStartRightX,
              EntranceDoorOpenEndRightX,
              entranceDoorFinalMoveStart
          )
      );
    }

    private void UpdateEntranceDoorPositions()
    {
      bool hasFinalMoveSound = TryGetEntranceDoorFinalMoveTiming(
          out float finalMoveStartTime,
          out float finalMoveDuration
      );

      int phase1EndLeftX = GetEntranceDoorPhase1EndLeftX();
      int phase1EndRightX = GetEntranceDoorPhase1EndRightX();

      if (entranceDoorOpenElapsed < finalMoveStartTime
          || finalMoveDuration <= 0f)
      {
        float phase1Duration = finalMoveStartTime;
        float phase1Progress = phase1Duration > 0f
            ? Mathf.Clamp01(
                entranceDoorOpenElapsed / phase1Duration
            )
            : 1f;

        animatedEntranceDoorLeftX = Mathf.RoundToInt(
            Mathf.Lerp(
                EntranceDoorOpenStartLeftX,
                phase1EndLeftX,
                phase1Progress
            )
        );
        animatedEntranceDoorRightX = Mathf.RoundToInt(
            Mathf.Lerp(
                EntranceDoorOpenStartRightX,
                phase1EndRightX,
                phase1Progress
            )
        );
        return;
      }

      if (hasFinalMoveSound)
        TryPlayEntranceDoorLastMoveSound();

      float finalProgress = Mathf.Clamp01(
          (entranceDoorOpenElapsed - finalMoveStartTime)
          / finalMoveDuration
      );
      float acceleratedProgress = finalProgress * finalProgress;

      animatedEntranceDoorLeftX = Mathf.RoundToInt(
          Mathf.Lerp(
              phase1EndLeftX,
              EntranceDoorOpenEndLeftX,
              acceleratedProgress
          )
      );
      animatedEntranceDoorRightX = Mathf.RoundToInt(
          Mathf.Lerp(
              phase1EndRightX,
              EntranceDoorOpenEndRightX,
              acceleratedProgress
          )
      );
    }

    private void TryPlayEntranceDoorLastMoveSound()
    {
      if (entranceDoorLastMovePlayed)
        return;

      if (entranceDoorLastMoveSound == null
          || entranceDoorLastMoveSound.length <= 0f)
      {
        return;
      }

      StopEntranceDoorSound();

      if (entranceDoorAudioSource != null)
      {
        entranceDoorAudioSource.PlayOneShot(
            entranceDoorLastMoveSound,
            entranceDoorLastMoveVolume
        );
      }

      entranceDoorLastMovePlayed = true;
    }

    private static RawImage FindDungeonViewport()
    {
      RawImage[] images = Object.FindObjectsByType<RawImage>(
          FindObjectsInactive.Exclude
      );

      foreach (RawImage image in images)
      {
        if (image != null && image.gameObject.name == "DungeonViewport")
          return image;
      }

      return null;
    }

    private void ApplyViewportPresentation()
    {
      if (dungeonViewport == null)
        return;

      RectTransform rectTransform = dungeonViewport.rectTransform;

      if (showEntranceScreen)
      {
        // Serialized full-screen RectTransform is the source of truth.
        // Do not rewrite anchors, offsets, or size while the entrance shows —
        // that dirties the canvas and fights CanvasScaler / pixel snapping.
        dungeonViewport.uvRect = EntranceUvRect;
        return;
      }

      dungeonViewport.uvRect = DungeonUvRect;

      RectTransform parent =
          rectTransform.parent as RectTransform;
      float parentWidth =
          parent != null ? parent.rect.width : DefaultViewWidth;
      float parentHeight =
          parent != null ? parent.rect.height : DefaultViewHeight;

      float aspect =
          DungeonViewWidth / (float)DungeonViewHeight;
      float fitWidth = parentWidth;
      float fitHeight = fitWidth / aspect;

      if (fitHeight > parentHeight)
      {
        fitHeight = parentHeight;
        fitWidth = fitHeight * aspect;
      }

      fitWidth = Mathf.Round(fitWidth);
      fitHeight = Mathf.Round(fitHeight);

      rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
      rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
      rectTransform.pivot = new Vector2(0.5f, 0.5f);
      rectTransform.anchoredPosition = Vector2.zero;
      rectTransform.sizeDelta = new Vector2(fitWidth, fitHeight);
    }

    private void OnEnable()
    {
      Camera.onPostRender += HandleCameraPostRender;
    }

    private void OnDisable()
    {
      Camera.onPostRender -= HandleCameraPostRender;

      if (entranceDoorOpening)
        StopEntranceDoorSound();

      CleanupStartupViewportDebug();
    }

    private void OnDestroy()
    {
      CleanupStartupViewportDebug();

      if (frameBuffer != null)
      {
        Destroy(frameBuffer);
      }
    }

    private struct ViewportDebugSnapshot
    {
      public float CanvasScaleFactor;
      public Rect ViewportRect;
      public Vector2 AnchoredPosition;
      public Vector2 SizeDelta;
      public Vector2 OffsetMin;
      public Vector2 OffsetMax;
      public Vector3 LocalScale;
      public Rect UvRect;
      public Rect ParentRect;
    }

    private void BeginStartupViewportDebug()
    {
      if (!debugStartupViewportFrames)
        return;

      // Never touch Time.timeScale — one-frame stepping uses Editor pause
      // (or a realtime hold outside the Editor).
      if (Time.timeScale != 1f)
        Time.timeScale = 1f;

      debugStartupActive = true;
      debugStartupFrame = 0;
      debugAwaitingSample = true;
      debugHoldingFrame = false;
      debugHoldEndRealtime = -1f;
      debugHasPreviousSnapshot = false;
      debugWarningHideRealtime = -1f;

      EnsureStartupDebugStatusLabel();
      SubscribeStartupDebugEditorUpdate();
      UpdateStartupDebugStatusLabel();
    }

    private void UpdateStartupViewportDebug()
    {
      if (!debugStartupActive)
        return;

      // Outside the Editor there is no player-loop pause, so the hold
      // timer is polled here with realtimeSinceStartup.
#if !UNITY_EDITOR
      TickStartupViewportFrameHold();
#endif

      UpdateStartupDebugWarningVisibility();
      UpdateStartupDebugStatusLabel();

      // Only sample on the single advanced frame; holds happen after.
      if (!debugAwaitingSample)
        return;

      debugAwaitingSample = false;
      ProcessStartupViewportDebugFrame();
    }

    private void ProcessStartupViewportDebugFrame()
    {
      if (debugStartupFrame > DebugStartupFrameCount)
      {
        CleanupStartupViewportDebug();
        return;
      }

      if (!TryCaptureViewportDebugSnapshot(out ViewportDebugSnapshot snapshot))
      {
        BeginStartupViewportFrameHold();
        return;
      }

      Debug.Log(
          "[EntranceViewport] " +
          $"frame={debugStartupFrame} " +
          $"scaleFactor={snapshot.CanvasScaleFactor:F6} " +
          $"rect={snapshot.ViewportRect} " +
          $"anchoredPosition={snapshot.AnchoredPosition} " +
          $"sizeDelta={snapshot.SizeDelta} " +
          $"offsetMin={snapshot.OffsetMin} " +
          $"offsetMax={snapshot.OffsetMax} " +
          $"localScale={snapshot.LocalScale} " +
          $"uvRect={snapshot.UvRect} " +
          $"parentRect={snapshot.ParentRect}"
      );

      if (debugHasPreviousSnapshot)
      {
        List<string> changed = CompareViewportDebugSnapshots(
            debugPreviousSnapshot,
            snapshot
        );

        if (changed.Count > 0)
        {
          LogEntranceViewportChanged(
              debugStartupFrame,
              debugPreviousSnapshot,
              snapshot,
              changed
          );

          if (debugShowViewportChangeWarning)
          {
            ShowViewportChangeWarning(
                debugStartupFrame,
                changed
            );
          }
        }
      }

      debugPreviousSnapshot = snapshot;
      debugHasPreviousSnapshot = true;
      BeginStartupViewportFrameHold();
    }

    private void BeginStartupViewportFrameHold()
    {
      debugHoldingFrame = true;
      debugHoldEndRealtime =
          Time.realtimeSinceStartup + debugStartupFrameHoldSeconds;
      UpdateStartupDebugStatusLabel();
      SetStartupDebugEditorPaused(true);
    }

    private void TickStartupViewportFrameHold()
    {
      if (!debugStartupActive || !debugHoldingFrame)
        return;

      UpdateStartupDebugWarningVisibility();
      UpdateStartupDebugStatusLabel();

      if (Time.realtimeSinceStartup < debugHoldEndRealtime)
        return;

      debugHoldingFrame = false;

      if (debugStartupFrame >= DebugStartupFrameCount)
      {
        CleanupStartupViewportDebug();
        return;
      }

      debugStartupFrame++;
      debugAwaitingSample = true;
      SetStartupDebugEditorPaused(false);
    }

    private void SubscribeStartupDebugEditorUpdate()
    {
#if UNITY_EDITOR
      if (debugEditorUpdateSubscribed)
        return;

      UnityEditor.EditorApplication.update +=
          HandleStartupDebugEditorUpdate;
      debugEditorUpdateSubscribed = true;
#endif
    }

    private void UnsubscribeStartupDebugEditorUpdate()
    {
#if UNITY_EDITOR
      if (!debugEditorUpdateSubscribed)
        return;

      UnityEditor.EditorApplication.update -=
          HandleStartupDebugEditorUpdate;
      debugEditorUpdateSubscribed = false;
#endif
    }

    private void HandleStartupDebugEditorUpdate()
    {
      TickStartupViewportFrameHold();
    }

    private void SetStartupDebugEditorPaused(bool paused)
    {
#if UNITY_EDITOR
      if (UnityEditor.EditorApplication.isPlaying)
        UnityEditor.EditorApplication.isPaused = paused;
#else
      // Player builds cannot pause the player loop; Tick is driven from
      // LateUpdate while holding (see UpdateStartupViewportDebug).
      _ = paused;
#endif
    }

    private bool TryCaptureViewportDebugSnapshot(
        out ViewportDebugSnapshot snapshot)
    {
      snapshot = default;

      if (dungeonViewport == null)
        return false;

      RectTransform rectTransform = dungeonViewport.rectTransform;
      Canvas canvas = dungeonViewport.canvas;
      RectTransform parent =
          rectTransform.parent as RectTransform;

      snapshot = new ViewportDebugSnapshot
      {
        CanvasScaleFactor =
            canvas != null ? canvas.scaleFactor : 0f,
        ViewportRect = rectTransform.rect,
        AnchoredPosition = rectTransform.anchoredPosition,
        SizeDelta = rectTransform.sizeDelta,
        OffsetMin = rectTransform.offsetMin,
        OffsetMax = rectTransform.offsetMax,
        LocalScale = rectTransform.localScale,
        UvRect = dungeonViewport.uvRect,
        ParentRect =
            parent != null ? parent.rect : Rect.zero
      };

      return true;
    }

    private static List<string> CompareViewportDebugSnapshots(
        ViewportDebugSnapshot previous,
        ViewportDebugSnapshot current)
    {
      List<string> changed = new();

      if (!Approximately(
              previous.CanvasScaleFactor,
              current.CanvasScaleFactor))
      {
        changed.Add("Canvas.scaleFactor");
      }

      if (!Approximately(previous.ViewportRect, current.ViewportRect))
        changed.Add("RectTransform.rect");

      if (!Approximately(
              previous.AnchoredPosition,
              current.AnchoredPosition))
      {
        changed.Add("anchoredPosition");
      }

      if (!Approximately(previous.SizeDelta, current.SizeDelta))
        changed.Add("sizeDelta");

      if (!Approximately(previous.OffsetMin, current.OffsetMin))
        changed.Add("offsetMin");

      if (!Approximately(previous.OffsetMax, current.OffsetMax))
        changed.Add("offsetMax");

      if (!Approximately(previous.LocalScale, current.LocalScale))
        changed.Add("localScale");

      if (!Approximately(previous.UvRect, current.UvRect))
        changed.Add("RawImage.uvRect");

      if (!Approximately(previous.ParentRect, current.ParentRect))
        changed.Add("parent.rect");

      return changed;
    }

    private static bool Approximately(float a, float b)
    {
      return Mathf.Abs(a - b) <= DebugViewportCompareTolerance;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
      return Approximately(a.x, b.x) && Approximately(a.y, b.y);
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
      return Approximately(a.x, b.x)
          && Approximately(a.y, b.y)
          && Approximately(a.z, b.z);
    }

    private static bool Approximately(Rect a, Rect b)
    {
      return Approximately(a.x, b.x)
          && Approximately(a.y, b.y)
          && Approximately(a.width, b.width)
          && Approximately(a.height, b.height);
    }

    private static void LogEntranceViewportChanged(
        int frame,
        ViewportDebugSnapshot previous,
        ViewportDebugSnapshot current,
        List<string> changed)
    {
      System.Text.StringBuilder builder = new();
      builder.AppendLine("[EntranceViewportChanged]");
      builder.AppendLine($"frame={frame}");
      builder.AppendLine(
          "changed=" + string.Join(", ", changed)
      );

      AppendChangedProperty(
          builder,
          "Canvas.scaleFactor",
          changed,
          previous.CanvasScaleFactor.ToString("F6"),
          current.CanvasScaleFactor.ToString("F6")
      );
      AppendChangedProperty(
          builder,
          "RectTransform.rect",
          changed,
          previous.ViewportRect.ToString(),
          current.ViewportRect.ToString()
      );
      AppendChangedProperty(
          builder,
          "anchoredPosition",
          changed,
          previous.AnchoredPosition.ToString(),
          current.AnchoredPosition.ToString()
      );
      AppendChangedProperty(
          builder,
          "sizeDelta",
          changed,
          previous.SizeDelta.ToString(),
          current.SizeDelta.ToString()
      );
      AppendChangedProperty(
          builder,
          "offsetMin",
          changed,
          previous.OffsetMin.ToString(),
          current.OffsetMin.ToString()
      );
      AppendChangedProperty(
          builder,
          "offsetMax",
          changed,
          previous.OffsetMax.ToString(),
          current.OffsetMax.ToString()
      );
      AppendChangedProperty(
          builder,
          "localScale",
          changed,
          previous.LocalScale.ToString(),
          current.LocalScale.ToString()
      );
      AppendChangedProperty(
          builder,
          "RawImage.uvRect",
          changed,
          previous.UvRect.ToString(),
          current.UvRect.ToString()
      );
      AppendChangedProperty(
          builder,
          "parent.rect",
          changed,
          previous.ParentRect.ToString(),
          current.ParentRect.ToString()
      );

      Debug.LogWarning(builder.ToString());
    }

    private static void AppendChangedProperty(
        System.Text.StringBuilder builder,
        string propertyName,
        List<string> changed,
        string previousValue,
        string currentValue)
    {
      if (!changed.Contains(propertyName))
        return;

      builder.AppendLine(
          $"{propertyName}: {previousValue} -> {currentValue}"
      );
    }

    private void ShowViewportChangeWarning(
        int frame,
        List<string> changed)
    {
      EnsureViewportChangeWarningOverlay();

      if (debugWarningRoot == null)
        return;

      debugWarningRoot.SetActive(true);
      debugWarningRoot.transform.SetAsLastSibling();
      if (debugStatusRoot != null)
        debugStatusRoot.transform.SetAsLastSibling();

      if (debugWarningText != null)
      {
        debugWarningText.text =
            "VIEWPORT CHANGED\n" +
            $"Frame: {frame}\n" +
            "Changed: " + string.Join(", ", changed);
      }

      debugWarningHideRealtime =
          Time.realtimeSinceStartup + debugViewportWarningDuration;
    }

    private void UpdateStartupDebugWarningVisibility()
    {
      if (debugWarningRoot == null || !debugWarningRoot.activeSelf)
        return;

      if (debugWarningHideRealtime < 0f)
        return;

      if (Time.realtimeSinceStartup < debugWarningHideRealtime)
        return;

      debugWarningRoot.SetActive(false);
      debugWarningHideRealtime = -1f;
    }

    private void EnsureStartupDebugStatusLabel()
    {
      if (debugStatusRoot != null)
        return;

      if (dungeonViewport == null)
        return;

      Transform parent = dungeonViewport.transform.parent;
      if (parent == null)
        return;

      debugStatusRoot = new GameObject(
          "StartupViewportDebugStatus",
          typeof(RectTransform),
          typeof(CanvasRenderer),
          typeof(Image)
      );
      debugStatusRoot.transform.SetParent(parent, false);

      RectTransform statusRect =
          debugStatusRoot.GetComponent<RectTransform>();
      statusRect.anchorMin = new Vector2(0f, 1f);
      statusRect.anchorMax = new Vector2(0f, 1f);
      statusRect.pivot = new Vector2(0f, 1f);
      statusRect.anchoredPosition = new Vector2(12f, -12f);
      statusRect.sizeDelta = new Vector2(420f, 70f);

      Image statusBackground =
          debugStatusRoot.GetComponent<Image>();
      Texture2D whiteTexture = Texture2D.whiteTexture;
      statusBackground.sprite = Sprite.Create(
          whiteTexture,
          new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
          new Vector2(0.5f, 0.5f),
          100f
      );
      statusBackground.color = new Color(0f, 0f, 0f, 0.65f);
      statusBackground.raycastTarget = false;

      GameObject textObject = new GameObject(
          "StatusText",
          typeof(RectTransform),
          typeof(CanvasRenderer),
          typeof(Text)
      );
      textObject.transform.SetParent(debugStatusRoot.transform, false);

      RectTransform textRect =
          textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(8f, 4f);
      textRect.offsetMax = new Vector2(-8f, -4f);

      debugStatusText = textObject.GetComponent<Text>();
      debugStatusText.alignment = TextAnchor.UpperLeft;
      debugStatusText.color = Color.white;
      debugStatusText.fontSize = 18;
      debugStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
      debugStatusText.verticalOverflow = VerticalWrapMode.Overflow;
      debugStatusText.raycastTarget = false;
      debugStatusText.font = Resources.GetBuiltinResource<Font>(
          "LegacyRuntime.ttf"
      );

      if (debugStatusText.font == null)
      {
        debugStatusText.font =
            Resources.GetBuiltinResource<Font>("Arial.ttf");
      }
    }

    private void UpdateStartupDebugStatusLabel()
    {
      if (!debugStartupActive || debugStatusText == null)
        return;

      float remaining = 0f;
      if (debugHoldingFrame)
      {
        remaining = Mathf.Max(
            0f,
            debugHoldEndRealtime - Time.realtimeSinceStartup
        );
      }

      debugStatusText.text =
          $"Diagnostic frame: {debugStartupFrame}\n" +
          $"Hold remaining: {remaining:F2}s";
    }

    private void EnsureViewportChangeWarningOverlay()
    {
      if (debugWarningRoot != null)
        return;

      if (dungeonViewport == null)
        return;

      Transform parent = dungeonViewport.transform.parent;
      if (parent == null)
        return;

      debugWarningRoot = new GameObject(
          "StartupViewportDebugWarning",
          typeof(RectTransform),
          typeof(CanvasRenderer),
          typeof(Image)
      );
      debugWarningRoot.transform.SetParent(parent, false);

      RectTransform overlayRect =
          debugWarningRoot.GetComponent<RectTransform>();
      overlayRect.anchorMin = Vector2.zero;
      overlayRect.anchorMax = Vector2.one;
      overlayRect.pivot = new Vector2(0.5f, 0.5f);
      overlayRect.anchoredPosition = Vector2.zero;
      overlayRect.sizeDelta = Vector2.zero;
      overlayRect.offsetMin = Vector2.zero;
      overlayRect.offsetMax = Vector2.zero;

      Image overlayImage = debugWarningRoot.GetComponent<Image>();
      Texture2D whiteTexture = Texture2D.whiteTexture;
      overlayImage.sprite = Sprite.Create(
          whiteTexture,
          new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
          new Vector2(0.5f, 0.5f),
          100f
      );
      overlayImage.type = Image.Type.Simple;
      overlayImage.color = new Color(1f, 0f, 0f, 0.65f);
      overlayImage.raycastTarget = false;

      GameObject textObject = new GameObject(
          "WarningText",
          typeof(RectTransform),
          typeof(CanvasRenderer),
          typeof(Text)
      );
      textObject.transform.SetParent(
          debugWarningRoot.transform,
          false
      );

      RectTransform textRect =
          textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;

      debugWarningText = textObject.GetComponent<Text>();
      debugWarningText.alignment = TextAnchor.MiddleCenter;
      debugWarningText.color = Color.white;
      debugWarningText.fontSize = 36;
      debugWarningText.horizontalOverflow =
          HorizontalWrapMode.Wrap;
      debugWarningText.verticalOverflow =
          VerticalWrapMode.Overflow;
      debugWarningText.raycastTarget = false;
      debugWarningText.font = Resources.GetBuiltinResource<Font>(
          "LegacyRuntime.ttf"
      );

      if (debugWarningText.font == null)
      {
        debugWarningText.font =
            Resources.GetBuiltinResource<Font>("Arial.ttf");
      }

      debugWarningRoot.SetActive(false);
    }

    private void CleanupStartupViewportDebug()
    {
      UnsubscribeStartupDebugEditorUpdate();
      SetStartupDebugEditorPaused(false);

      if (debugWarningRoot != null)
      {
        Destroy(debugWarningRoot);
        debugWarningRoot = null;
        debugWarningText = null;
      }

      if (debugStatusRoot != null)
      {
        Destroy(debugStatusRoot);
        debugStatusRoot = null;
        debugStatusText = null;
      }

      debugWarningHideRealtime = -1f;
      debugHasPreviousSnapshot = false;
      debugAwaitingSample = false;
      debugHoldingFrame = false;
      debugHoldEndRealtime = -1f;

      if (Time.timeScale != 1f)
        Time.timeScale = 1f;

      debugStartupActive = false;
    }

    public void Render(DungeonMap map)
    {
      Debug.Log("DungeonRenderer Render called.");

      currentMap = map;
      frameDirty = true;
    }

    public void RequestRedraw()
    {
      frameDirty = true;
    }

    private void HandleCameraPostRender(Camera renderedCamera)
    {
      if (renderedCamera != dungeonCamera)
      {
        return;
      }

      if (targetTexture == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Target Texture is missing."
        );

        return;
      }

      if (frameBuffer == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Framebuffer is missing."
        );

        return;
      }

      if (frameDirty)
      {
        DrawDungeonFrame();
        frameDirty = false;
      }

      Graphics.Blit(
          frameBuffer,
          targetTexture
      );
    }

    private void CreateFrameBuffer()
    {
      viewWidth =
          layout != null && layout.Width > 0
              ? layout.Width
              : DefaultViewWidth;

      viewHeight =
          layout != null && layout.Height > 0
              ? layout.Height
              : DefaultViewHeight;

      frameBuffer = new Texture2D(
          viewWidth,
          viewHeight,
          TextureFormat.RGBA32,
          false
      );

      frameBuffer.name = "Dungeon Frame Buffer";
      frameBuffer.filterMode = FilterMode.Point;
      frameBuffer.wrapMode = TextureWrapMode.Clamp;

      framePixels =
          new Color32[viewWidth * viewHeight];
    }

    private int GetEntranceDoorLeftDrawX()
    {
      if (entranceDoorOpening || entranceDoorOpened)
        return animatedEntranceDoorLeftX;

      return entranceDoorLeftX;
    }

    private int GetEntranceDoorRightDrawX()
    {
      if (entranceDoorOpening || entranceDoorOpened)
        return animatedEntranceDoorRightX;

      return entranceDoorRightX;
    }

    private int GetEntranceDoorLeftDrawY()
    {
      if (entranceDoorOpening || entranceDoorOpened)
        return EntranceDoorOpenY;

      return entranceDoorLeftY;
    }

    private int GetEntranceDoorRightDrawY()
    {
      if (entranceDoorOpening || entranceDoorOpened)
        return EntranceDoorOpenY;

      return entranceDoorRightY;
    }

    private void DrawDungeonFrame()
    {
      dungeonDrawOffsetY =
          showEntranceScreen ? EntranceDungeonOffsetY : 0;

      Clear(
          new Color32(
              0,
              0,
              0,
              255
          )
      );

      if (layout == null)
      {
        visibleWallPieces.Clear();
        Debug.LogWarning(
            "DungeonRenderer: ViewportLayout is missing."
        );

        ApplyFrameBuffer();
        return;
      }

      if (graphics == null)
      {
        visibleWallPieces.Clear();
        Debug.LogWarning(
            "DungeonRenderer: DungeonGraphics is missing."
        );

        ApplyFrameBuffer();
        return;
      }

      System.Text.StringBuilder drawnFrontWalls =
          new System.Text.StringBuilder();
      System.Text.StringBuilder drawnSideWalls =
          new System.Text.StringBuilder();

      visibleWallPieces.Clear();

      foreach (ViewportPiece piece in layout.Pieces)
      {
        if (!ShouldDrawPiece(piece))
          continue;

        if (TryGetCenterFrontWallDepth(piece.Graphic, out _))
        {
          if (drawnFrontWalls.Length > 0)
            drawnFrontWalls.Append(", ");

          drawnFrontWalls.Append(piece.Graphic);
          visibleWallPieces.Add(piece.Graphic.ToString());
        }
        else if (TryGetSideWallDepthAndSide(piece.Graphic, out _, out _))
        {
          if (drawnSideWalls.Length > 0)
            drawnSideWalls.Append(", ");

          drawnSideWalls.Append(piece.Graphic);
          visibleWallPieces.Add(piece.Graphic.ToString());
        }

        DrawPiece(piece);
      }

      string frontList =
          drawnFrontWalls.Length > 0
              ? drawnFrontWalls.ToString()
              : "(none)";
      string sideList =
          drawnSideWalls.Length > 0
              ? drawnSideWalls.ToString()
              : "(none)";

      if (currentMap != null)
      {
        Debug.Log(
            "DungeonRenderer walls at " +
            $"({currentMap.PlayerX},{currentMap.PlayerY}) " +
            $"facing {currentMap.PlayerFacing}: " +
            $"front=[{frontList}] sides=[{sideList}]"
        );
      }
      else
      {
        Debug.Log(
            "DungeonRenderer walls: " +
            $"front=[{frontList}] sides=[{sideList}]"
        );
      }

      if (showEntranceScreen)
        DrawEntranceOverlay();

      ApplyFrameBuffer();
    }

    private void DrawEntranceOverlay()
    {
      Texture2D entrance =
          graphics != null
              ? graphics.EntranceDoorClosedOutside
              : null;

      if (entrance == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: EntranceDoorClosedOutside " +
            "texture is missing."
        );
      }
      else
      {
        Blit(entrance, 0, 0);
      }

      Texture2D entranceDoorLeft =
          graphics != null
              ? graphics.EntranceDoorClosedLeft
              : null;

      if (entranceDoorLeft == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: EntranceDoorClosedLeft " +
            "texture is missing."
        );
      }
      else
      {
        Blit(
            entranceDoorLeft,
            GetEntranceDoorLeftDrawX(),
            GetEntranceDoorLeftDrawY()
        );
      }

      Texture2D entranceDoorRight =
          graphics != null
              ? graphics.EntranceDoorClosedRight
              : null;

      if (entranceDoorRight == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: EntranceDoorClosedRight " +
            "texture is missing."
        );
      }
      else
      {
        Blit(
            entranceDoorRight,
            GetEntranceDoorRightDrawX(),
            GetEntranceDoorRightDrawY()
        );
      }
    }

    private bool ShouldDrawPiece(ViewportPiece piece)
    {
      if (piece == null)
        return false;

      if (piece.Graphic == DungeonGraphicType.None)
        return false;

      if (IsDepthWallGraphic(piece.Graphic))
      {
        if (currentMap == null)
          return piece.Enabled;

        return IsDepthWallVisible(piece.Graphic);
      }

      return piece.Enabled;
    }

    private static bool IsDepthWallGraphic(DungeonGraphicType graphic)
    {
      switch (graphic)
      {
        case DungeonGraphicType.WallF0L:
        case DungeonGraphicType.WallF0R:
        case DungeonGraphicType.WallF1L:
        case DungeonGraphicType.WallF1R:
        case DungeonGraphicType.WallF2L:
        case DungeonGraphicType.WallF2R:
        case DungeonGraphicType.WallF3L:
        case DungeonGraphicType.WallF3R:
        case DungeonGraphicType.FrontWallF1:
        case DungeonGraphicType.FrontWallF2:
        case DungeonGraphicType.FrontWallF3:
          return true;
        default:
          return false;
      }
    }

    private static bool TryGetCenterFrontWallDepth(
        DungeonGraphicType graphic,
        out int depth)
    {
      switch (graphic)
      {
        case DungeonGraphicType.FrontWallF1:
          depth = 1;
          return true;
        case DungeonGraphicType.FrontWallF2:
          depth = 2;
          return true;
        case DungeonGraphicType.FrontWallF3:
          depth = 3;
          return true;
        default:
          depth = 0;
          return false;
      }
    }

    private static bool TryGetSideWallDepthAndSide(
        DungeonGraphicType graphic,
        out int depth,
        out bool isLeft)
    {
      switch (graphic)
      {
        case DungeonGraphicType.WallF0L:
          depth = 0;
          isLeft = true;
          return true;
        case DungeonGraphicType.WallF0R:
          depth = 0;
          isLeft = false;
          return true;
        case DungeonGraphicType.WallF1L:
          depth = 1;
          isLeft = true;
          return true;
        case DungeonGraphicType.WallF1R:
          depth = 1;
          isLeft = false;
          return true;
        case DungeonGraphicType.WallF2L:
          depth = 2;
          isLeft = true;
          return true;
        case DungeonGraphicType.WallF2R:
          depth = 2;
          isLeft = false;
          return true;
        case DungeonGraphicType.WallF3L:
          depth = 3;
          isLeft = true;
          return true;
        case DungeonGraphicType.WallF3R:
          depth = 3;
          isLeft = false;
          return true;
        default:
          depth = 0;
          isLeft = false;
          return false;
      }
    }

    private bool IsDepthWallVisible(DungeonGraphicType graphic)
    {
      if (TryGetCenterFrontWallDepth(graphic, out int centerDepth))
        return IsCenterFrontWallVisible(centerDepth);

      if (TryGetSideWallDepthAndSide(
              graphic,
              out int sideDepth,
              out bool isLeft))
      {
        return IsSideWallVisible(sideDepth, isLeft);
      }

      return false;
    }

    private bool IsCenterFrontWallVisible(int depth)
    {
      if (depth < 1)
        return false;

      if (IsFrontDepthOccluded(depth))
        return false;

      DungeonMap.GetForwardOffset(
          currentMap.PlayerFacing,
          out int forwardX,
          out int forwardY);

      int tileX = currentMap.PlayerX + forwardX * depth;
      int tileY = currentMap.PlayerY + forwardY * depth;

      return IsWallTile(tileX, tileY);
    }

    private bool IsSideWallVisible(int depth, bool isLeft)
    {
      // F0 sides stay independent of center front walls.
      if (depth > 0)
      {
        if (IsFrontDepthOccluded(depth))
          return false;

        // A center front wall at this depth replaces the side pieces.
        if (IsCenterFrontWallVisible(depth))
          return false;
      }

      DungeonMap.GetForwardOffset(
          currentMap.PlayerFacing,
          out int forwardX,
          out int forwardY);

      DungeonMap.GetRightOffset(
          currentMap.PlayerFacing,
          out int rightX,
          out int rightY);

      int sideX = isLeft ? -rightX : rightX;
      int sideY = isLeft ? -rightY : rightY;

      int tileX =
          currentMap.PlayerX +
          forwardX * depth +
          sideX;

      int tileY =
          currentMap.PlayerY +
          forwardY * depth +
          sideY;

      return IsWallTile(tileX, tileY);
    }

    // A solid center tile at a nearer depth hides farther
    // center and side wall pieces (F1 hides depth 2-3, F2 hides depth 3).
    private bool IsFrontDepthOccluded(int depth)
    {
      if (depth <= 0)
        return false;

      DungeonMap.GetForwardOffset(
          currentMap.PlayerFacing,
          out int forwardX,
          out int forwardY);

      for (int nearer = 1; nearer < depth; nearer++)
      {
        int tileX =
            currentMap.PlayerX + forwardX * nearer;
        int tileY =
            currentMap.PlayerY + forwardY * nearer;

        if (IsWallTile(tileX, tileY))
          return true;
      }

      return false;
    }

    private bool IsWallTile(int x, int y)
    {
      if (!currentMap.IsInside(x, y))
        return true;

      return currentMap.GetTile(x, y).Type ==
          DungeonTileType.Wall;
    }

    private void DrawPiece(ViewportPiece piece)
    {
      if (piece == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Null viewport piece."
        );

        return;
      }

      if (piece.Graphic == DungeonGraphicType.None)
      {
        return;
      }

      Texture2D texture =
          graphics.GetTexture(piece.Graphic);

      if (texture == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Missing texture for " +
            piece.Graphic
        );

        return;
      }

      Texture2D mask =
          graphics.GetMask(piece.Graphic, out bool flipMaskX);

      Blit(
          texture,
          piece.X,
          piece.Y + dungeonDrawOffsetY,
          mask,
          flipMaskX
      );
    }

    private void ApplyFrameBuffer()
    {
      frameBuffer.SetPixels32(framePixels);
      frameBuffer.Apply(false);
    }

    private void Clear(Color32 colour)
    {
      for (int i = 0; i < framePixels.Length; i++)
      {
        framePixels[i] = colour;
      }
    }

    private void Blit(
        Texture2D source,
        int destinationX,
        int destinationY,
        Texture2D mask = null,
        bool flipMaskHorizontal = false,
        bool flipVertical = false)
    {
      Color32[] sourcePixels =
          source.GetPixels32();

      Color32[] maskPixels = null;
      bool useMask = false;

      if (mask != null
          && mask.width == source.width
          && mask.height == source.height)
      {
        maskPixels = mask.GetPixels32();
        useMask = true;
      }

      for (
          int sourceY = 0;
          sourceY < source.height;
          sourceY++)
      {
        int sampleY = flipVertical
            ? source.height - 1 - sourceY
            : sourceY;

        int targetY =
            destinationY + sourceY;

        if (
            targetY < 0 ||
            targetY >= viewHeight)
        {
          continue;
        }

        for (
            int sourceX = 0;
            sourceX < source.width;
            sourceX++)
        {
          int targetX =
              destinationX + sourceX;

          if (
              targetX < 0 ||
              targetX >= viewWidth)
          {
            continue;
          }

          if (useMask)
          {
            int maskX = flipMaskHorizontal
                ? mask.width - 1 - sourceX
                : sourceX;

            Color32 maskColour =
                maskPixels[
                    sampleY * mask.width +
                    maskX
                ];

            // White (or bright) mask pixels are drawable;
            // black pixels clip.
            if (maskColour.r < 128
                && maskColour.g < 128
                && maskColour.b < 128)
            {
              continue;
            }
          }

          Color32 sourceColour =
              sourcePixels[
                  sampleY * source.width +
                  sourceX
              ];

          if (sourceColour.a == 0)
          {
            continue;
          }

          framePixels[
              targetY * viewWidth +
              targetX
          ] = sourceColour;
        }
      }
    }
  }
}
