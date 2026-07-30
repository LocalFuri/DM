using System.Collections;
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
    private bool entranceDoorFinalPhaseActive;
    private bool entranceDoorFinalMoveActive;
    private float entranceDoorOpenElapsed;
    private float entranceDoorFinalMoveStartElapsed;
    private float entranceDoorFinalMoveDuration;
    private float entranceDoorNextRattleElapsed;
    private int animatedEntranceDoorLeftX;
    private int animatedEntranceDoorRightX;
    private int entranceDoorFinalMoveFromLeftX;
    private int entranceDoorFinalMoveFromRightX;
    private int dungeonDrawOffsetY;

    private bool entranceViewportReady;
    private Coroutine entranceViewportPrepareCoroutine;
    private int entranceLayoutStabilizeFrames;

    private readonly List<string> visibleWallPieces = new();

    // Final wall pieces drawn in the most recent frame.
    public IReadOnlyList<string> VisibleWallPieces => visibleWallPieces;

    public bool IsEntranceBlockingInput =>
        !entranceViewportReady
        || (!entranceDoorOpened
            && (showEntranceScreen || entranceDoorOpening));

    private void Awake()
    {
      Debug.Log("DungeonRenderer Awake.");

      if (entranceDoorAudioSource == null)
        entranceDoorAudioSource = GetComponent<AudioSource>();

      if (dungeonViewport == null)
        dungeonViewport = FindDungeonViewport();

      CreateFrameBuffer();

      // Hide until CanvasScaler / viewport rect have settled so the first
      // visible entrance frame is not drawn at the pre-scaler scale.
      SetEntranceViewportVisible(false);
      entranceViewportReady = false;

      // Entrance RectTransform stays exactly as serialized in the scene.
      // Only ensure UV; never rewrite anchors/size (avoids canvas dirties).
      ApplyViewportPresentation();
    }

    private void Reset()
    {
      entranceDoorAudioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
      Debug.Log("DungeonRenderer Start.");

      if (entranceViewportPrepareCoroutine != null)
        StopCoroutine(entranceViewportPrepareCoroutine);

      entranceViewportPrepareCoroutine =
          StartCoroutine(PrepareEntranceViewportAfterLayoutSettles());
    }

    private IEnumerator PrepareEntranceViewportAfterLayoutSettles()
    {
      const float tolerance = 0.0001f;
      const int requiredStableSamples = 2;
      const int maxFrames = 60;

      Canvas canvas =
          dungeonViewport != null ? dungeonViewport.canvas : null;

      float previousScaleFactor = float.NaN;
      Rect previousRect = new Rect(
          float.NaN,
          float.NaN,
          float.NaN,
          float.NaN
      );
      int stableSamples = 0;
      int framesWaited = 0;

      while (framesWaited < maxFrames)
      {
        Canvas.ForceUpdateCanvases();
        framesWaited++;

        float scaleFactor =
            canvas != null ? canvas.scaleFactor : 0f;
        Rect viewportRect =
            dungeonViewport != null
                ? dungeonViewport.rectTransform.rect
                : Rect.zero;

        bool scaleReady = scaleFactor > 0f;
        bool scaleStable =
            !float.IsNaN(previousScaleFactor)
            && Mathf.Abs(scaleFactor - previousScaleFactor)
                <= tolerance;
        bool rectStable =
            !float.IsNaN(previousRect.x)
            && Mathf.Abs(viewportRect.x - previousRect.x) <= tolerance
            && Mathf.Abs(viewportRect.y - previousRect.y) <= tolerance
            && Mathf.Abs(viewportRect.width - previousRect.width)
                <= tolerance
            && Mathf.Abs(viewportRect.height - previousRect.height)
                <= tolerance;

        if (scaleReady && scaleStable && rectStable)
        {
          stableSamples++;
          if (stableSamples >= requiredStableSamples)
            break;
        }
        else
        {
          stableSamples = 0;
        }

        previousScaleFactor = scaleFactor;
        previousRect = viewportRect;
        yield return null;
      }

      // Ensure the map has been submitted so the first shown frame is valid.
      int mapWaitFrames = 0;
      while (currentMap == null && mapWaitFrames < maxFrames)
      {
        mapWaitFrames++;
        framesWaited++;
        yield return null;
      }

      ApplyViewportPresentation();
      frameDirty = true;

      // Draw into the RT while still hidden, then reveal.
      yield return new WaitForEndOfFrame();
      framesWaited++;

      entranceLayoutStabilizeFrames = framesWaited;
      entranceViewportReady = true;
      SetEntranceViewportVisible(true);
      entranceViewportPrepareCoroutine = null;

      string viewportRectText =
          dungeonViewport != null
              ? dungeonViewport.rectTransform.rect.ToString()
              : "n/a";

      Debug.Log(
          "DungeonRenderer: Entrance layout stable after " +
          $"{entranceLayoutStabilizeFrames} frame(s) " +
          $"(scaleFactor=" +
          $"{(canvas != null ? canvas.scaleFactor : 0f):0.####}, " +
          $"viewportRect={viewportRectText})."
      );
    }

    private void SetEntranceViewportVisible(bool visible)
    {
      if (dungeonViewport == null)
        return;

      dungeonViewport.enabled = visible;
    }

    private void Update()
    {
      if (!entranceViewportReady || !entranceDoorOpening)
        return;

      entranceDoorOpenElapsed += Time.unscaledDeltaTime;
      EnsureEntranceDoorFinalPhaseStarted();
      UpdateEntranceDoorPositions();

      // DoorLastMove + viewport only when both doors are fully open.
      if (AreEntranceDoorsFullyOpen())
      {
        CompleteEntranceTransition();
        return;
      }

      UpdateEntranceDoorChainedRattle();
      RequestRedraw();
    }

    public void OpenEntranceDoor()
    {
      if (!entranceViewportReady)
        return;

      if (entranceDoorOpening || entranceDoorOpened)
        return;

      entranceDoorOpening = true;
      entranceDoorOpened = false;
      entranceDoorFinalPhaseActive = false;
      entranceDoorFinalMoveActive = false;
      entranceDoorOpenElapsed = 0f;
      entranceDoorFinalMoveStartElapsed = -1f;
      entranceDoorFinalMoveDuration = 0f;
      entranceDoorNextRattleElapsed = -1f;
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

    private float GetEntranceDoorFinalMoveStartTime()
    {
      return entranceDoorOpenDuration * entranceDoorFinalMoveStart;
    }

    private float GetEntranceDoorFinalMoveDuration()
    {
      return entranceDoorOpenDuration
          * (1f - entranceDoorFinalMoveStart);
    }

    private float GetEntranceDoorRattleDuration()
    {
      if (entranceDoorOpenSound == null
          || entranceDoorOpenSound.length <= 0f)
      {
        return 0f;
      }

      return entranceDoorOpenSound.length;
    }

    private void EnsureEntranceDoorFinalPhaseStarted()
    {
      float finalMoveStartTime = GetEntranceDoorFinalMoveStartTime();
      if (entranceDoorFinalPhaseActive
          || entranceDoorOpenElapsed < finalMoveStartTime)
      {
        return;
      }

      // Leave early looping rattle; chain one-shots for the final move.
      StopEntranceDoorSound();
      entranceDoorFinalPhaseActive = true;
      entranceDoorFinalMoveStartElapsed = entranceDoorOpenElapsed;
      entranceDoorFinalMoveDuration = GetEntranceDoorFinalMoveDuration();
      entranceDoorNextRattleElapsed = entranceDoorOpenElapsed;
    }

    private void UpdateEntranceDoorChainedRattle()
    {
      // Never start another Door_Rattle after the entrance has finished.
      if (!entranceDoorOpening
          || entranceDoorOpened
          || !entranceDoorFinalPhaseActive)
      {
        return;
      }

      float rattleDuration = GetEntranceDoorRattleDuration();
      if (rattleDuration <= 0f
          || entranceDoorAudioSource == null
          || entranceDoorOpenSound == null)
      {
        return;
      }

      // Chain Door_Rattle clips with no overlap until doors finish.
      if (entranceDoorOpenElapsed < entranceDoorNextRattleElapsed)
        return;

      entranceDoorAudioSource.PlayOneShot(
          entranceDoorOpenSound,
          entranceDoorSoundVolume
      );
      entranceDoorNextRattleElapsed += rattleDuration;

      // Skip any missed slots after a hitch instead of stacking plays.
      while (entranceDoorNextRattleElapsed
          < entranceDoorOpenElapsed)
      {
        entranceDoorNextRattleElapsed += rattleDuration;
      }
    }

    private bool HasEntranceDoorFinalMoveFinished()
    {
      if (!entranceDoorFinalPhaseActive
          || entranceDoorFinalMoveStartElapsed < 0f
          || entranceDoorFinalMoveDuration <= 0f)
      {
        return false;
      }

      return entranceDoorOpenElapsed
          >= entranceDoorFinalMoveStartElapsed
          + entranceDoorFinalMoveDuration;
    }

    private bool AreEntranceDoorsFullyOpen()
    {
      return animatedEntranceDoorLeftX == EntranceDoorOpenEndLeftX
          && animatedEntranceDoorRightX == EntranceDoorOpenEndRightX;
    }

    private void CompleteEntranceTransition()
    {
      // Same frame: both doors fully open, stop rattles, play DoorLastMove,
      // switch gameplay viewport, end entrance, enable input.
      StopAllEntranceDoorSounds();
      PlayEntranceDoorLastMoveSound();

      entranceDoorOpening = false;
      entranceDoorOpened = true;
      entranceDoorFinalPhaseActive = false;
      entranceDoorNextRattleElapsed = float.PositiveInfinity;
      animatedEntranceDoorLeftX = EntranceDoorOpenEndLeftX;
      animatedEntranceDoorRightX = EntranceDoorOpenEndRightX;
      showEntranceScreen = false;

      DrawDungeonFrame();
      frameDirty = false;
      if (frameBuffer != null && targetTexture != null)
        Graphics.Blit(frameBuffer, targetTexture);

      ApplyViewportPresentation();
      Canvas.ForceUpdateCanvases();
      ApplyViewportPresentation();
    }

    private void StopAllEntranceDoorSounds()
    {
      if (entranceDoorAudioSource == null)
        return;

      entranceDoorAudioSource.Stop();
      entranceDoorAudioSource.loop = false;
      entranceDoorAudioSource.clip = null;
    }

    private void PlayEntranceDoorLastMoveSound()
    {
      if (entranceDoorAudioSource == null
          || entranceDoorLastMoveSound == null
          || entranceDoorLastMoveSound.length <= 0f)
      {
        return;
      }

      entranceDoorAudioSource.PlayOneShot(
          entranceDoorLastMoveSound,
          entranceDoorLastMoveVolume
      );
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
      float finalMoveStartTime = GetEntranceDoorFinalMoveStartTime();
      float finalMoveDuration = GetEntranceDoorFinalMoveDuration();

      int phase1EndLeftX = GetEntranceDoorPhase1EndLeftX();
      int phase1EndRightX = GetEntranceDoorPhase1EndRightX();

      if (entranceDoorOpenElapsed < finalMoveStartTime
          || finalMoveDuration <= 0f)
      {
        entranceDoorFinalMoveActive = false;

        float phase1Duration = finalMoveStartTime;
        float phase1Progress = phase1Duration > 0f
            ? Mathf.Clamp01(
                entranceDoorOpenElapsed / phase1Duration
            )
            : 1f;

        // Shared progress for both leaves.
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

      BeginEntranceDoorFinalMoveIfNeeded(
          phase1EndLeftX,
          phase1EndRightX,
          finalMoveDuration
      );

      float finalMoveElapsed =
          entranceDoorOpenElapsed - entranceDoorFinalMoveStartElapsed;
      if (entranceDoorFinalMoveStartElapsed < 0f)
        finalMoveElapsed = entranceDoorOpenElapsed - finalMoveStartTime;

      // One shared normalized progress for left and right.
      float finalProgress = Mathf.Clamp01(
          finalMoveElapsed / entranceDoorFinalMoveDuration
      );

      // Snap both ends only when progress reaches 1 so unequal travel
      // distances cannot RoundToInt one leaf to "fully open" early.
      if (finalProgress >= 1f || HasEntranceDoorFinalMoveFinished())
      {
        animatedEntranceDoorLeftX = EntranceDoorOpenEndLeftX;
        animatedEntranceDoorRightX = EntranceDoorOpenEndRightX;
        return;
      }

      animatedEntranceDoorLeftX = Mathf.RoundToInt(
          Mathf.Lerp(
              entranceDoorFinalMoveFromLeftX,
              EntranceDoorOpenEndLeftX,
              finalProgress
          )
      );
      animatedEntranceDoorRightX = Mathf.RoundToInt(
          Mathf.Lerp(
              entranceDoorFinalMoveFromRightX,
              EntranceDoorOpenEndRightX,
              finalProgress
          )
      );

      // Keep either leaf from appearing fully open before the shared end.
      if (animatedEntranceDoorLeftX == EntranceDoorOpenEndLeftX)
        animatedEntranceDoorLeftX = EntranceDoorOpenEndLeftX + 1;
      if (animatedEntranceDoorRightX == EntranceDoorOpenEndRightX)
        animatedEntranceDoorRightX = EntranceDoorOpenEndRightX - 1;
    }

    private void BeginEntranceDoorFinalMoveIfNeeded(
        int phase1EndLeftX,
        int phase1EndRightX,
        float finalMoveDuration)
    {
      if (entranceDoorFinalMoveActive)
        return;

      entranceDoorFinalMoveActive = true;
      if (entranceDoorFinalMoveDuration <= 0f)
        entranceDoorFinalMoveDuration = finalMoveDuration;
      if (entranceDoorFinalMoveStartElapsed < 0f)
        entranceDoorFinalMoveStartElapsed = entranceDoorOpenElapsed;

      entranceDoorFinalMoveFromLeftX = phase1EndLeftX;
      entranceDoorFinalMoveFromRightX = phase1EndRightX;
      animatedEntranceDoorLeftX = phase1EndLeftX;
      animatedEntranceDoorRightX = phase1EndRightX;
    }

    private static RawImage FindDungeonViewport()
    {
      RawImage[] images = UnityEngine.Object.FindObjectsByType<RawImage>(
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

      if (entranceViewportPrepareCoroutine != null)
      {
        StopCoroutine(entranceViewportPrepareCoroutine);
        entranceViewportPrepareCoroutine = null;
      }

      if (entranceDoorOpening)
        StopEntranceDoorSound();
    }

    private void OnDestroy()
    {
      if (frameBuffer != null)
      {
        Destroy(frameBuffer);
      }
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

    private void HandleCameraPostRender(Camera camera)
    {
      if (camera != dungeonCamera)
        return;

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

      Graphics.Blit(frameBuffer, targetTexture);
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
