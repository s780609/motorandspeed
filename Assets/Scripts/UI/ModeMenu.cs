using System.Collections.Generic;
using MotorAndSpeed.Audio;
using MotorAndSpeed.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MotorAndSpeed.UI
{
    /// <summary>
    /// 右上角的模式下拉選單：點選後立刻開始該模式，需要時先載入五股世界。
    /// 由 GameShell 建立，切換場景也不會消失。
    /// </summary>
    public class ModeMenu : MonoBehaviour
    {
        private const string WORLD_SCENE = "WuguWorld";
        private const string TEST_SCENE = "Prototype";
        private const float WIDTH = 260f;
        private const float ROW_HEIGHT = 34f;
        private const float MARGIN = 16f;

        private static readonly Color HeaderColor = new Color(0.08f, 0.09f, 0.1f, 0.9f);
        private static readonly Color ItemColor = new Color(0.14f, 0.16f, 0.18f, 0.95f);
        private static readonly Color ItemHighlight = new Color(0.25f, 0.45f, 0.7f, 1f);

        private readonly List<Button> _items = new List<Button>();
        private Text _header;
        private RectTransform _list;
        private GameMode _current = GameMode.FreeRide;
        private GameMode? _pending;

        /// <summary>目前的選單（遊戲啟動時由 GameShell 建立）。</summary>
        public static ModeMenu Instance { get; private set; }

        /// <summary>選單上的選項數量。</summary>
        public int OptionCount => _items.Count;

        /// <summary>目前選擇的模式。</summary>
        public GameMode Current => _current;

        /// <summary>
        /// 只更新選單上顯示的模式（模式從其他地方切換時呼叫，例如結算面板的「再玩一次」）。
        /// </summary>
        public void ShowMode(GameMode mode)
        {
            _current = mode;
            _header.text = $"模式：{GameModes.DisplayName(mode)}　▼";
        }

        /// <summary>
        /// 選擇模式並開始。
        /// </summary>
        public void Select(GameMode mode)
        {
            ShowMode(mode);
            _list.gameObject.SetActive(false);

            if (!GameModes.IsWorldMode(mode))
            {
                SceneManager.LoadScene(TEST_SCENE);

                return;
            }

            GameModeController controller = FindFirstObjectByType<GameModeController>();
            if (controller != null)
            {
                controller.StartMode(mode);

                return;
            }

            // 目前不在五股世界：先載入，載入完成後再開始
            _pending = mode;
            SceneManager.LoadScene(WORLD_SCENE);
        }

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
                Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name == TEST_SCENE)
                ShowMode(GameMode.TestTrack);

            if (_pending.HasValue && scene.name == WORLD_SCENE)
            {
                GameModeController controller = FindFirstObjectByType<GameModeController>();
                GameMode mode = _pending.Value;
                _pending = null;
                if (controller != null)
                    controller.StartMode(mode);
            }
        }

        private void Build()
        {
            // 下拉選單需要事件系統（滑鼠點擊）
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            GameObject canvasObject = new GameObject("ModeMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            Button header = CreateButton(canvasObject.transform, HeaderColor, out _header);
            RectTransform headerRect = (RectTransform)header.transform;
            headerRect.anchorMin = headerRect.anchorMax = headerRect.pivot = new Vector2(1f, 1f);
            headerRect.anchoredPosition = new Vector2(-MARGIN, -MARGIN);
            headerRect.sizeDelta = new Vector2(WIDTH, ROW_HEIGHT + 4f);
            _header.text = $"模式：{GameModes.DisplayName(_current)}　▼";
            header.onClick.AddListener(() =>
            {
                GameSounds.Play(GameSound.Click);
                _list.gameObject.SetActive(!_list.gameObject.activeSelf);
            });

            _list = new GameObject("Options", typeof(RectTransform)).GetComponent<RectTransform>();
            _list.SetParent(canvasObject.transform, false);
            _list.anchorMin = _list.anchorMax = _list.pivot = new Vector2(1f, 1f);
            _list.anchoredPosition = new Vector2(-MARGIN, -MARGIN - ROW_HEIGHT - 8f);
            _list.sizeDelta = new Vector2(WIDTH, ROW_HEIGHT * GameModes.All.Length);

            for (int i = 0; i < GameModes.All.Length; i++)
            {
                GameMode mode = GameModes.All[i];
                Button item = CreateButton(_list, ItemColor, out Text label);
                RectTransform rect = (RectTransform)item.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * ROW_HEIGHT);
                rect.sizeDelta = new Vector2(WIDTH, ROW_HEIGHT - 2f);
                label.text = GameModes.DisplayName(mode);
                item.onClick.AddListener(() =>
                {
                    GameSounds.Play(GameSound.Click);
                    Select(mode);
                });
                _items.Add(item);
            }

            _list.gameObject.SetActive(false);
        }

        private static Button CreateButton(Transform parent, Color color, out Text label)
        {
            GameObject root = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = color;
            Button button = root.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = ItemHighlight;
            colors.pressedColor = ItemHighlight;
            colors.selectedColor = color;
            button.colors = colors;

            label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(root.transform, false);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            return button;
        }
    }
}
