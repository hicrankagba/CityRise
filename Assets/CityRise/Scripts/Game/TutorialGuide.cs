using CityRise.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CityRise.Game
{
    /// <summary>
    /// Rehber: Muhtar Rıza oyunu adım adım anlatır. Bazı adımlar oyuncu işi yapınca kendiliğinden geçer
    /// (ilk dükkanı kurmak, para toplamak, dükkana girmek), bazıları "İleri" ile. Her an atlanabilir;
    /// ilerleme kayıtta tutulur, bitince bir daha gösterilmez.
    /// Muhtarın yüzü oyundaki gerçekçi karakterlerden biri, ayrı bir kamerayla canlı çizilir.
    /// </summary>
    public class TutorialGuide : MonoBehaviour
    {
        enum Wait { Next, OwnBusiness, Collect, ClaimMission, EnterInterior }

        static readonly (string key, Wait wait)[] Steps =
        {
            ("guide.0", Wait.Next),
            ("guide.1", Wait.Next),
            ("guide.2", Wait.OwnBusiness),
            ("guide.3", Wait.Collect),
            ("guide.4", Wait.ClaimMission),
            ("guide.5", Wait.EnterInterior),
            ("guide.6", Wait.Next),
            ("guide.7", Wait.Next),
        };

        GameManager game;
        GameHud hud;
        PlayerProfile profile;
        RectTransform panel;
        Text speech, stepLabel;
        Button nextButton;
        int collectsAtStart, claimedAtStart;
        GameObject portraitRig;
        Camera portraitCam;
        RenderTexture portraitRT;

        const int PortraitLayer = 31;

        public void Init(GameManager gm, GameHud gameHud, RectTransform uiRoot)
        {
            game = gm;
            hud = gameHud;
            profile = gm.Profile;
            if (profile.tutorialDone) { enabled = false; return; }
            Build(uiRoot);
            BuildPortrait();
            hud.GuideActive = true;
            ShowStep();
        }

        void Build(RectTransform root)
        {
            panel = UIKit.Rect("Rehber", root);
            panel.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(28, 28), new Vector2(1000, 220));

            // Portre: yuvarlak çerçeve içinde canlı görüntü.
            var frame = UIKit.Box("Cerceve", panel, UIKit.Accent, UIKit.Circle).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(210, 210));
            var mask = UIKit.Box("Maske", frame, Color.white, UIKit.Circle);
            mask.rectTransform.Stretch(8);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            portraitRT = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "MuhtarPortre" };
            var img = new GameObject("Yuz", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(mask.transform, false);
            img.rectTransform.Stretch();
            img.texture = portraitRT;
            img.raycastTarget = false;

            // Konuşma balonu.
            var bubble = UIKit.Box("Balon", panel, UIKit.PanelLight).rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(226, 0), new Vector2(770, 210));
            UIKit.Label("Ad", bubble, Loc.T("guide.name"), 26, new Color(0.55f, 0.35f, 0.1f), TextAnchor.UpperLeft, FontStyle.Bold)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -14), new Vector2(400, 34));
            stepLabel = UIKit.Label("Adim", bubble, "", 20, new Color(0.45f, 0.43f, 0.4f), TextAnchor.UpperRight);
            stepLabel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -18), new Vector2(120, 30));
            var skip = UIKit.Button("Atla", bubble, Loc.T("guide.skip"), new Color(0.7f, 0.68f, 0.64f), Finish, 20);
            ((RectTransform)skip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(124, 40));
            speech = UIKit.Label("Metin", bubble, "", 27, UIKit.TextDark, TextAnchor.UpperLeft);
            speech.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -54), new Vector2(560, 150));
            nextButton = UIKit.Button("Ileri", bubble, Loc.T("guide.next"), UIKit.Green, Advance, 28);
            ((RectTransform)nextButton.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 16), new Vector2(170, 70));
        }

        /// <summary>Muhtar: uzak bir köşede duran gerçekçi karakter ve ona bakan küçük kamera.</summary>
        void BuildPortrait()
        {
            var lib = game.interiorLibrary;
            InteriorLibraryEntryPrefab(lib, out var prefab);
            portraitRig = new GameObject("MuhtarPortresi");
            portraitRig.transform.position = new Vector3(-7000f, 0f, -7000f);
            if (prefab != null)
            {
                var body = Instantiate(prefab, portraitRig.transform);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PortraitLayer;
                var anim = body.GetComponent<Animator>();
                if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var camGo = new GameObject("PortreKamera");
            camGo.transform.SetParent(portraitRig.transform, false);
            camGo.transform.localPosition = new Vector3(0.04f, 1.56f, 0.8f);
            camGo.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.05f, 0.02f, -1f));
            portraitCam = camGo.AddComponent<Camera>();
            portraitCam.fieldOfView = 24f;
            portraitCam.nearClipPlane = 0.1f;
            portraitCam.farClipPlane = 10f;
            portraitCam.clearFlags = CameraClearFlags.SolidColor;
            portraitCam.backgroundColor = new Color(0.55f, 0.72f, 0.88f);
            portraitCam.cullingMask = 1 << PortraitLayer;
            portraitCam.targetTexture = portraitRT;
            // Yüze her saatte aynı ışık (gece de görünsün).
            var key = new GameObject("PortreIsigi").AddComponent<Light>();
            key.transform.SetParent(portraitRig.transform, false);
            key.transform.localPosition = new Vector3(0.6f, 2.1f, 1.4f);
            key.type = LightType.Point;
            key.range = 4f;
            key.intensity = 3f;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.cullingMask = 1 << PortraitLayer;
            key.shadows = LightShadows.None;
            // Ana kamera portreyi çizmesin.
            if (game.ActiveCamera != null) game.ActiveCamera.cullingMask &= ~(1 << PortraitLayer);
        }

        static void InteriorLibraryEntryPrefab(CityRise.Interior.InteriorLibrary lib, out GameObject prefab)
        {
            prefab = null;
            if (lib == null) return;
            // Takım elbiseli karakter muhtara yakışır; yoksa ilk gerçekçi karakter.
            var e = lib.Get("mx/Ch23") ?? lib.Get("mx/Ch01");
            if (e == null) foreach (var m in lib.models) if (m.key.StartsWith("mx/")) { e = m; break; }
            prefab = e?.prefab;
        }

        void ShowStep()
        {
            int i = Mathf.Clamp(profile.tutorialStep, 0, Steps.Length - 1);
            // Zaten yapılmış adımlar atlanır.
            while (i < Steps.Length && Done(Steps[i].wait, true)) i++;
            if (i >= Steps.Length) { Finish(); return; }
            profile.tutorialStep = i;
            collectsAtStart = profile.stats.collects;
            claimedAtStart = profile.missionsClaimed;
            speech.text = Loc.T(Steps[i].key);
            stepLabel.text = $"{i + 1} / {Steps.Length}";
            bool manual = Steps[i].wait == Wait.Next;
            nextButton.gameObject.SetActive(manual);
            nextButton.SetText(i == Steps.Length - 1 ? Loc.T("guide.finish") : Loc.T("guide.next"));
        }

        /// <summary>Adımın koşulu sağlandı mı? atStart: adım açılırken (yalnızca durum koşulları sayılır).</summary>
        bool Done(Wait w, bool atStart)
        {
            switch (w)
            {
                case Wait.OwnBusiness: return profile.businesses.Count > 0;
                case Wait.Collect: return !atStart && profile.stats.collects > collectsAtStart;
                case Wait.ClaimMission: return !atStart && profile.missionsClaimed > claimedAtStart;
                case Wait.EnterInterior: return !atStart && game.InInterior;
                default: return false;
            }
        }

        void Update()
        {
            if (panel == null) return;
            // Pencere açıkken balon gizlenir; dükkan içinde alttaki düğmelerin üstüne çıkar.
            panel.gameObject.SetActive(!hud.IsModalOpen);
            panel.anchoredPosition = new Vector2(28, game.InInterior ? 150 : 28);
            int i = profile.tutorialStep;
            if (i < Steps.Length && Steps[i].wait != Wait.Next && Done(Steps[i].wait, false)) Advance();
        }

        void Advance()
        {
            profile.tutorialStep++;
            if (profile.tutorialStep >= Steps.Length) Finish();
            else ShowStep();
        }

        void Finish()
        {
            profile.tutorialDone = true;
            if (hud != null) hud.GuideActive = false;
            if (panel != null) Destroy(panel.gameObject);
            if (portraitRig != null) Destroy(portraitRig);
            if (portraitRT != null) portraitRT.Release();
            enabled = false;
        }
    }
}
