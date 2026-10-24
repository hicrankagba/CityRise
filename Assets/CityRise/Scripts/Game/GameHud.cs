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
        Button collectButton, upgradeButton, buyButton;
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
            RefreshInterior();
        }

        public void ExitInterior()
        {
            interiorPlot = null;
            interiorBusiness = null;
            interiorId = null;
            interiorBar.gameObject.SetActive(false);
            coinLayer.gameObject.SetActive(true);
        }

        void RefreshInterior()
        {
            if (interiorPlot == null) return;
            bool mine = interiorBusiness != null;
            collectButton.gameObject.SetActive(mine);
            upgradeButton.gameObject.SetActive(mine);
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
