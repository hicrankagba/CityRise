using System.Collections;
using System.Collections.Generic;
using CityRise.City;
using CityRise.Core;
using CityRise.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityRise.Game
{
    /// <summary>
    /// Oyun arayüzü (yatay ekran): sol üstte seviye ve XP çubuğu, sağ üstte nakit,
    /// arsa satın alma ve işletme pencereleri, seviye atlama penceresi, binaların üstünde para simgeleri.
    /// Tüm metinler Loc anahtarlarıyla gelir.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        GameManager game;
        CityEconomy eco;
        RectTransform root, safeArea, modalLayer, coinLayer, interiorBar;
        Text interiorTitle, interiorInfo;
        Button collectButton, upgradeButton, buyButton, editButton;
        RectTransform editPanel, editTabs, editGrid;
        Text editHint;
        ShopOptions.Group editGroup = ShopOptions.Group.Floor;
        string interiorId;
        int interiorLevel;
        CanvasGroup fade;
        Rect lastSafe;
        OwnedBusiness interiorBusiness;
        Plot interiorPlot;
        Text levelNumber, levelLabel, xpLabel, cashLabel, toastLabel;
        Image xpFill;
        CanvasGroup toastGroup;
        Coroutine toastRoutine;
        readonly Dictionary<int, Button> coins = new Dictionary<int, Button>();
        readonly Queue<System.Action> pendingModals = new Queue<System.Action>();
        RectTransform missionPanel, missionList;
        Text missionHeader;
        bool missionsCollapsed;
        MissionSystem missions;

        public bool IsModalOpen => modalLayer != null && modalLayer.childCount > 0;

        public void Init(GameManager gm)
        {
            game = gm;
            eco = gm.Economy;
            Build();
            Refresh();
            eco.Changed += Refresh;
            eco.Collected += (plot, amount, xp) => Toast(Loc.T("collect.done", Loc.Money(amount), xp));
            eco.BusinessBuilt += (plot, owned) => Toast(Loc.T("buy.done", Loc.T("business." + owned.businessId)));
            eco.LeveledUp += (level, unlocked) => QueueModal(() => ShowLevelUp(level, unlocked));
            eco.StageUnlocked += (stage, blocks) => QueueModal(() => ShowStage(stage));
            missions = gm.Missions;
            if (missions != null)
            {
                missions.Changed += RefreshMissions;
                eco.Changed += RefreshMissions;
                missions.Completed += d => Toast(Loc.T("mission.done", MissionTitle(d)));
                missions.Claimed += d => Toast(Loc.T("mission.claimed", Loc.Money(d.rewardCash), d.rewardXp));
                RefreshMissions();
            }
        }

        // ------------------------------------------------------------ Kurulum

        void Build()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var canvasGo = new GameObject("Arayuz", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRoot = (RectTransform)canvasGo.transform;
            // Çentikli ekranlarda arayüz güvenli alanın içinde kalır.
            safeArea = UIKit.Rect("GuvenliAlan", canvasRoot).Stretch();
            root = safeArea;

            coinLayer = UIKit.Rect("Paralar", canvasRoot).Stretch();
            coinLayer.SetAsFirstSibling();

            // Sol üst: seviye rozeti, seviye/aşama yazısı ve XP çubuğu.
            var levelPanel = UIKit.Box("SeviyePaneli", root, UIKit.Panel).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(470, 118));
            var badge = UIKit.Box("Rozet", levelPanel, UIKit.Accent, UIKit.Circle).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(92, 92));
            levelNumber = UIKit.Label("Numara", badge, "1", 46, UIKit.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            levelNumber.rectTransform.Stretch();
            levelLabel = UIKit.Label("SeviyeYazi", levelPanel, "", 30, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            levelLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(124, -14), new Vector2(330, 40));
            var bar = UIKit.Box("XPCubugu", levelPanel, new Color(1, 1, 1, 0.15f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(124, 20), new Vector2(326, 34));
            xpFill = UIKit.Box("Dolu", bar, UIKit.Green);
            xpFill.rectTransform.anchorMin = Vector2.zero;
            xpFill.rectTransform.anchorMax = new Vector2(0, 1);
            xpFill.rectTransform.pivot = new Vector2(0, 0.5f);
            xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
            xpLabel = UIKit.Label("XPYazi", bar, "", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            xpLabel.rectTransform.Stretch();

            // Sağ üst: nakit.
            var cashPanel = UIKit.Box("NakitPaneli", root, UIKit.Panel).rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(340, 88));
            var coin = UIKit.Box("Simge", cashPanel, UIKit.Accent, UIKit.Circle).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(60, 60));
            UIKit.Label("Dolar", coin, "$", 38, UIKit.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            cashLabel = UIKit.Label("Nakit", cashPanel, "", 40, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            cashLabel.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(240, 60));

            // Alt orta: kısa bildirimler.
            var toast = UIKit.Box("Bildirim", root, UIKit.Panel).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 76));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.blocksRaycasts = false;
            toastLabel = UIKit.Label("Yazi", toast, "", 30, Color.white, TextAnchor.MiddleCenter);
            toastLabel.rectTransform.Stretch(12);

            // Sol: görevler (seviye panelinin altında; başlığa dokununca katlanır).
            missionPanel = UIKit.Box("Gorevler", root, UIKit.Panel).rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -160), new Vector2(470, 60));
            var header = UIKit.Button("Baslik", missionPanel, "", new Color(0, 0, 0, 0), () => { missionsCollapsed = !missionsCollapsed; RefreshMissions(); }, 28);
            ((RectTransform)header.transform).Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(470, 56));
            missionHeader = header.GetComponentInChildren<Text>();
            missionHeader.alignment = TextAnchor.MiddleLeft;
            missionHeader.rectTransform.offsetMin = new Vector2(20, 0);
            missionList = UIKit.Rect("Liste", missionPanel);
            missionList.anchorMin = new Vector2(0, 0);
            missionList.anchorMax = new Vector2(1, 1);
            missionList.offsetMin = new Vector2(10, 10);
            missionList.offsetMax = new Vector2(-10, -56);

            BuildInteriorBar();
            modalLayer = UIKit.Rect("Pencereler", root).Stretch();

            var fadeImg = UIKit.Box("Gecis", canvasRoot, Color.black, UIKit.Rounded);
            fadeImg.rectTransform.Stretch(-40);
            fadeImg.raycastTarget = false;
            fade = fadeImg.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0;
            fade.blocksRaycasts = false;
            ApplySafeArea();
        }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == lastSafe || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafe = sa;
            safeArea.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>İç görünümde: üstte işletme adı, altta Şehre Dön / Topla / Yükselt.</summary>
        void BuildInteriorBar()
        {
            interiorBar = UIKit.Rect("IcGorunum", root).Stretch();
            var title = UIKit.Box("Baslik", interiorBar, UIKit.Panel).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(640, 118));
            interiorTitle = UIKit.Label("Ad", title, "", 40, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            interiorTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(600, 54));
            interiorInfo = UIKit.Label("Bilgi", title, "", 26, new Color(1, 1, 1, 0.8f), TextAnchor.UpperCenter);
            interiorInfo.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(600, 40));

            var back = UIKit.Button("SehreDon", interiorBar, Loc.T("interior.back"), UIKit.Panel, () => game.CloseInterior(), 32);
            ((RectTransform)back.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(320, 100));
            collectButton = UIKit.Button("Topla", interiorBar, "", UIKit.Accent, () => { if (interiorPlot != null) eco.Collect(interiorPlot.id); }, 30);
            ((RectTransform)collectButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-480, 28), new Vector2(300, 100));
            upgradeButton = UIKit.Button("Yukselt", interiorBar, "", UIKit.Green, () =>
            {
                if (interiorPlot != null && eco.Upgrade(interiorPlot.id))
                    Toast(Loc.T("biz.upgraded", Loc.T("business." + interiorBusiness.businessId), interiorBusiness.level));
            }, 28);
            ((RectTransform)upgradeButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(430, 100));
            editButton = UIKit.Button("Duzenle", interiorBar, Loc.T("edit.open"), new Color(0.45f, 0.35f, 0.75f), () => OpenEditPanel(), 30);
            ((RectTransform)editButton.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(370, 28), new Vector2(280, 100));
            BuildEditPanel();
            buyButton = UIKit.Button("SatinAl", interiorBar, "", UIKit.Green, () => { if (interiorPlot != null) eco.Acquire(interiorPlot); }, 30);
            ((RectTransform)buyButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(480, 100));
            interiorBar.gameObject.SetActive(false);
        }

        /// <summary>owned boşsa işletme komşunundur (ya da kamu binasıdır).</summary>
        public void EnterInterior(Plot plot, OwnedBusiness owned, string businessId, int level)
        {
            interiorPlot = plot;
            interiorBusiness = owned;
            interiorId = businessId;
            interiorLevel = level;
            interiorBar.gameObject.SetActive(true);
            coinLayer.gameObject.SetActive(false);
            missionPanel.gameObject.SetActive(false);
            RefreshInterior();
        }

        public void ExitInterior()
        {
            editPanel.gameObject.SetActive(false);
            interiorPlot = null;
            interiorBusiness = null;
            interiorId = null;
            interiorBar.gameObject.SetActive(false);
            coinLayer.gameObject.SetActive(true);
            missionPanel.gameObject.SetActive(true);
        }

        void RefreshInterior()
        {
            if (interiorPlot == null) return;
            bool mine = interiorBusiness != null;
            bool editing = editPanel.gameObject.activeSelf;
            if (!mine && editing) editPanel.gameObject.SetActive(false);
            collectButton.gameObject.SetActive(mine && !editing);
            upgradeButton.gameObject.SetActive(mine && !editing);
            editButton.gameObject.SetActive(mine && !editing);
            if (!mine)
            {
                var neighborDef = eco.catalog.Find(interiorId);
                interiorTitle.text = Loc.T("biz.title", Loc.T("business." + interiorId), interiorLevel);
                buyButton.gameObject.SetActive(neighborDef != null);
                if (neighborDef == null)
                {
                    interiorInfo.text = Loc.T("interior.civic");
                    return;
                }
                interiorInfo.text = Loc.T("interior.owner", Loc.Money(neighborDef.NetPerHour));
                var check = eco.CanAcquire(interiorPlot);
                long price = eco.AcquirePrice(interiorPlot);
                buyButton.SetText(check == CityEconomy.BuildCheck.LevelTooLow ? Loc.T("buy.locked", neighborDef.requiredLevel)
                    : check == CityEconomy.BuildCheck.StageLocked ? Loc.T("buy.stage", Loc.Stage(neighborDef.requiredStage))
                    : check == CityEconomy.BuildCheck.NotEnoughCash ? Loc.T("buy.nocash", Loc.Money(price))
                    : Loc.T("interior.buy", Loc.Money(price)));
                buyButton.interactable = check == CityEconomy.BuildCheck.Ok;
                return;
            }
            buyButton.gameObject.SetActive(false);
            var b = interiorBusiness;
            var def = eco.catalog.Find(b.businessId);
            interiorTitle.text = Loc.T("biz.title", Loc.T("business." + b.businessId), b.level);
            interiorInfo.text = Loc.T("biz.income", Loc.Money(eco.NetPerHour(b)));
            collectButton.SetText(Loc.T("interior.collect", Loc.Money(b.stored)));
            collectButton.interactable = b.stored >= 1;
            bool max = def == null || b.level >= def.maxLevel;
            upgradeButton.SetText(max ? Loc.T("biz.max") : Loc.T("biz.upgrade", Loc.Money(eco.UpgradeCost(b))));
            upgradeButton.interactable = !max && eco.CanUpgrade(b);
        }

        public IEnumerator Fade(float target, float duration)
        {
            float start = fade.alpha;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                fade.alpha = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            fade.alpha = target;
            fade.blocksRaycasts = target > 0.5f;
        }

        void Refresh()
        {
            var p = eco.profile;
            levelNumber.text = p.level.ToString();
            levelLabel.text = $"{Loc.T("hud.level", p.level)}  ·  {Loc.Stage(p.stage)}";
            float k = Mathf.Clamp01(p.xp / (float)p.XpToNextLevel);
            xpFill.rectTransform.anchorMax = new Vector2(k, 1);
            xpLabel.text = Loc.T("hud.xp", p.xp, p.XpToNextLevel);
            cashLabel.text = Loc.Money(p.cash).Substring(1);
        }

        void LateUpdate()
        {
            ApplySafeArea();
            if (interiorPlot != null) RefreshInterior();
            else UpdateCoins();
#if ENABLE_INPUT_SYSTEM
            // Android geri tuşu / Esc: iç görünümden ya da pencereden çık.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (IsModalOpen) CloseModal();
                else if (game.InInterior) game.CloseInterior();
            }
