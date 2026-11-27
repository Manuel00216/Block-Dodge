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
        private const int GemGoal = 5;
        private const int CheckpointGoal = 3;
        private static readonly float[] CheckpointPositions = { 1.8f, -1.4f, 1.4f };
        private int gemsCollected;
        private int checkpointsCollected;
        private int waveNumber;
        private RectTransform safeArea;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        public const float PlayerY = -3.6f;
        private const float PlayerHalfWidth = 0.38f;
        private const float ArenaHalfWidth = 2.6f;
        private const float GemBlockSeparation = 1.3f;
        private const float GemFallSpeed = 4f;
        private const int SafetyCells = 33;
        private const float SafetyStep = 0.1f;
        private const int SafetySteps = 45;
        private readonly List<FallingBlock> blocks = new List<FallingBlock>();
        private State state;
        private Rigidbody2D playerBody;
        private Sprite square;
        private Sprite gemSprite;
        private Sprite checkpointSprite;
        private GameObject gem;
        private GameObject checkpoint;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI bonusText;
        private GameObject overlay;
        private GameObject pauseButton;
        private float horizontalVelocity;
        private float spawnTimer;
        private float gemSpawnTimer;
        private float checkpointSpawnTimer;
        private float collectibleSpawnCooldown;
        private int score;
        private int controllingTouch = -1;
        private bool mouseControls;
        private int moveDirection;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Time.timeScale = 1f;
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            gemSprite = CreateTokenSprite(true);
            checkpointSprite = CreateTokenSprite(false);
            SetupWorld();
            SetupUI();
            ShowMap();
        }

        private Sprite CreateTokenSprite(bool diamond)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size / 2f) / (size / 2f);
                    float dy = (y + 0.5f - size / 2f) / (size / 2f);
                    float distance = diamond ? Mathf.Abs(dx) + Mathf.Abs(dy) : Mathf.Sqrt(dx * dx + dy * dy);
                    bool visible = diamond ? distance <= 0.90f : distance <= 0.95f && distance >= 0.60f;
                    texture.SetPixel(x, y, visible ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
            bonusText = MakeText(root, "Bonus", "", 38, new Vector2(0.06f, 0.76f), new Vector2(0.76f, 0.88f));
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
            ClearCollectibles();
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
            bonusText.text = "GEMS " + gemsCollected + "/" + GemGoal + "\nCHECKPOINTS " + checkpointsCollected + "/" + CheckpointGoal;
        }

        private void ShowReady()
        {
            state = State.Ready;
            pauseButton.SetActive(false);
            scoreText.gameObject.SetActive(true);
            bonusText.gameObject.SetActive(true);
            ShowOverlay("JUNGLE  " + (levelIndex + 1), "Dodge " + levels[levelIndex].target + " blocks.\nCatch 5 falling gems\nand reach 3 checkpoints.", "PLAY", StartRun, "MAP", ShowMap);
            UpdateScore();
        }

        private void StartRun()
        {
            Time.timeScale = 1f;
            foreach (var block in blocks) if (block != null) Destroy(block.gameObject);
            blocks.Clear();
            score = 0; gemsCollected = 0; checkpointsCollected = 0; waveNumber = 0;
            ClearCollectibles();
            gemSpawnTimer = 1.5f;
            checkpointSpawnTimer = 4f;
            collectibleSpawnCooldown = 0f;
            UpdateScore();
            playerBody.position = new Vector2(0, PlayerY);
            horizontalVelocity = 0; spawnTimer = 1f;
            ClearInput();
            state = State.Playing;
            bonusText.gameObject.SetActive(false);
            overlay.SetActive(false); pauseButton.SetActive(true);
        }

        private void Update()
        {
            if (safeArea != null && (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)) UpdateSafeArea();
            if (state != State.Playing) return;
            ReadInput();
            if (checkpoint != null)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f);
                checkpoint.transform.localScale = new Vector3(0.85f * pulse, 0.45f * pulse, 1f);
            }
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnBlock();
                spawnTimer = levels[levelIndex].spawnInterval;
            }
            collectibleSpawnCooldown = Mathf.Max(0f, collectibleSpawnCooldown - Time.deltaTime);
            if (gem == null && gemsCollected < GemGoal)
            {
                gemSpawnTimer -= Time.deltaTime;
                if (gemSpawnTimer <= 0f && collectibleSpawnCooldown <= 0f)
                {
                    TrySpawnGem();
                    gemSpawnTimer = 0.3f;
                    if (gem != null) collectibleSpawnCooldown = 1f;
                }
            }
            if (checkpoint == null && checkpointsCollected < CheckpointGoal)
            {
                checkpointSpawnTimer -= Time.deltaTime;
                if (checkpointSpawnTimer <= 0f && collectibleSpawnCooldown <= 0f)
                {
                    TrySpawnCheckpoint();
                    checkpointSpawnTimer = 0.4f;
                    if (checkpoint != null) collectibleSpawnCooldown = 1f;
                }
            }
        }

        private void FixedUpdate()
        {
            if (state != State.Playing) return;
            horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, moveDirection * 5f, 35f * Time.fixedDeltaTime);
            float x = Mathf.Clamp(playerBody.position.x + horizontalVelocity * Time.fixedDeltaTime, -ArenaHalfWidth + PlayerHalfWidth, ArenaHalfWidth - PlayerHalfWidth);
            playerBody.MovePosition(new Vector2(x, PlayerY));
            MoveGem(x);
            CollectCheckpoint(x);
        }

        private void TrySpawnGem()
        {
            int start = Random.Range(0, 22);
            for (int i = 0; i < 22; i++)
            {
                float x = -2.1f + ((start + i) % 22) * 0.2f;
                bool clear = true;
                foreach (var block in blocks)
                {
                    if (block != null && Mathf.Abs(block.transform.position.x - x) < GemBlockSeparation)
                    {
                        clear = false;
                        break;
                    }
                }
                if (!clear || !HasSafeRoute(float.NaN, float.NaN, x, 2.4f, 2.7f, 0.5f)) continue;
                gem = MakeSprite("Gem", new Vector2(x, 6.6f), new Vector2(0.60f, 0.60f), new Color(1f, 0.80f, 0.08f), 3);
                gem.GetComponent<SpriteRenderer>().sprite = gemSprite;
                return;
            }
        }

        private void MoveGem(float playerX)
        {
            if (gem == null) return;
            Vector3 position = gem.transform.position;
            float nextY = position.y - GemFallSpeed * Time.fixedDeltaTime;
            gem.transform.position = new Vector3(position.x, nextY, position.z);
            bool atPlayerHeight = nextY <= PlayerY + 0.68f && position.y >= PlayerY - 0.68f;
            if (atPlayerHeight && Mathf.Abs(playerX - position.x) <= 0.68f)
            {
                Destroy(gem);
                gem = null;
                gemsCollected++;
                gemSpawnTimer = 2.5f;
                UpdateScore();
            }
            else if (nextY < PlayerY - 0.68f)
            {
                Destroy(gem);
                gem = null;
                gemSpawnTimer = 1.5f;
            }
        }

        private void TrySpawnCheckpoint()
        {
            for (int i = 0; i < CheckpointPositions.Length; i++)
            {
                float x = CheckpointPositions[(checkpointsCollected + i) % CheckpointPositions.Length];
                if (Mathf.Abs(x - playerBody.position.x) < 0.65f) continue;
                if (!HasSafeRoute(float.NaN, float.NaN, x, 0.4f, 3.8f, 0.42f)) continue;
                checkpoint = MakeSprite("Checkpoint", new Vector2(x, -3.95f), new Vector2(0.85f, 0.45f), new Color(0.12f, 0.62f, 1f), 1);
                checkpoint.GetComponent<SpriteRenderer>().sprite = checkpointSprite;
                return;
            }
        }

        private void ClearCollectibles()
        {
            if (gem != null) Destroy(gem);
            if (checkpoint != null) Destroy(checkpoint);
            gem = null;
            checkpoint = null;
        }

        private void CollectCheckpoint(float playerX)
        {
            if (checkpoint == null || Mathf.Abs(playerX - checkpoint.transform.position.x) > 0.52f) return;
            Destroy(checkpoint);
            checkpoint = null;
            checkpointsCollected++;
            checkpointSpawnTimer = 3f;
            UpdateScore();
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
            if (levelIndex >= 2 && waveNumber % 4 == 0 && CanSpawnPattern(-1.5f, 1.5f))
            {
                CreateBlock(-1.5f);
                CreateBlock(1.5f);
                return;
            }

            float range = levels[levelIndex].spawnRange;
            float start = Random.Range(0f, 1f);
            for (int i = 0; i < 16; i++)
            {
                float fraction = Mathf.Repeat(start + i / 16f, 1f);
                float x = Mathf.Lerp(-range, range, fraction);
                if (!CanSpawnPattern(x, float.NaN)) continue;
                CreateBlock(x);
                return;
            }
        }

        private bool CanSpawnPattern(float first, float second)
        {
            if (gem != null && (Mathf.Abs(gem.transform.position.x - first) < GemBlockSeparation ||
                (!float.IsNaN(second) && Mathf.Abs(gem.transform.position.x - second) < GemBlockSeparation))) return false;
            if (!HasSafeRoute(first, second, float.NaN, 0f, 0f, 0f)) return false;

            if (gem != null)
            {
                float time = (gem.transform.position.y - PlayerY) / GemFallSpeed;
                if (time > 0.2f && !HasSafeRoute(first, second, gem.transform.position.x,
                    Mathf.Max(0.2f, time - 0.12f), time + 0.12f, 0.5f)) return false;
            }
            if (checkpoint != null && !HasSafeRoute(first, second, checkpoint.transform.position.x,
                0.4f, 3.8f, 0.42f)) return false;
            return true;
        }

        // Search conservative positions through time, including obstacles already in flight.
        private bool HasSafeRoute(float first, float second, float goal, float goalStart, float goalEnd, float goalRadius)
        {
            bool hasGoal = !float.IsNaN(goal);
            float limit = ArenaHalfWidth - PlayerHalfWidth;
            float spacing = 2f * limit / (SafetyCells - 1);
            var reachable = new bool[SafetyCells, 2];
            var next = new bool[SafetyCells, 2];
            int initial = Mathf.Clamp(Mathf.RoundToInt((playerBody.position.x + limit) / spacing), 0, SafetyCells - 1);
            reachable[initial, hasGoal ? 0 : 1] = true;

            for (int step = 1; step <= SafetySteps; step++)
            {
                System.Array.Clear(next, 0, next.Length);
                float time = step * SafetyStep;
                for (int cell = 0; cell < SafetyCells; cell++)
                {
                    float x = -limit + cell * spacing;
                    if (IsHazardAt(x, time, first, second)) continue;
                    bool touchesGoal = hasGoal && time >= goalStart && time <= goalEnd && Mathf.Abs(x - goal) <= goalRadius;
                    int reach = step <= 2 ? 0 : 2;
                    for (int previous = Mathf.Max(0, cell - reach); previous <= Mathf.Min(SafetyCells - 1, cell + reach); previous++)
                    {
                        if (reachable[previous, 1]) next[cell, 1] = true;
                        if (reachable[previous, 0]) next[cell, touchesGoal ? 1 : 0] = true;
                    }
                }
                var swap = reachable;
                reachable = next;
                next = swap;
            }

            for (int cell = 0; cell < SafetyCells; cell++)
                if (reachable[cell, 1]) return true;
            return false;
        }

        private bool IsHazardAt(float playerX, float time, float first, float second)
        {
            foreach (var block in blocks)
            {
                if (block != null && HazardIntersects(playerX, time, block.transform.position.x,
                    block.transform.position.y, block.Speed)) return true;
            }
            float speed = levels[levelIndex].blockSpeed;
            return (!float.IsNaN(first) && HazardIntersects(playerX, time, first, 6.6f, speed)) ||
                (!float.IsNaN(second) && HazardIntersects(playerX, time, second, 6.6f, speed));
        }

        private static bool HazardIntersects(float playerX, float time, float blockX, float blockY, float speed)
        {
            float predictedY = blockY - speed * time;
            return Mathf.Abs(predictedY - PlayerY) < 0.805f + speed * SafetyStep &&
                Mathf.Abs(blockX - playerX) < 0.96f;
        }

        private void CreateBlock(float x)
        {
            if (gem != null && Mathf.Abs(gem.transform.position.x - x) < GemBlockSeparation) return;
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
            score++;
            UpdateScore();
            if (score >= levels[levelIndex].target) CompleteLevel();
        }

        private void CompleteLevel()
        {
            state = State.Complete;
            ClearInput();
            pauseButton.SetActive(false);
            int stars = 1 + (gemsCollected >= GemGoal ? 1 : 0) + (checkpointsCollected >= CheckpointGoal ? 1 : 0);
            if (stars > BestStars(levelIndex))
            {
                PlayerPrefs.SetInt("BlockDodge.Level." + (levelIndex + 1) + ".Stars", stars);
                PlayerPrefs.Save();
            }
            string detail = "Stars: " + stars + " / 3\n" +
                (gemsCollected >= GemGoal ? "+" : "-") + " Gems: " + gemsCollected + "/" + GemGoal + "\n" +
                (checkpointsCollected >= CheckpointGoal ? "+" : "-") + " Checkpoints: " + checkpointsCollected + "/" + CheckpointGoal;
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
        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (square != null) Destroy(square);
            if (gemSprite != null) { Destroy(gemSprite.texture); Destroy(gemSprite); }
            if (checkpointSprite != null) { Destroy(checkpointSprite.texture); Destroy(checkpointSprite); }
        }
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
        public float Speed => speed;
        public void Initialize(PrototypeGame owner, Rigidbody2D rigidbody, float fallSpeed) { game = owner; body = rigidbody; speed = fallSpeed; }
        private void FixedUpdate()
        {
            if (!game.IsPlaying) return;
            float nextY = body.position.y - speed * Time.fixedDeltaTime;
            body.MovePosition(new Vector2(body.position.x, nextY));
            if (!scored && nextY < -4.45f) { scored = true; game.BlockPassed(this); }
            if (nextY < -6.5f) game.RemoveBlock(this);
        }
    }
}
