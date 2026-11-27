using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BlockDodge
{
    public sealed class PrototypeGame : MonoBehaviour
    {
        private enum State { Map, Ready, Playing, Paused, GameOver, Complete }

        [System.Serializable]
        private sealed class LevelDefinition
        {
            public int target;
            public float spawnInterval;
            public float blockSpeed;
            public float spawnRange;
            public LevelDefinition(int target, float spawnInterval, float blockSpeed, float spawnRange)
            {
                this.target = target;
                this.spawnInterval = spawnInterval;
                this.blockSpeed = blockSpeed;
                this.spawnRange = spawnRange;
            }
        }

        [SerializeField] private LevelDefinition[] levels =
        {
            new LevelDefinition(20, 1.50f, 3.0f, 1.5f),
            new LevelDefinition(25, 1.35f, 3.2f, 2.1f),
            new LevelDefinition(30, 1.25f, 3.4f, 2.1f),
            new LevelDefinition(30, 1.10f, 3.8f, 2.1f),
            new LevelDefinition(35, 1.00f, 4.0f, 2.1f)
        };
        private int levelIndex;
        private bool reachedEdge;
        private int closeDodges;
        private int waveNumber;
        private RectTransform safeArea;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        public const float PlayerY = -3.6f;
        private const float PlayerHalfWidth = 0.38f;
        private const float ArenaHalfWidth = 2.6f;
        private readonly List<FallingBlock> blocks = new List<FallingBlock>();
        private State state;
        private Rigidbody2D playerBody;
        private Sprite square;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI bonusText;
        private float bonusMessageTime;
        private GameObject overlay;
        private GameObject pauseButton;
        private float horizontalVelocity;
        private float spawnTimer;
        private int score;
        private int controllingTouch = -1;
        private bool mouseControls;
        private int moveDirection;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            SetupWorld();
            SetupUI();
            ShowMap();
        }

        private void SetupWorld()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(6f, 3f / camera.aspect);
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.73f, 0.89f, 0.83f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            MakeSprite("Ground", new Vector2(0, -4.2f), new Vector2(6, 0.38f), new Color(0.22f, 0.48f, 0.31f), 0);
            var player = MakeSprite("Player", new Vector2(0, PlayerY), new Vector2(0.76f, 0.76f), new Color(1f, 0.62f, 0.30f), 2);
            playerBody = player.AddComponent<Rigidbody2D>();
            playerBody.bodyType = RigidbodyType2D.Dynamic;
            playerBody.gravityScale = 0;
            playerBody.freezeRotation = true;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            player.AddComponent<BoxCollider2D>();
            player.AddComponent<PlayerHit>().Game = this;
        }

        private GameObject MakeSprite(string name, Vector2 position, Vector2 size, Color color, int order)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return obj;
        }

        private void SetupUI()
        {
            var canvasObject = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasRect, false);
            safeArea = safeAreaObject.GetComponent<RectTransform>();
            UpdateSafeArea();
            RectTransform root = safeArea;
            scoreText = MakeText(root, "Score", "DODGED  0", 52, new Vector2(0.06f, 0.88f), new Vector2(0.74f, 0.97f));
            scoreText.alignment = TextAlignmentOptions.MidlineLeft;
            scoreText.color = new Color(0.06f, 0.24f, 0.16f);
            bonusText = MakeText(root, "Bonus", "", 34, new Vector2(0.06f, 0.81f), new Vector2(0.74f, 0.88f));
            bonusText.alignment = TextAlignmentOptions.MidlineLeft;
            bonusText.color = new Color(0.06f, 0.24f, 0.16f);
            pauseButton = MakeButton(root, "Pause", "II", new Vector2(0.77f, 0.89f), new Vector2(0.94f, 0.97f), Pause);
            overlay = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            overlay.transform.SetParent(root, false);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.12f, 0.10f, 0.88f);
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string name, string value, int fontSize, Vector2 min, Vector2 max)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white; text.raycastTarget = false; text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * 0.65f; text.fontSizeMax = fontSize;
            return text;
        }

        private GameObject MakeButton(RectTransform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            obj.GetComponent<UnityEngine.UI.Image>().color = new Color(0.18f, 0.55f, 0.39f);
            obj.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(action);
            MakeText(rect, "Label", label, 44, Vector2.zero, Vector2.one);
            return obj;
        }

        private void ShowOverlay(string title, string detail, string primary, UnityEngine.Events.UnityAction primaryAction, string secondary = null, UnityEngine.Events.UnityAction secondaryAction = null, string third = null, UnityEngine.Events.UnityAction thirdAction = null)
        {
            ClearOverlay();
            overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.12f, 0.10f, 0.88f);
            overlay.SetActive(true);
            var rect = overlay.GetComponent<RectTransform>();
            MakeText(rect, "Title", title, 68, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.70f));
            MakeText(rect, "Detail", detail, 45, new Vector2(0.06f, 0.45f), new Vector2(0.94f, 0.59f));
            MakeButton(rect, "Primary", primary, new Vector2(0.20f, 0.34f), new Vector2(0.80f, 0.42f), primaryAction);
            if (secondary != null) MakeButton(rect, "Secondary", secondary, new Vector2(0.20f, 0.23f), new Vector2(0.80f, 0.31f), secondaryAction);
            if (third != null) MakeButton(rect, "Third", third, new Vector2(0.20f, 0.12f), new Vector2(0.80f, 0.20f), thirdAction);
        }

        private void ClearOverlay()
        {
            foreach (Transform child in overlay.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private int BestStars(int index)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt("BlockDodge.Level." + (index + 1) + ".Stars", 0), 0, 3);
        }

        private bool IsUnlocked(int index)
        {
            return index == 0 || (index < levels.Length && BestStars(index - 1) > 0);
        }

        private void ShowMap()
        {
            Time.timeScale = 1f;
            state = State.Map;
            ClearInput();
            foreach (var block in blocks) if (block != null) Destroy(block.gameObject);
            blocks.Clear();
            pauseButton.SetActive(false);
            scoreText.gameObject.SetActive(false);
            bonusText.gameObject.SetActive(false);
            ClearOverlay();
            overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.12f, 0.10f, 1f);
            overlay.SetActive(true);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            MakeText(rect, "MapTitle", "JUNGLE MAP", 68, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.95f));
            MakeText(rect, "MapHint", "Choose an unlocked level", 34, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.85f));
            for (int index = 0; index < 10; index++)
            {
                int selectedLevel = index;
                int row = index / 2;
                int column = index % 2;
                float left = column == 0 ? 0.09f : 0.53f;
                float top = 0.76f - row * 0.12f;
                bool available = index < levels.Length;
                bool unlocked = available && IsUnlocked(index);
                string label = available
                    ? "LEVEL " + (index + 1) + "\n" + (unlocked ? BestStars(index) + " / 3 stars" : "LOCKED")
                    : "LEVEL " + (index + 1) + "\nSOON";
                GameObject button = MakeButton(rect, "Level " + (index + 1), label,
                    new Vector2(left, top - 0.10f), new Vector2(left + 0.38f, top),
                    () => SelectLevel(selectedLevel));
                button.GetComponent<UnityEngine.UI.Button>().interactable = unlocked;
            }
        }

        private void SelectLevel(int index)
        {
            if (index < 0 || index >= levels.Length || !IsUnlocked(index)) return;
            levelIndex = index;
            ShowReady();
        }

        private void UpdateSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            lastSafeArea = area;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        private void UpdateScore()
        {
            scoreText.text = "DODGED  " + score + " / " + levels[levelIndex].target;
        }

        private void ShowReady()
        {
            state = State.Ready;
            pauseButton.SetActive(false);
            scoreText.gameObject.SetActive(true);
            bonusText.gameObject.SetActive(true);
            bonusText.text = "";
            ShowOverlay("JUNGLE  " + (levelIndex + 1), "Dodge " + levels[levelIndex].target + " blocks.\nBonus stars: stay off edges\nand make a close dodge.", "PLAY", StartRun, "MAP", ShowMap);
            UpdateScore();
        }

        private void StartRun()
        {
            Time.timeScale = 1f;
            foreach (var block in blocks) if (block != null) Destroy(block.gameObject);
            blocks.Clear();
            score = 0; reachedEdge = false; closeDodges = 0; waveNumber = 0; UpdateScore();
            bonusText.text = ""; bonusMessageTime = 0;
            playerBody.position = new Vector2(0, PlayerY);
            horizontalVelocity = 0; spawnTimer = 1f;
            ClearInput();
            state = State.Playing;
            overlay.SetActive(false); pauseButton.SetActive(true);
        }

        private void Update()
        {
            if (safeArea != null && (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)) UpdateSafeArea();
            if (state != State.Playing) return;
            ReadInput();
            if (bonusMessageTime > 0)
            {
                bonusMessageTime -= Time.deltaTime;
                if (bonusMessageTime <= 0) bonusText.text = "";
            }
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnBlock();
                spawnTimer = levels[levelIndex].spawnInterval;
            }
        }

        private void FixedUpdate()
        {
            if (state != State.Playing) return;
            horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, moveDirection * 5f, 35f * Time.fixedDeltaTime);
            float x = Mathf.Clamp(playerBody.position.x + horizontalVelocity * Time.fixedDeltaTime, -ArenaHalfWidth + PlayerHalfWidth, ArenaHalfWidth - PlayerHalfWidth);
            if (Mathf.Abs(x) >= ArenaHalfWidth - PlayerHalfWidth - 0.01f) reachedEdge = true;
            playerBody.MovePosition(new Vector2(x, PlayerY));
        }

        private void ReadInput()
        {
            moveDirection = 0;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    bool down = touch.press.isPressed;
                    if (touch.press.wasPressedThisFrame && controllingTouch < 0 && touch.position.ReadValue().y < Screen.height * 0.75f)
                        controllingTouch = id;
                    if (id == controllingTouch && down)
                    {
                        moveDirection = touch.position.ReadValue().x < Screen.width * 0.5f ? -1 : 1;
                        return;
                    }
                }
                controllingTouch = -1;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame && mouse.position.ReadValue().y < Screen.height * 0.75f) mouseControls = true;
                if (!mouse.leftButton.isPressed) mouseControls = false;
                if (mouseControls) moveDirection = mouse.position.ReadValue().x < Screen.width * 0.5f ? -1 : 1;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveDirection = -1;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveDirection = 1;
                if (keyboard.escapeKey.wasPressedThisFrame) Pause();
            }
        }

        private void SpawnBlock()
        {
            waveNumber++;
            if (levelIndex >= 2 && waveNumber % 4 == 0)
            {
                CreateBlock(-1.5f);
                CreateBlock(1.5f);
            }
            else
            {
                CreateBlock(Random.Range(-levels[levelIndex].spawnRange, levels[levelIndex].spawnRange));
            }
        }

        private void CreateBlock(float x)
        {
            var obj = MakeSprite("Block", new Vector2(x, 6.6f), new Vector2(0.85f, 0.85f), new Color(0.54f, 0.30f, 0.16f), 1);
            var body = obj.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            obj.AddComponent<BoxCollider2D>().isTrigger = true;
            var block = obj.AddComponent<FallingBlock>();
            block.Initialize(this, body, levels[levelIndex].blockSpeed);
            blocks.Add(block);
        }

        public void BlockPassed(FallingBlock block)
        {
            if (state != State.Playing) return;
            if (block.NearMiss)
            {
                closeDodges++;
                bonusText.text = "CLOSE DODGE!";
                bonusMessageTime = 1.5f;
            }
            score++;
            UpdateScore();
            if (score >= levels[levelIndex].target) CompleteLevel();
        }

        private void CompleteLevel()
        {
            state = State.Complete;
            ClearInput();
            pauseButton.SetActive(false);
            int stars = 1 + (reachedEdge ? 0 : 1) + (closeDodges > 0 ? 1 : 0);
            if (stars > BestStars(levelIndex))
            {
                PlayerPrefs.SetInt("BlockDodge.Level." + (levelIndex + 1) + ".Stars", stars);
                PlayerPrefs.Save();
            }
            string detail = "Stars: " + stars + " / 3\n" +
                (reachedEdge ? "-" : "+") + " Avoid the edges\n" +
                (closeDodges > 0 ? "+" : "-") + " Make a close dodge";
            if (levelIndex + 1 < levels.Length)
                ShowOverlay("LEVEL COMPLETE", detail, "NEXT LEVEL", NextLevel, "REPLAY", StartRun, "MAP", ShowMap);
            else
                ShowOverlay("LEVEL 5 COMPLETE", detail + "\nMore levels coming soon!", "MAP", ShowMap, "REPLAY", StartRun);
        }

        private void NextLevel()
        {
            levelIndex++;
            ShowReady();
        }
        public void RemoveBlock(FallingBlock block) { blocks.Remove(block); Destroy(block.gameObject); }
        public float PlayerX => playerBody.position.x;
        public bool IsPlaying => state == State.Playing;
        public void Hit()
        {
            if (state != State.Playing) return;
            state = State.GameOver; ClearInput(); pauseButton.SetActive(false);
            ShowOverlay("GAME OVER", "Blocks dodged: " + score, "PLAY AGAIN", StartRun, "MAP", ShowMap);
        }
        private void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused; ClearInput(); Time.timeScale = 0f; pauseButton.SetActive(false);
            ShowOverlay("PAUSED", "Take your time.", "RESUME", Resume, "RESTART", StartRun, "MAP", ShowMap);
        }
        private void Resume() { Time.timeScale = 1f; ClearInput(); state = State.Playing; overlay.SetActive(false); pauseButton.SetActive(true); }
        private void ClearInput() { controllingTouch = -1; mouseControls = false; moveDirection = 0; horizontalVelocity = 0; }
        private void OnApplicationPause(bool paused) { if (paused) Pause(); }
        private void OnApplicationFocus(bool focused) { if (!focused && Application.isPlaying) Pause(); }
        private void OnDestroy() { Time.timeScale = 1f; if (square != null) Destroy(square); }
    }

    public sealed class PlayerHit : MonoBehaviour
    {
        public PrototypeGame Game { get; set; }
        private void OnTriggerEnter2D(Collider2D other) { if (other.GetComponent<FallingBlock>() != null) Game.Hit(); }
    }

    public sealed class FallingBlock : MonoBehaviour
    {
        private PrototypeGame game;
        private Rigidbody2D body;
        private bool scored;
        private float speed;
        public bool NearMiss { get; private set; }
        public void Initialize(PrototypeGame owner, Rigidbody2D rigidbody, float fallSpeed) { game = owner; body = rigidbody; speed = fallSpeed; }
        private void FixedUpdate()
        {
            if (!game.IsPlaying) return;
            float nextY = body.position.y - speed * Time.fixedDeltaTime;
            body.MovePosition(new Vector2(body.position.x, nextY));
            float horizontalDistance = Mathf.Abs(body.position.x - game.PlayerX);
            if (Mathf.Abs(nextY - PrototypeGame.PlayerY) <= 0.805f && horizontalDistance >= 0.805f && horizontalDistance <= 1.4f)
                NearMiss = true;
            if (!scored && nextY < -4.45f) { scored = true; game.BlockPassed(this); }
            if (nextY < -6.5f) game.RemoveBlock(this);
        }
    }
}