#endif
        }

        /// <summary>Geliri biriken işletmelerin üstünde dokunulabilir para simgesi.</summary>
        void UpdateCoins()
        {
            var cam = game.ActiveCamera;
            if (cam == null) return;
            foreach (var owned in eco.profile.businesses)
            {
                bool show = owned.stored >= Mathf.Max(1f, (float)eco.NetPerHour(owned) / 60f) && !IsModalOpen;
                coins.TryGetValue(owned.plotId, out var button);
                if (!show)
                {
                    if (button != null) button.gameObject.SetActive(false);
                    continue;
                }
                var plot = eco.layout.FindPlot(owned.plotId);
                var sp = cam.WorldToScreenPoint(game.CoinAnchor(plot));
                if (sp.z < 0)
                {
                    if (button != null) button.gameObject.SetActive(false);
                    continue;
                }
                if (button == null)
                {
                    int id = owned.plotId;
                    button = UIKit.Button("Para", coinLayer, "$", UIKit.Accent, () => eco.Collect(id), 40);
                    button.image.sprite = UIKit.Circle;
                    button.image.type = Image.Type.Simple;
                    button.GetComponentInChildren<Text>().color = UIKit.TextDark;
                    ((RectTransform)button.transform).sizeDelta = new Vector2(78, 78);
                    coins[id] = button;
                }
                button.gameObject.SetActive(true);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(coinLayer, sp, null, out var local);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float bob = Mathf.Sin(Time.time * 3f + owned.plotId) * 6f;
                rt.anchoredPosition = local + new Vector2(0, bob);
            }
        }

        // ------------------------------------------------------------ Dükkan düzenleme

        static readonly ShopOptions.Group[] EditGroups =
        {
            ShopOptions.Group.Floor, ShopOptions.Group.Wall, ShopOptions.Group.Wainscot, ShopOptions.Group.Lamp,
            ShopOptions.Group.Decor, ShopOptions.Group.Sign, ShopOptions.Group.Awning,
        };

        /// <summary>Sağda açılan düzenleme paneli: üstte sekmeler, altta seçenek kartları. Değişiklik anında görünür.</summary>
        void BuildEditPanel()
        {
            editPanel = UIKit.Box("DuzenlePaneli", interiorBar, UIKit.Panel).rectTransform;
            editPanel.anchorMin = new Vector2(1, 0);
            editPanel.anchorMax = new Vector2(1, 1);
            editPanel.pivot = new Vector2(1, 0.5f);
            editPanel.offsetMin = new Vector2(-760, 28);
            editPanel.offsetMax = new Vector2(-28, -160);
            UIKit.Label("Baslik", editPanel, Loc.T("edit.title"), 38, Color.white, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -16), new Vector2(560, 50));
            var close = UIKit.Button("Kapat", editPanel, "Tamam", UIKit.Green, () => { editPanel.gameObject.SetActive(false); game.ShiftInteriorView(false); }, 40);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(150, 64));
            editTabs = UIKit.Rect("Sekmeler", editPanel);
            editTabs.anchorMin = new Vector2(0, 1);
            editTabs.anchorMax = new Vector2(1, 1);
            editTabs.pivot = new Vector2(0.5f, 1);
            editTabs.offsetMin = new Vector2(16, -214);
            editTabs.offsetMax = new Vector2(-16, -84);
            editGrid = UIKit.Rect("Secenekler", editPanel);
            editGrid.anchorMin = Vector2.zero;
            editGrid.anchorMax = Vector2.one;
            editGrid.offsetMin = new Vector2(16, 64);
            editGrid.offsetMax = new Vector2(-16, -226);
            editHint = UIKit.Label("Ipucu", editPanel, "", 22, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            editHint.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 14), new Vector2(680, 40));
            editPanel.gameObject.SetActive(false);
        }

        void OpenEditPanel()
        {
            if (interiorBusiness == null) return;
            editPanel.gameObject.SetActive(true);
            game.ShiftInteriorView(true);
            RefreshEditPanel();
        }

        void RefreshEditPanel()
        {
            if (editPanel == null || !editPanel.gameObject.activeSelf || interiorBusiness == null) return;
            for (int i = editTabs.childCount - 1; i >= 0; i--) Destroy(editTabs.GetChild(i).gameObject);
            for (int i = editGrid.childCount - 1; i >= 0; i--) Destroy(editGrid.GetChild(i).gameObject);
            editTabs.DetachChildren();
            editGrid.DetachChildren();

            // Sekmeler: iki sıra (4 + 3).
            for (int i = 0; i < EditGroups.Length; i++)
            {
                var g = EditGroups[i];
                bool on = g == editGroup;
                var tab = UIKit.Button("Sekme", editTabs, Loc.T("edit.tab." + g), on ? UIKit.Accent : new Color(1, 1, 1, 0.12f), () => { editGroup = g; RefreshEditPanel(); }, 24);
                if (on) tab.GetComponentInChildren<Text>().color = UIKit.TextDark;
                int row = i / 4, col = i % 4;
                ((RectTransform)tab.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 178, -row * 66), new Vector2(170, 58));
            }

            var b = interiorBusiness;
            var cards = new List<(string name, Color color, Color color2, string state, bool selected, bool affordable, UnityEngine.Events.UnityAction click)>();
            if (editGroup == ShopOptions.Group.Decor)
            {
                bool food = System.Array.IndexOf(ShopOptions.FoodShops, b.businessId) >= 0;
                foreach (var d in ShopOptions.Decor)
                {
                    if (d.foodOnly && !food) continue;
                    bool has = b.look.Has(d.id);
                    long price = eco.DecorPrice(b, d.id);
                    string state = has ? Loc.T("edit.on") : price > 0 ? Loc.Money(price) : Loc.T("edit.off");
                    string id = d.id;
                    cards.Add((d.name, new Color(0.55f, 0.45f, 0.35f), Color.white, state, has, has || eco.profile.cash >= price, () => Pick(() => eco.ToggleDecor(interiorPlot.id, id))));
                }
                editHint.text = Loc.T("edit.hint.decor");
            }
            else
            {
                var group = editGroup;
                int current = b.look.Get(group);
                // İlk kart: varsayılan (tentede "yok").
                cards.Add((Loc.T(group == ShopOptions.Group.Awning ? "edit.none" : "edit.default"), new Color(0.4f, 0.4f, 0.42f), Color.white,
                    current == -1 ? Loc.T("edit.selected") : Loc.T("edit.free"), current == -1, true, () => Pick(() => eco.ChooseLook(interiorPlot.id, group, -1))));
                var opts = ShopOptions.Options(group);
                for (int i = 0; i < opts.Length; i++)
                {
                    int index = i;
                    long price = eco.LookPrice(b, group, i);
                    string state = current == i ? Loc.T("edit.selected") : price == 0 ? Loc.T("edit.bought") : Loc.Money(price);
                    cards.Add((opts[i].name, opts[i].color, opts[i].color2, state, current == i, eco.profile.cash >= price, () => Pick(() => eco.ChooseLook(interiorPlot.id, group, index))));
                }
                editHint.text = Loc.T("edit.hint");
            }

            const float cw = 164, ch = 150, gap = 10;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                int col = i % 4, row = i / 4;
                var card = UIKit.Button("Kart", editGrid, "", c.selected ? new Color(0.98f, 0.73f, 0.16f, 0.9f) : new Color(1, 1, 1, 0.1f), c.click, 20);
                ((RectTransform)card.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * (cw + gap), -row * (ch + gap)), new Vector2(cw, ch));
                var sw = UIKit.Box("Renk", card.transform, c.color).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(cw - 20, 58));
                if (editGroup == ShopOptions.Group.Sign || editGroup == ShopOptions.Group.Awning)
                    UIKit.Box("Renk2", sw, c.color2).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(editGroup == ShopOptions.Group.Sign ? 70 : 24, editGroup == ShopOptions.Group.Sign ? 14 : 58));
                var name = UIKit.Label("Ad", card.transform, c.name, 22, c.selected ? UIKit.TextDark : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(cw - 8, 32));
                var state = UIKit.Label("Durum", card.transform, c.state, 20, c.selected ? UIKit.TextDark : c.affordable ? new Color(0.6f, 0.95f, 0.6f) : new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter);
                state.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(cw - 8, 30));
            }
        }

        void Pick(System.Func<bool> action)
        {
            if (interiorPlot == null) return;
            if (!action()) Toast(Loc.T("edit.nocash"));
            RefreshEditPanel();
        }

        // ------------------------------------------------------------ Görevler

        public static string MissionTitle(MissionDef d)
        {
            string special = "mission.id." + d.id;
            if (Loc.T(special) != special) return Loc.T(special);
            string amount = d.kind == MissionKind.CollectCash ? Loc.Money(d.target) : d.target.ToString();
            string business = string.IsNullOrEmpty(d.param) ? "" : Loc.T("business." + d.param);
            return Loc.T("mission." + d.kind, amount, business);
        }

        /// <summary>Görev listesini yeniden çizer (ilerleme ya da görev değişince).</summary>
        void RefreshMissions()
        {
            if (missions == null || missionPanel == null) return;
            for (int i = missionList.childCount - 1; i >= 0; i--) Destroy(missionList.GetChild(i).gameObject);
            missionList.DetachChildren();
            int ready = 0;
            foreach (var p in missions.Active) if (missions.IsDone(p)) ready++;
            missionHeader.text = (missionsCollapsed ? "▶  " : "▼  ") + Loc.T("mission.header") + (ready > 0 ? $"  ({ready}!)" : "");
            if (missionsCollapsed || missions.Active.Count == 0)
            {
                missionPanel.sizeDelta = new Vector2(470, 56);
                return;
            }
            const float rowH = 104, gap = 8;
            float y = 0;
            foreach (var p in missions.Active)
            {
                var d = missions.Def(p.id);
                if (d == null) continue;
                bool done = missions.IsDone(p);
                var row = UIKit.Box("Gorev", missionList, done ? new Color(0.3f, 0.72f, 0.36f, 0.25f) : new Color(1, 1, 1, 0.08f)).rectTransform;
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.anchoredPosition = new Vector2(0, -y);
                row.sizeDelta = new Vector2(0, rowH);
                UIKit.Label("Ad", row, MissionTitle(d), 26, Color.white, TextAnchor.UpperLeft, FontStyle.Bold)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(420, 34));
                string reward = d.rewardXp > 0 ? Loc.T("mission.rewardXp", Loc.Money(d.rewardCash), d.rewardXp) : Loc.T("mission.reward", Loc.Money(d.rewardCash));
                if (done)
                {
                    UIKit.Label("Odul", row, reward, 22, new Color(1, 1, 1, 0.85f), TextAnchor.LowerLeft)
                        .rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 12), new Vector2(240, 34));
                    string id = p.id;
                    var claim = UIKit.Button("OdulAl", row, Loc.T("mission.claim"), UIKit.Green, () => missions.Claim(id), 24);
                    ((RectTransform)claim.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(170, 52));
                }
                else
                {
                    long v = missions.Progress(p);
                    var bar = UIKit.Box("Cubuk", row, new Color(1, 1, 1, 0.15f)).rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 14), new Vector2(250, 26));
                    var fill = UIKit.Box("Dolu", bar, UIKit.Accent).rectTransform;
                    fill.anchorMin = Vector2.zero;
                    fill.anchorMax = new Vector2(Mathf.Clamp01(v / (float)Mathf.Max(1, d.target)), 1);
                    fill.offsetMin = fill.offsetMax = Vector2.zero;
                    string progress = d.kind == MissionKind.CollectCash ? $"{Loc.Money(v)} / {Loc.Money(d.target)}" : $"{v} / {d.target}";
                    UIKit.Label("Sayi", bar, progress, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
                    UIKit.Label("Odul", row, reward, 20, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleRight)
                        .rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 12), new Vector2(170, 30));
                }
                y += rowH + gap;
            }
            missionPanel.sizeDelta = new Vector2(470, 56 + y + 10 - gap);
        }

        // ------------------------------------------------------------ Bildirim

        public void Toast(string message)
        {
            // İç görünümde alttaki düğmelerin üstünde dursun.
            ((RectTransform)toastGroup.transform).anchoredPosition = new Vector2(0, interiorPlot != null ? 150 : 40);
            toastLabel.text = message;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime) { toastGroup.alpha = t / 0.15f; yield return null; }
            toastGroup.alpha = 1;
            yield return new WaitForSecondsRealtime(2.2f);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { toastGroup.alpha = 1 - t / 0.4f; yield return null; }
            toastGroup.alpha = 0;
        }

        // ------------------------------------------------------------ Pencereler

        RectTransform OpenModal(Vector2 size, out RectTransform content)
        {
            ClearModal();
            var dim = UIKit.Box("Karartma", modalLayer, new Color(0, 0, 0, 0.45f), UIKit.Rounded).rectTransform.Stretch(-20);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseModal);
            var panel = UIKit.Box("Pencere", modalLayer, UIKit.PanelLight).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var close = UIKit.Button("Kapat", panel, "×", new Color(0.8f, 0.3f, 0.25f), CloseModal, 44);
            ((RectTransform)close.transform).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(68, 68));
            content = UIKit.Rect("Icerik", panel).Stretch(28);
            return panel;
        }

        public void CloseModal()
        {
            ClearModal();
            if (pendingModals.Count > 0) pendingModals.Dequeue()();
        }

        void ClearModal()
        {
            for (int i = modalLayer.childCount - 1; i >= 0; i--) Destroy(modalLayer.GetChild(i).gameObject);
            modalLayer.DetachChildren();
        }

        void QueueModal(System.Action show)
        {
            if (IsModalOpen) pendingModals.Enqueue(show);
            else show();
        }

        public void ShowBuyPanel(Plot plot)
        {
            OpenModal(new Vector2(1100, 840), out var content);
            var title = UIKit.Label("Baslik", content, Loc.T("buy.title"), 44, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(900, 60));
            var land = UIKit.Label("Arsa", content, Loc.T("buy.land", Loc.Money(eco.LandPrice(plot))), 26, new Color(0.35f, 0.33f, 0.3f), TextAnchor.UpperLeft);
            land.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(900, 40));

            // Kaydırılabilir liste.
            var scroll = UIKit.Rect("Liste", content);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = Vector2.zero;
            scroll.offsetMax = new Vector2(0, -110);
            var viewport = UIKit.Rect("Gorunum", scroll).Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var listContent = UIKit.Rect("Satirlar", viewport);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.offsetMin = listContent.offsetMax = Vector2.zero;
            var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport;
            sr.content = listContent;
            sr.horizontal = false;
            sr.scrollSensitivity = 30;

            foreach (var def in eco.catalog.Sorted())
            {
                var check = eco.CanBuild(def, plot);
                bool locked = check == CityEconomy.BuildCheck.LevelTooLow || check == CityEconomy.BuildCheck.StageLocked;
                var row = UIKit.Box("Satir", listContent, locked ? new Color(0.88f, 0.86f, 0.82f) : Color.white);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
                var name = UIKit.Label("Ad", row.transform, Loc.T("business." + def.id), 36, locked ? UIKit.Grey : UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold);
                name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(560, 46));
                var info = UIKit.Label("Bilgi", row.transform, Loc.T("buy.row", Loc.Money(eco.TotalPrice(def, plot)), Loc.Money(def.NetPerHour)), 26, UIKit.Grey, TextAnchor.UpperLeft);
                info.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -62), new Vector2(560, 40));

                string text;
                Color color;
                switch (check)
                {
                    case CityEconomy.BuildCheck.Ok: text = Loc.T("buy.button", Loc.Money(eco.TotalPrice(def, plot))); color = UIKit.Green; break;
                    case CityEconomy.BuildCheck.NotEnoughCash: text = Loc.T("buy.nocash", Loc.Money(eco.TotalPrice(def, plot))); color = UIKit.Grey; break;
                    case CityEconomy.BuildCheck.StageLocked: text = Loc.T("buy.stage", Loc.Stage(def.requiredStage)); color = UIKit.Grey; break;
                    case CityEconomy.BuildCheck.PlotTooSmall: text = Loc.T("buy.small"); color = UIKit.Grey; break;
                    default: text = Loc.T("buy.locked", def.requiredLevel); color = UIKit.Grey; break;
                }
                var d = def;
                var btn = UIKit.Button("Kur", row.transform, text, color, () =>
                {
                    if (eco.Build(d, plot)) CloseModal();
                }, 26);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(380, 80));
                btn.interactable = check == CityEconomy.BuildCheck.Ok;
            }
        }

        public void ShowBusinessPanel(Plot plot, OwnedBusiness owned)
        {
            var def = eco.catalog.Find(owned.businessId);
            if (def == null) return;
            OpenModal(new Vector2(860, 520), out var content);
            UIKit.Label("Baslik", content, Loc.T("biz.title", Loc.T("business." + def.id), owned.level), 44, UIKit.TextDark, TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(720, 60));
            UIKit.Label("Gelir", content, Loc.T("biz.income", Loc.Money(eco.NetPerHour(owned))), 30, UIKit.TextDark, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -90), new Vector2(780, 44));
            UIKit.Label("Kasa", content, Loc.T("biz.stored", Loc.Money(owned.stored), Loc.Money(eco.Capacity(owned))), 30, UIKit.TextDark, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -140), new Vector2(780, 44));

            var collect = UIKit.Button("Topla", content, Loc.T("biz.collect"), UIKit.Accent, () => { eco.Collect(plot.id); CloseModal(); }, 32);
            ((RectTransform)collect.transform).Place(new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(320, 96));
            collect.interactable = owned.stored >= 1;

            bool max = owned.level >= def.maxLevel;
            var upgrade = UIKit.Button("Yukselt", content, max ? Loc.T("biz.max") : Loc.T("biz.upgrade", Loc.Money(eco.UpgradeCost(owned))), UIKit.Green, () =>
            {
                if (eco.Upgrade(plot.id))
                {
                    Toast(Loc.T("biz.upgraded", Loc.T("business." + def.id), owned.level));
                    CloseModal();
                }
            }, 30);
            ((RectTransform)upgrade.transform).Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(420, 96));
            upgrade.interactable = !max && eco.CanUpgrade(owned);
        }

        void ShowLevelUp(int level, List<BusinessDefinition> unlocked)
        {
            OpenModal(new Vector2(900, 560), out var content);
            UIKit.Label("Baslik", content, Loc.T("level.title", level), 58, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
            UIKit.Label("Odul", content, Loc.T("level.reward", Loc.Money(level * eco.config.levelUpCashPerLevel)), 34, UIKit.Green, TextAnchor.MiddleCenter, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(800, 50));
            if (unlocked.Count > 0)
            {
                var names = new List<string>();
                foreach (var d in unlocked) names.Add(Loc.T("business." + d.id));
                UIKit.Label("Yeni", content, Loc.T("level.unlocked", string.Join(", ", names)), 32, UIKit.TextDark, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(800, 100));
            }
            var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
        }

        void ShowStage(CityStage stage)
        {
            OpenModal(new Vector2(900, 460), out var content);
            UIKit.Label("Baslik", content, Loc.T("level.stage", Loc.Stage(stage)), 40, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
            var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
        }

        public void ShowWelcome(double earned)
        {
            QueueModal(() =>
            {
                OpenModal(new Vector2(900, 440), out var content);
                UIKit.Label("Baslik", content, Loc.T("offline.title"), 52, UIKit.TextDark, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Stretch();
                UIKit.Label("Metin", content, Loc.T("offline.body", Loc.Money(earned)), 32, UIKit.TextDark, TextAnchor.MiddleCenter)
                    .rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(800, 140));
                var ok = UIKit.Button("Tamam", content, Loc.T("level.ok"), UIKit.Accent, CloseModal, 36);
                ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(360, 96));
            });
        }
    }
}
