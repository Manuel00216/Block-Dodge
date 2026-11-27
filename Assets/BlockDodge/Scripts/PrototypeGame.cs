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
        private enum State { Ready, Playing, Paused, GameOver }
        private const float PlayerY = -3.6f;
        private const float PlayerHalfWidth = 0.38f;
        private const float ArenaHalfWidth = 2.6f;
        private readonly List<FallingBlock> blocks = new List<FallingBlock>();
        private State state;
        private Rigidbody2D playerBody;
        private Sprite square;
        private TextMeshProUGUI scoreText;
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
            ShowReady();
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
            camera.orthographicSize = 6f;
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
            RectTransform root = canvasObject.GetComponent<RectTransform>();
            scoreText = MakeText(root, "Score", "DODGED  0", 42, new Vector2(0.08f, 0.89f), new Vector2(0.68f, 0.97f));
            scoreText.alignment = TextAlignmentOptions.MidlineLeft;
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

        private void ShowOverlay(string title, string detail, string primary, UnityEngine.Events.UnityAction primaryAction, string secondary = null, UnityEngine.Events.UnityAction secondaryAction = null)
        {
            foreach (Transform child in overlay.transform) Destroy(child.gameObject);
            overlay.SetActive(true);
            var rect = overlay.GetComponent<RectTransform>();
            MakeText(rect, "Title", title, 68, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.70f));
            MakeText(rect, "Detail", detail, 32, new Vector2(0.10f, 0.47f), new Vector2(0.90f, 0.58f));
            MakeButton(rect, "Primary", primary, new Vector2(0.20f, 0.34f), new Vector2(0.80f, 0.42f), primaryAction);
            if (secondary != null) MakeButton(rect, "Secondary", secondary, new Vector2(0.20f, 0.23f), new Vector2(0.80f, 0.31f), secondaryAction);
        }

        private void ShowReady()
        {
            state = State.Ready;
            pauseButton.SetActive(false);
            ShowOverlay("BLOCK DODGE", "Hold left or right to move.\nAvoid the falling blocks.", "PLAY", StartRun);
        }

        private void StartRun()
        {
            Time.timeScale = 1f;
            foreach (var block in blocks) if (block != null) Destroy(block.gameObject);
            blocks.Clear();
            score = 0; scoreText.text = "DODGED  0";
            playerBody.position = new Vector2(0, PlayerY);
            horizontalVelocity = 0; spawnTimer = 1f;
            ClearInput();
            state = State.Playing;
            overlay.SetActive(false); pauseButton.SetActive(true);
        }

        private void Update()
        {
            if (state != State.Playing) return;
            ReadInput();
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnBlock();
                spawnTimer = 1.25f;
            }
        }

        private void FixedUpdate()
        {
            if (state != State.Playing) return;
            horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, moveDirection * 5f, 35f * Time.fixedDeltaTime);
            float x = Mathf.Clamp(playerBody.position.x + horizontalVelocity * Time.fixedDeltaTime, -ArenaHalfWidth + PlayerHalfWidth, ArenaHalfWidth - PlayerHalfWidth);
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
            var obj = MakeSprite("Block", new Vector2(Random.Range(-2.1f, 2.1f), 6.6f), new Vector2(0.85f, 0.85f), new Color(0.54f, 0.30f, 0.16f), 1);
            var body = obj.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            obj.AddComponent<BoxCollider2D>().isTrigger = true;
            var block = obj.AddComponent<FallingBlock>();
            block.Initialize(this, body);
            blocks.Add(block);
        }

        public void BlockPassed(FallingBlock block) { if (state == State.Playing) { score++; scoreText.text = "DODGED  " + score; } }
        public void RemoveBlock(FallingBlock block) { blocks.Remove(block); Destroy(block.gameObject); }
        public bool IsPlaying => state == State.Playing;
        public void Hit()
        {
            if (state != State.Playing) return;
            state = State.GameOver; ClearInput(); pauseButton.SetActive(false);
            ShowOverlay("GAME OVER", "Blocks dodged: " + score, "PLAY AGAIN", StartRun);
        }
        private void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused; ClearInput(); Time.timeScale = 0f; pauseButton.SetActive(false);
            ShowOverlay("PAUSED", "Take your time.", "RESUME", Resume, "RESTART", StartRun);
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
        public void Initialize(PrototypeGame owner, Rigidbody2D rigidbody) { game = owner; body = rigidbody; }
        private void FixedUpdate()
        {
            if (!game.IsPlaying) return;
            float nextY = body.position.y - 3.3f * Time.fixedDeltaTime;
            body.MovePosition(new Vector2(body.position.x, nextY));
            if (!scored && nextY < -4.45f) { scored = true; game.BlockPassed(this); }
            if (nextY < -6.5f) game.RemoveBlock(this);
        }
    }
}
