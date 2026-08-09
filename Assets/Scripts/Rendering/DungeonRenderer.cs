using System.Collections;
using System.Collections.Generic;
using DM.Dungeon;
using DM.Heroes;
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
    private const int DungeonViewHeight = 164;
    // Authored dungeon-view pixels in the 320×200 buffer (excludes ceiling strips).
    private const int DungeonUvHeight = 136;

    private const int PartyAreaHeight = 36;
    private const int RightInterfaceWidth = 96;
    private const int RightInterfaceHeight = 164;
    private const int GameplayViewportX = 0;
    private const int GameplayViewportYFromTop = 36;
    private const int RightInterfaceX = 224;
    private const int RightInterfaceYFromTop = 36;

    [Header("Rendering")]
    [SerializeField] private Camera dungeonCamera;
    [SerializeField] private RenderTexture targetTexture;
    [SerializeField] private RawImage dungeonViewport;

    [Header("UI")]
    [SerializeField] private Image movementArrows;

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
    [SerializeField] private AudioSource entranceDoorOneShotAudioSource;
    [SerializeField] private AudioClip entranceDoorOpenSound;
    [Range(0f, 1f)]
    [SerializeField] private float entranceDoorSoundVolume = 1.0f;
    [SerializeField] private AudioClip entranceDoorLastMoveSound;
    [Range(0f, 1f)]
    [SerializeField] private float entranceDoorLastMoveVolume = 1.0f;

    [Header("Hero Portraits")]
    [SerializeField] private bool drawHeroPortraits = false;
    [SerializeField] private int heroPortraitX = 96;
    // Framebuffer Y (bottom-up). Screenshot top-down Y=36 for Iaido.png:
    // 136 - 36 - 29 = 71.
    [SerializeField] private int heroPortraitY = 71;
    [SerializeField] private Texture2D[] heroPortraitTextures;

    [Header("Bitmap Font")]
    [SerializeField] private DungeonBitmapFont bitmapFont;

    [Header("TEMP Reference Wall Test — disable or delete after comparison")]
    [SerializeField] private bool showOriginalWallReferenceTest = true;
    [SerializeField]
    private Texture2D originalWallReference_10_4_North;

    [Header("TEMP Exact Comparison")]
    [SerializeField]
    [Tooltip(
        "Show the full 320×200 framebuffer at 1:1 screen pixels, " +
        "centered, with no CanvasScaler enlargement or fitScale."
    )]
    private bool exact320x200ComparisonMode = false;

    private static readonly Rect EntranceUvRect = new Rect(0f, 0f, 1f, 1f);
    private static readonly Rect DungeonUvRect = new Rect(
        0f,
        0f,
        DungeonViewWidth / (float)DefaultViewWidth,
        DungeonUvHeight / (float)DefaultViewHeight
    );
    private static readonly Rect FullFrameUvRect = new Rect(0f, 0f, 1f, 1f);

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

    private RectTransform gameplayRoot;
    private RectTransform partyArea;
    private RectTransform rightInterfaceArea;

    private bool canvasScalerStateSaved;
    private CanvasScaler.ScaleMode savedScalerMode;
    private float savedScalerScaleFactor;
    private Vector2 savedScalerReferenceResolution;
    private float savedScalerMatchWidthOrHeight;
    private bool previousExact320x200ComparisonMode;

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

      EnsureEntranceDoorOneShotAudioSource();

      if (dungeonViewport == null)
        dungeonViewport = FindDungeonViewport();

      if (movementArrows == null)
        movementArrows = FindMovementArrows();

      if (bitmapFont == null)
        bitmapFont = GetComponent<DungeonBitmapFont>();

      CreateFrameBuffer();

      // Hide until CanvasScaler / viewport rect have settled so the first
      // visible entrance frame is not drawn at the pre-scaler scale.
      SetEntranceViewportVisible(false);
      entranceViewportReady = false;

      // Hidden for the whole entrance + door sequence; shown only when
      // the normal dungeon viewport becomes active.
      SetMovementArrowsVisible(false);

      // Entrance RectTransform stays exactly as serialized in the scene.
      // Only ensure UV; never rewrite anchors/size (avoids canvas dirties).
      ApplyViewportPresentation();
    }

    private void Reset()
    {
      entranceDoorAudioSource = GetComponent<AudioSource>();
      EnsureEntranceDoorOneShotAudioSource();
    }

    private void EnsureEntranceDoorOneShotAudioSource()
    {
      if (entranceDoorOneShotAudioSource == null)
      {
        AudioSource[] sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
          if (sources[i] != null
              && sources[i] != entranceDoorAudioSource)
          {
            entranceDoorOneShotAudioSource = sources[i];
            break;
          }
        }
      }

      if (entranceDoorOneShotAudioSource == null)
        entranceDoorOneShotAudioSource =
            gameObject.AddComponent<AudioSource>();

      entranceDoorOneShotAudioSource.playOnAwake = false;
      entranceDoorOneShotAudioSource.loop = false;
      entranceDoorOneShotAudioSource.volume = 1f;
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

    private float GetEntranceDoorRattlePlaybackDuration()
    {
      float clipLength = GetEntranceDoorRattleDuration();
      if (clipLength <= 0f)
        return 0f;

      float pitch = 1f;
      if (entranceDoorOneShotAudioSource != null)
        pitch = entranceDoorOneShotAudioSource.pitch;
      if (pitch <= 0.0001f)
        pitch = 1f;

      return clipLength / pitch;
    }

    private float GetRemainingEntranceDoorAnimationTime()
    {
      float animationEndElapsed = entranceDoorOpenDuration;
      if (entranceDoorFinalPhaseActive
          && entranceDoorFinalMoveStartElapsed >= 0f
          && entranceDoorFinalMoveDuration > 0f)
      {
        animationEndElapsed =
            entranceDoorFinalMoveStartElapsed
            + entranceDoorFinalMoveDuration;
      }

      return Mathf.Max(
          0f,
          animationEndElapsed - entranceDoorOpenElapsed
      );
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

      // No more rattles fit before DoorLastMove; stay silent.
      if (float.IsPositiveInfinity(entranceDoorNextRattleElapsed))
        return;

      float rattleDuration = GetEntranceDoorRattlePlaybackDuration();
      if (rattleDuration <= 0f
          || entranceDoorOneShotAudioSource == null
          || entranceDoorOpenSound == null)
      {
        return;
      }

      // Chain Door_Rattle clips with no overlap until doors finish.
      if (entranceDoorOpenElapsed < entranceDoorNextRattleElapsed)
        return;

      // Skip any last rattle that would be cut off by CompleteEntranceTransition.
      if (GetRemainingEntranceDoorAnimationTime() < rattleDuration)
      {
        entranceDoorNextRattleElapsed = float.PositiveInfinity;
        return;
      }

      entranceDoorOneShotAudioSource.PlayOneShot(
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
      if (entranceDoorAudioSource != null)
      {
        entranceDoorAudioSource.Stop();
        entranceDoorAudioSource.loop = false;
        entranceDoorAudioSource.clip = null;
      }

      if (entranceDoorOneShotAudioSource != null)
        entranceDoorOneShotAudioSource.Stop();
    }

    private void PlayEntranceDoorLastMoveSound()
    {
      if (entranceDoorOneShotAudioSource == null
          || entranceDoorLastMoveSound == null
          || entranceDoorLastMoveSound.length <= 0f)
      {
        return;
      }

      entranceDoorOneShotAudioSource.PlayOneShot(
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

    private static Image FindMovementArrows()
    {
      Image[] images = UnityEngine.Object.FindObjectsByType<Image>(
          FindObjectsInactive.Include
      );

      foreach (Image image in images)
      {
        if (image != null && image.gameObject.name == "MovementArrows")
          return image;
      }

      return null;
    }

    private void SetMovementArrowsVisible(bool visible)
    {
      if (movementArrows == null)
        return;

      movementArrows.gameObject.SetActive(visible);
    }

    private void EnsureGameplayLayoutHierarchy()
    {
      if (dungeonViewport == null)
        return;

      RectTransform canvasRect =
          dungeonViewport.canvas != null
              ? dungeonViewport.canvas.transform as RectTransform
              : dungeonViewport.rectTransform.parent as RectTransform;

      if (canvasRect == null)
        return;

      if (gameplayRoot == null)
      {
        GameObject rootObject = new GameObject(
            "GameplayRoot",
            typeof(RectTransform)
        );
        rootObject.layer = canvasRect.gameObject.layer;
        gameplayRoot = rootObject.GetComponent<RectTransform>();
        gameplayRoot.SetParent(canvasRect, false);
      }

      if (partyArea == null)
      {
        GameObject partyObject = new GameObject(
            "PartyArea",
            typeof(RectTransform)
        );
        partyObject.layer = gameplayRoot.gameObject.layer;
        partyArea = partyObject.GetComponent<RectTransform>();
        partyArea.SetParent(gameplayRoot, false);
      }

      if (rightInterfaceArea == null)
      {
        GameObject rightObject = new GameObject(
            "RightInterfaceArea",
            typeof(RectTransform)
        );
        rightObject.layer = gameplayRoot.gameObject.layer;
        rightInterfaceArea = rightObject.GetComponent<RectTransform>();
        rightInterfaceArea.SetParent(gameplayRoot, false);
      }

      dungeonViewport.rectTransform.SetParent(gameplayRoot, false);
      if (movementArrows != null)
        movementArrows.rectTransform.SetParent(gameplayRoot, false);

      // Draw order: party, viewport, right interface, arrows on top.
      partyArea.SetSiblingIndex(0);
      dungeonViewport.rectTransform.SetSiblingIndex(1);
      rightInterfaceArea.SetSiblingIndex(2);
      if (movementArrows != null)
        movementArrows.rectTransform.SetSiblingIndex(3);
      gameplayRoot.SetAsLastSibling();
    }

    private void ApplyGameplayUiLayout()
    {
      float fitScale = 1f;

      if (!exact320x200ComparisonMode)
      {
        RectTransform canvasRect =
            dungeonViewport != null
            && dungeonViewport.canvas != null
                ? dungeonViewport.canvas.transform as RectTransform
                : null;
        float parentWidth =
            canvasRect != null ? canvasRect.rect.width : DefaultViewWidth;
        float parentHeight =
            canvasRect != null ? canvasRect.rect.height : DefaultViewHeight;

        fitScale = Mathf.Min(
            parentWidth / DefaultViewWidth,
            parentHeight / DefaultViewHeight
        );
      }

      ApplyGameplayUiLayout(fitScale);
    }

    private void ApplyGameplayUiLayout(float fitScale)
    {
      // Re-show party / right chrome and place the cropped dungeon viewport.
      if (partyArea != null)
        partyArea.gameObject.SetActive(true);

      if (rightInterfaceArea != null)
        rightInterfaceArea.gameObject.SetActive(true);

      EnsureGameplayLayoutHierarchy();
      if (gameplayRoot == null)
        return;

      gameplayRoot.anchorMin = new Vector2(0.5f, 0.5f);
      gameplayRoot.anchorMax = new Vector2(0.5f, 0.5f);
      gameplayRoot.pivot = new Vector2(0.5f, 0.5f);
      gameplayRoot.anchoredPosition = Vector2.zero;
      gameplayRoot.sizeDelta = new Vector2(
          DefaultViewWidth,
          DefaultViewHeight
      );
      gameplayRoot.localScale = new Vector3(fitScale, fitScale, 1f);
      gameplayRoot.localRotation = Quaternion.identity;

      // Logical DM top-left (0,36) → Unity bottom-left y = 200 - 36 - 164 = 0.
      if (partyArea != null)
      {
        partyArea.anchorMin = new Vector2(0f, 1f);
        partyArea.anchorMax = new Vector2(0f, 1f);
        partyArea.pivot = new Vector2(0f, 1f);
        partyArea.anchoredPosition = Vector2.zero;
        partyArea.sizeDelta = new Vector2(
            DefaultViewWidth,
            PartyAreaHeight
        );
        partyArea.localScale = Vector3.one;
        partyArea.localRotation = Quaternion.identity;
      }

      RectTransform viewportRect = dungeonViewport.rectTransform;
      viewportRect.anchorMin = new Vector2(0f, 0f);
      viewportRect.anchorMax = new Vector2(0f, 0f);
      viewportRect.pivot = new Vector2(0f, 0f);
      viewportRect.anchoredPosition = new Vector2(
          GameplayViewportX,
          DefaultViewHeight
              - GameplayViewportYFromTop
              - DungeonViewHeight
      );
      viewportRect.sizeDelta = new Vector2(
          DungeonViewWidth,
          DungeonViewHeight
      );
      viewportRect.localScale = Vector3.one;
      viewportRect.localRotation = Quaternion.identity;

      // Authored 224×136 view only — excludes Ceiling Strip 84/85 (Y 139 / 148).
      dungeonViewport.uvRect = DungeonUvRect;

      if (rightInterfaceArea != null)
      {
        rightInterfaceArea.anchorMin = new Vector2(0f, 0f);
        rightInterfaceArea.anchorMax = new Vector2(0f, 0f);
        rightInterfaceArea.pivot = new Vector2(0f, 0f);
        rightInterfaceArea.anchoredPosition = new Vector2(
            RightInterfaceX,
            DefaultViewHeight
                - RightInterfaceYFromTop
                - RightInterfaceHeight
        );
        rightInterfaceArea.sizeDelta = new Vector2(
            RightInterfaceWidth,
            RightInterfaceHeight
        );
        rightInterfaceArea.localScale = Vector3.one;
        rightInterfaceArea.localRotation = Quaternion.identity;
      }

      if (movementArrows != null)
      {
        ViewportPiece arrowsPiece =
            FindLayoutPiece(DungeonGraphicType.MovementArrows);

        if (arrowsPiece != null)
        {
          MovementArrowsLayout.Apply(
              movementArrows,
              arrowsPiece.X,
              arrowsPiece.Y,
              arrowsPiece.Enabled
          );
        }
        else
        {
          SetMovementArrowsVisible(false);
        }
      }

      if (frameBuffer != null)
        frameBuffer.filterMode = FilterMode.Point;

      if (targetTexture != null)
        targetTexture.filterMode = FilterMode.Point;

      if (dungeonViewport.texture != null)
        dungeonViewport.texture.filterMode = FilterMode.Point;
    }

    private void ApplyViewportPresentation()
    {
      if (dungeonViewport == null)
        return;

      if (exact320x200ComparisonMode)
      {
        ApplyExact320x200ComparisonPresentation();
        previousExact320x200ComparisonMode = true;
        return;
      }

      if (previousExact320x200ComparisonMode)
      {
        RestoreCanvasScalerAfterExactComparison();
        previousExact320x200ComparisonMode = false;
      }

      if (showEntranceScreen)
      {
        // Serialized full-screen RectTransform is the source of truth.
        // Do not rewrite anchors, offsets, or size while the entrance shows —
        // that dirties the canvas and fights CanvasScaler / pixel snapping.
        dungeonViewport.uvRect = EntranceUvRect;
        return;
      }

      ApplyGameplayUiLayout();
    }

    private void ApplyExact320x200ComparisonPresentation()
    {
      ApplyConstantPixelCanvasScaler();
      EnsureGameplayLayoutHierarchy();
      if (gameplayRoot == null || dungeonViewport == null)
        return;

      // Match Edit Mode: one full 320×200 framebuffer, no dungeon UV crop.
      if (partyArea != null)
        partyArea.gameObject.SetActive(false);

      if (rightInterfaceArea != null)
        rightInterfaceArea.gameObject.SetActive(false);

      gameplayRoot.anchorMin = new Vector2(0.5f, 0.5f);
      gameplayRoot.anchorMax = new Vector2(0.5f, 0.5f);
      gameplayRoot.pivot = new Vector2(0.5f, 0.5f);
      gameplayRoot.anchoredPosition = Vector2.zero;
      gameplayRoot.sizeDelta = new Vector2(
          DefaultViewWidth,
          DefaultViewHeight
      );
      gameplayRoot.localScale = Vector3.one;
      gameplayRoot.localRotation = Quaternion.identity;

      RectTransform viewportRect = dungeonViewport.rectTransform;
      viewportRect.anchorMin = new Vector2(0.5f, 0.5f);
      viewportRect.anchorMax = new Vector2(0.5f, 0.5f);
      viewportRect.pivot = new Vector2(0.5f, 0.5f);
      viewportRect.anchoredPosition = Vector2.zero;
      viewportRect.sizeDelta = new Vector2(
          DefaultViewWidth,
          DefaultViewHeight
      );
      viewportRect.localScale = Vector3.one;
      viewportRect.localRotation = Quaternion.identity;
      dungeonViewport.uvRect = FullFrameUvRect;

      if (movementArrows != null)
      {
        ViewportPiece arrowsPiece =
            FindLayoutPiece(DungeonGraphicType.MovementArrows);

        if (arrowsPiece != null)
        {
          if (movementArrows.rectTransform.parent != gameplayRoot)
            movementArrows.rectTransform.SetParent(gameplayRoot, false);

          MovementArrowsLayout.Apply(
              movementArrows,
              arrowsPiece.X,
              arrowsPiece.Y,
              arrowsPiece.Enabled
          );
        }
        else
        {
          SetMovementArrowsVisible(false);
        }
      }

      if (frameBuffer != null)
        frameBuffer.filterMode = FilterMode.Point;

      if (targetTexture != null)
        targetTexture.filterMode = FilterMode.Point;

      if (dungeonViewport.texture != null)
        dungeonViewport.texture.filterMode = FilterMode.Point;

      SetEntranceViewportVisible(true);
    }

    private void ApplyConstantPixelCanvasScaler()
    {
      if (dungeonViewport == null || dungeonViewport.canvas == null)
        return;

      CanvasScaler scaler =
          dungeonViewport.canvas.GetComponent<CanvasScaler>();
      if (scaler == null)
        return;

      if (!canvasScalerStateSaved)
      {
        savedScalerMode = scaler.uiScaleMode;
        savedScalerScaleFactor = scaler.scaleFactor;
        savedScalerReferenceResolution = scaler.referenceResolution;
        savedScalerMatchWidthOrHeight = scaler.matchWidthOrHeight;
        canvasScalerStateSaved = true;
      }

      scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
      scaler.scaleFactor = 1f;
    }

    private void RestoreCanvasScalerAfterExactComparison()
    {
      if (partyArea != null)
        partyArea.gameObject.SetActive(true);

      if (rightInterfaceArea != null)
        rightInterfaceArea.gameObject.SetActive(true);

      if (!canvasScalerStateSaved
          || dungeonViewport == null
          || dungeonViewport.canvas == null)
      {
        return;
      }

      CanvasScaler scaler =
          dungeonViewport.canvas.GetComponent<CanvasScaler>();
      if (scaler == null)
        return;

      scaler.uiScaleMode = savedScalerMode;
      scaler.scaleFactor = savedScalerScaleFactor;
      scaler.referenceResolution = savedScalerReferenceResolution;
      scaler.matchWidthOrHeight = savedScalerMatchWidthOrHeight;
    }

    private void OnValidate()
    {
      if (!Application.isPlaying)
        return;

      if (dungeonViewport == null)
        return;

      ApplyViewportPresentation();
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

      // Play Mode: draw and present immediately so teleports / tests do not
      // depend on Camera.onPostRender ordering (same path as entrance end).
      if (!Application.isPlaying)
        return;

      if (frameBuffer == null || targetTexture == null)
        return;

      DrawDungeonFrame();
      frameDirty = false;
      Graphics.Blit(frameBuffer, targetTexture);
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

      Color32 clearColour = exact320x200ComparisonMode
          ? new Color32(255, 0, 255, 255)
          : new Color32(0, 0, 0, 255);

      Clear(clearColour);

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

      // TEMP: at (10,4) North, blit the original 224×136 reference wall
      // instead of composing tiles. Toggle showOriginalWallReferenceTest off
      // (or clear the texture) to restore normal rendering.
      if (TryDrawOriginalWallReferenceTest())
      {
        visibleWallPieces.Clear();
        visibleWallPieces.Add("OriginalWallReference");
        ApplyFrameBuffer();
        return;
      }

      System.Text.StringBuilder drawnFrontWalls =
          new System.Text.StringBuilder();
      System.Text.StringBuilder drawnSideWalls =
          new System.Text.StringBuilder();

      visibleWallPieces.Clear();

      bool f1WallGroupDrawn = false;

      foreach (ViewportPiece piece in layout.Pieces)
      {
        // F1 group: straight FrontWallF1 uses F1 wrap only (no F1L/F1R).
        // Side-only F1L/F1R still draw when the centre cell is open.
        if (IsF1WallGraphic(piece.Graphic))
        {
          if (!f1WallGroupDrawn)
          {
            DrawF1WallGroup(
                drawnFrontWalls,
                drawnSideWalls
            );
            f1WallGroupDrawn = true;
          }

          continue;
        }

        // Live UI chrome — positioned from layout, never blitted.
        if (piece.Graphic == DungeonGraphicType.MovementArrows)
          continue;

        if (!ShouldDrawPiece(piece))
          continue;

        RecordDrawnWallPiece(
            piece.Graphic,
            drawnFrontWalls,
            drawnSideWalls
        );
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

      if (!showEntranceScreen)
        TryDrawHeroPortraitOverlay();

      if (showEntranceScreen)
        DrawEntranceOverlay();

      if (!showEntranceScreen)
        DrawChampionNameTest();

      DrawComparisonModeDebugInfo();

      ApplyFrameBuffer();
    }

    private void DrawComparisonModeDebugInfo()
    {
      if (!exact320x200ComparisonMode)
        return;

      if (bitmapFont == null || framePixels == null || currentMap == null)
        return;

      bitmapFont.DrawPoseDebugText(
          framePixels,
          viewWidth,
          viewHeight,
          currentMap.PlayerX,
          currentMap.PlayerY,
          currentMap.PlayerFacing
      );
    }

    private void DrawChampionNameTest()
    {
      if (bitmapFont == null || framePixels == null)
        return;

      // First hero-name frame: 43×7 inside Champion Status Slot 1
      // (layout X=0,Y=171). Lighter band is texture top → FB Y 193..199.
      const int frameX = 0;
      const int frameY = 193;
      const int frameWidth = 43;
      const int frameHeight = 7;
      const int localX = -1;
      const int localY = 0;
      const int championNameAdvance = 6;

      Color32 halkGold = new Color32(255, 182, 0, 255);

      bitmapFont.DrawText(
          framePixels,
          320,
          200,
          "HALK",
          frameX + localX,
          frameY + localY,
          halkGold,
          frameX,
          frameY,
          frameWidth,
          frameHeight,
          championNameAdvance
      );
    }

    private void TryDrawHeroPortraitOverlay()
    {
      // TEMP: drawHeroPortraits off until F1 wall seams are fixed.
      if (!drawHeroPortraits)
        return;

      if (currentMap == null)
        return;

      if (!IsCenterFrontWallVisible(1))
        return;

      if (!TryGetHeroOnVisibleFrontWall(out HeroDefinition hero))
        return;

      Texture2D portrait = FindHeroPortraitTexture(hero.PortraitName);
      if (portrait == null)
        return;

      DrawHeroPortrait(
          portrait,
          heroPortraitX,
          heroPortraitY + dungeonDrawOffsetY
      );
    }

    // TEMP reference test — remove with the serialized fields above.
    private bool TryDrawOriginalWallReferenceTest()
    {
      if (!showOriginalWallReferenceTest)
        return false;

      Texture2D reference = ResolveOriginalWallReferenceTexture();
      if (reference == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Original wall reference test is ON but " +
            "originalWallReference_10_4_North is null."
        );
        return false;
      }

      if (currentMap == null)
        return false;

      if (currentMap.PlayerX != 10
          || currentMap.PlayerY != 4
          || currentMap.PlayerFacing != DungeonFacing.North)
      {
        return false;
      }

      Debug.Log(
          "DungeonRenderer: TEMP original wall reference blit at " +
          $"({currentMap.PlayerX},{currentMap.PlayerY}) " +
          $"facing {currentMap.PlayerFacing} " +
          $"tex={reference.width}x{reference.height}."
      );

      Blit(
          reference,
          0,
          dungeonDrawOffsetY
      );

      return true;
    }

    private Texture2D ResolveOriginalWallReferenceTexture()
    {
      if (originalWallReference_10_4_North != null)
        return originalWallReference_10_4_North;

#if UNITY_EDITOR
      originalWallReference_10_4_North =
          UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
              "Assets/Art/Reference/OriginalWall_10_4_North.png"
          );
#endif

      return originalWallReference_10_4_North;
    }

    private bool TryGetHeroOnVisibleFrontWall(out HeroDefinition hero)
    {
      hero = null;

      GetHeroPlacementLookup(
          currentMap.PlayerFacing,
          out int heroX,
          out int heroY,
          out HeroWallDirection wallDirection
      );

      hero = HeroDatabase.GetByPlacement(
          0,
          heroX,
          heroY,
          wallDirection
      );

      return hero != null;
    }

    private void GetHeroPlacementLookup(
        DungeonFacing facing,
        out int heroX,
        out int heroY,
        out HeroWallDirection wallDirection)
    {
      heroX = currentMap.PlayerX;
      heroY = currentMap.PlayerY;

      switch (facing)
      {
        case DungeonFacing.North:
          heroY += 1;
          wallDirection = HeroWallDirection.North;
          break;

        case DungeonFacing.East:
          wallDirection = HeroWallDirection.East;
          break;

        case DungeonFacing.South:
          wallDirection = HeroWallDirection.South;
          break;

        case DungeonFacing.West:
          wallDirection = HeroWallDirection.West;
          break;

        default:
          wallDirection = HeroWallDirection.North;
          break;
      }
    }

    private Texture2D FindHeroPortraitTexture(string portraitName)
    {
      if (heroPortraitTextures == null
          || string.IsNullOrEmpty(portraitName))
      {
        return null;
      }

      for (int i = 0; i < heroPortraitTextures.Length; i++)
      {
        Texture2D texture = heroPortraitTextures[i];
        if (texture == null)
          continue;

        if (string.Equals(
                texture.name,
                portraitName,
                System.StringComparison.OrdinalIgnoreCase))
        {
          return texture;
        }
      }

      return null;
    }

    private void DrawHeroPortrait(
        Texture2D portraitTexture,
        int x,
        int y)
    {
      if (portraitTexture == null)
        return;

      Blit(portraitTexture, x, y);
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

      if (currentMap == null)
      {
        if (piece.Graphic == DungeonGraphicType.None)
          return false;

        return piece.Enabled;
      }

      return ViewportPatternCatalog.ShouldDrawPiece(
          piece,
          currentMap,
          currentMap.PlayerX,
          currentMap.PlayerY,
          currentMap.PlayerFacing,
          warnUnknownKeyInEditor: false);
    }

    private static bool IsF1WallGraphic(DungeonGraphicType graphic)
    {
      return StraightF1WallLogic.IsF1WallGroupGraphic(graphic);
    }

    // Straight FrontWallF1 A/B: 224×111 composite 1:1 (skip F1L/F1R).
    // Side-only F1L/F1R when the centre cell is open.
    private void DrawF1WallGroup(
        System.Text.StringBuilder drawnFrontWalls,
        System.Text.StringBuilder drawnSideWalls)
    {
      ViewportPiece leftPiece =
          FindLayoutPiece(DungeonGraphicType.WallF1L);
      ViewportPiece rightPiece =
          FindLayoutPiece(DungeonGraphicType.WallF1R);
      ViewportPiece frontA =
          FindLayoutPiece(DungeonGraphicType.FrontWallF1_A);
      ViewportPiece frontB =
          FindLayoutPiece(DungeonGraphicType.FrontWallF1_B);
      ViewportPiece frontLegacy =
          FindLayoutPiece(DungeonGraphicType.FrontWallF1);

      ViewportPiece frontPiece = null;
      if (ShouldDrawPiece(frontA))
        frontPiece = frontA;
      else if (ShouldDrawPiece(frontB))
        frontPiece = frontB;
      else if (ShouldDrawPiece(frontLegacy))
        frontPiece = frontLegacy;

      if (frontPiece != null)
      {
        RecordDrawnWallPiece(
            frontPiece.Graphic,
            drawnFrontWalls,
            drawnSideWalls
        );
        DrawStraightF1FrontWall(frontPiece);
        return;
      }

      if (ShouldDrawPiece(leftPiece))
      {
        RecordDrawnWallPiece(
            DungeonGraphicType.WallF1L,
            drawnFrontWalls,
            drawnSideWalls
        );
        DrawPiece(leftPiece);
      }

      if (ShouldDrawPiece(rightPiece))
      {
        RecordDrawnWallPiece(
            DungeonGraphicType.WallF1R,
            drawnFrontWalls,
            drawnSideWalls
        );
        DrawPiece(rightPiece);
      }
    }

    // 224×111 composite copied 1:1. Authored MirrorHorizontally only.
    private void DrawStraightF1FrontWall(ViewportPiece frontPiece)
    {
      Texture2D texture =
          graphics.GetTexture(frontPiece.Graphic);

      if (texture == null)
      {
        Debug.LogWarning(
            "DungeonRenderer: Missing texture for " +
            frontPiece.Graphic
        );
        return;
      }

      if (texture.width < StraightF1WallLogic.CompositeWidth
          || texture.height <= 0)
      {
        Debug.LogWarning(
            "DungeonRenderer: FrontWallF1 texture size " +
            $"{texture.width}x{texture.height} is not a 224-wide composite."
        );
        return;
      }

      int destY = frontPiece.Y + dungeonDrawOffsetY;
      BlitStraightF1Composite(
          texture,
          destY,
          frontPiece.MirrorHorizontally);
    }

    private void BlitStraightF1Composite(
        Texture2D source,
        int destinationY,
        bool mirrorHorizontally)
    {
      Color32[] sourcePixels = source.GetPixels32();
      int sourceWidth = source.width;
      int sourceHeight = source.height;

      for (int row = 0; row < sourceHeight; row++)
      {
        int targetY = destinationY + row;
        if (targetY < 0 || targetY >= viewHeight)
          continue;

        int sourceRow = row * sourceWidth;
        int destRow = targetY * viewWidth;

        for (int destX = 0; destX < DungeonViewWidth; destX++)
        {
          int writeX = StraightF1WallLogic.WriteDestX(
              destX,
              mirrorHorizontally);

          Color32 colour = sourcePixels[sourceRow + destX];
          colour.a = 255;
          framePixels[destRow + writeX] = colour;
        }
      }
    }

    private ViewportPiece FindLayoutPiece(DungeonGraphicType graphic)
    {
      if (layout == null)
        return null;

      foreach (ViewportPiece piece in layout.Pieces)
      {
        if (piece != null && piece.Graphic == graphic)
          return piece;
      }

      return null;
    }

    private void RecordDrawnWallPiece(
        DungeonGraphicType graphic,
        System.Text.StringBuilder drawnFrontWalls,
        System.Text.StringBuilder drawnSideWalls)
    {
      if (TryGetCenterFrontWallDepth(graphic, out _))
      {
        if (drawnFrontWalls.Length > 0)
          drawnFrontWalls.Append(", ");

        drawnFrontWalls.Append(graphic);
        visibleWallPieces.Add(graphic.ToString());
      }
      else if (TryGetSideWallDepthAndSide(graphic, out _, out _))
      {
        if (drawnSideWalls.Length > 0)
          drawnSideWalls.Append(", ");

        drawnSideWalls.Append(graphic);
        visibleWallPieces.Add(graphic.ToString());
      }
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
        case DungeonGraphicType.FrontWallF1_A:
        case DungeonGraphicType.FrontWallF1_B:
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
        case DungeonGraphicType.FrontWallF1_A:
        case DungeonGraphicType.FrontWallF1_B:
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
      {
        if (!IsCenterFrontWallVisible(centerDepth))
          return false;

        if (!StraightF1WallLogic.IsStraightF1FrontGraphic(graphic)
            || centerDepth != 1)
        {
          return true;
        }

        // A/B parity on the depth-1 wall cell ahead of the player.
        DungeonMap.GetForwardOffset(
            currentMap.PlayerFacing,
            out int forwardX,
            out int forwardY);

        int wallX = currentMap.PlayerX + forwardX;
        int wallY = currentMap.PlayerY + forwardY;

        return StraightF1WallLogic.IsVariantVisibleForWall(
            graphic,
            currentMap.PlayerFacing,
            wallX,
            wallY,
            centerWallPresent: true
        );
      }

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
      // F0 sides stay independent of nearer front occlusion.
      if (depth > 0 && IsFrontDepthOccluded(depth))
        return false;

      // Straight FrontWallF1 A/B: centre wall draws wrap alone; suppress F1L/R.
      // F2: centre wall draws FrontWallF2 alone; suppress F2L/R.
      if (depth == 1 && IsCenterFrontWallVisible(1))
        return false;

      if (depth == 2 && IsCenterFrontWallVisible(2))
        return false;

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

      bool mirror = GetEffectiveEnvironmentMirror(piece);
      int destY = piece.Y + dungeonDrawOffsetY;

      // Floor/Ceiling: mirror across the 224px dungeon viewport only.
      if (StraightF1WallLogic.IsFloorOrCeilingGraphic(piece.Graphic))
      {
        StraightF1WallLogic.BlitViewportComponentToBuffer(
            texture,
            framePixels,
            viewWidth,
            viewHeight,
            piece.X,
            destY,
            mirror
        );
        return;
      }

      Texture2D mask =
          graphics.GetMask(piece.Graphic, out bool flipMaskX);

      // Full authored sprite at layout X/Y — no crop, shift, or stretch.
      Blit(
          texture,
          piece.X,
          destY,
          mask,
          flipMaskX,
          flipVertical: false,
          sourceXOffset: 0,
          sourceWidth: -1,
          flipSourceHorizontal: mirror
      );
    }

    /// <summary>
    /// With a live map, Floor/Ceiling share one environment phase.
    /// Without a map (layout testing), use the piece's Mirror Horizontally flag.
    /// Front Wall F1 does not use this path (authored MirrorHorizontally only).
    /// </summary>
    private bool GetEffectiveEnvironmentMirror(ViewportPiece piece)
    {
      if (piece == null)
        return false;

      if (currentMap == null
          || !StraightF1WallLogic.IsEnvironmentPhaseGraphic(piece.Graphic))
      {
        return piece.MirrorHorizontally;
      }

      return StraightF1WallLogic.IsEnvironmentPhaseB(
          currentMap.PlayerFacing,
          currentMap.PlayerX,
          currentMap.PlayerY
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
        bool flipVertical = false,
        int sourceXOffset = 0,
        int sourceWidth = -1,
        bool flipSourceHorizontal = false)
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

      if (sourceWidth < 0)
        sourceWidth = source.width;

      if (sourceXOffset < 0)
        sourceXOffset = 0;

      if (sourceXOffset + sourceWidth > source.width)
        sourceWidth = source.width - sourceXOffset;

      if (sourceWidth <= 0)
        return;

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
            int column = 0;
            column < sourceWidth;
            column++)
        {
          int sourceX = flipSourceHorizontal
              ? sourceXOffset + (sourceWidth - 1 - column)
              : sourceXOffset + column;
          int targetX = destinationX + column;

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
