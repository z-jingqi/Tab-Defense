using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TapDefense.Core;

namespace TapDefense
{
    // Created once from code: no layout groups, per-frame hierarchy rebuilds or UI instantiation.
    public sealed class GameHud : MonoBehaviour
    {
        private readonly Color ink = new Color(0.035f, 0.055f, 0.1f);
        private readonly Color panel = new Color(0.07f, 0.11f, 0.18f);
        private readonly Color mint = new Color(0.35f, 1f, 0.82f);
        private readonly Color muted = new Color(0.55f, 0.66f, 0.74f);
        private RectTransform arrowPanel;
        private Image arrowPreview;
        private Font font;
        private GameController game;
        private GameConfig config;
        private RectTransform safe;
        private Text modeLabel, timer, health, experience, footer, combo, overlayTitle, overlayBody, overlayStats, hint, bossLabel;
        private Image healthFill, xpFill, bossFill;
        private GameObject overlay, bossBar;
        private Button mainButton, pauseButton, soundButton;
        private Text mainButtonLabel, pauseLabel, soundLabel;
        private Text powerToast;
        private readonly Button[] cards = new Button[3];
        private readonly Text[] cardTitles = new Text[3];
        private readonly Text[] cardBodies = new Text[3];
        private RunState previousState = (RunState)(-1);
        private bool previousPaused;
        private int previousPending = -1;
        private float refreshAt;
        private string toast = "";
        private float toastUntil;

        public void Initialize(GameController controller, GameConfig settings)
        {
            game = controller; config = settings;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect("Safe Area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();
            if (!EventSystem.current)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
                events.sendNavigationEvents = false;
            }

            // Compact backplates keep HUD text readable over moving characters.
            Image headerBackplate = Image("Header Backplate", safe, new Color(ink.r, ink.g, ink.b, 0.32f));
            Place(headerBackplate.rectTransform, Vector2.up, Vector2.one, Vector2.zero, new Vector2(0,320), Vector2.up);
            Image footerBackplate = Image("Footer Backplate", safe, new Color(ink.r, ink.g, ink.b, 0.32f));
            Place(footerBackplate.rectTransform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0,305), Vector2.zero);

            Text brand = Label("Brand", safe, "T A P   D E F E N S E", 27, muted, TextAnchor.MiddleLeft);
            Place(brand.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(40,-48), new Vector2(740,42), new Vector2(0,0.5f));
            modeLabel = Label("Mode", safe, "", 53, Color.white, TextAnchor.MiddleLeft);
            Place(modeLabel.rectTransform, Vector2.up, Vector2.up, new Vector2(40,-112), new Vector2(650,68), new Vector2(0,0.5f));
            timer = Label("Time", safe, "", 49, mint, TextAnchor.MiddleRight);
            Place(timer.rectTransform, Vector2.one, Vector2.one, new Vector2(-40,-112), new Vector2(260,68), new Vector2(1,0.5f));
            pauseButton = Button("Pause", safe, "II", () => game.TogglePause(), out pauseLabel);
            Place(pauseButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-40,-46), new Vector2(90,58), new Vector2(1,0.5f));
            soundButton = Button("Sound", safe, "SND", () => game.ToggleSound(), out soundLabel);
            soundLabel.fontSize = 22;
            Place(soundButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-144,-46), new Vector2(110,58), new Vector2(1,0.5f));
            healthFill = Bar("Hero HP", safe, -184, new Color(0.35f,1,0.82f), out health);
            var xpTrack=Image("Experience",transform,panel);
            Place(xpTrack.rectTransform,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,12),new Vector2(.5f,0));
            xpFill=Image("Fill",xpTrack.transform,new Color(.65f,.48f,1));xpFill.sprite=config.squareSprite;
            xpFill.type=UnityEngine.UI.Image.Type.Filled;xpFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
            experience=Label("Level",safe,"",24,Color.white,TextAnchor.LowerLeft);
            Place(experience.rectTransform,Vector2.zero,Vector2.zero,new Vector2(40,28),new Vector2(650,40),Vector2.zero);
            bossFill = Bar("Boss", safe, -286, new Color(1,0.36f,0.44f), out bossLabel);
            bossBar = bossFill.transform.parent.gameObject;

            arrowPanel = Image("Arrow Preview", safe, panel).rectTransform;
            Place(arrowPanel, Vector2.right, Vector2.right, new Vector2(-28,700), new Vector2(150,250), new Vector2(1,0.5f));
            arrowPreview = Image("Current Arrow", arrowPanel, Color.white);
            arrowPreview.sprite = config.arrowSprite; arrowPreview.preserveAspect = true;
            Center(arrowPreview.rectTransform, 35, 70, 110);
            hint = Label("Arrow State",arrowPanel,"",20,Color.white,TextAnchor.MiddleCenter);
            Center(hint.rectTransform,-83,140,60);
            footer = Label("Stats", safe, "", 24, muted, TextAnchor.MiddleCenter);
            Place(footer.rectTransform, Vector2.zero, Vector2.right, new Vector2(0,220), new Vector2(-50,60), new Vector2(0.5f,0.5f));
            combo = Label("Toast", safe, "", 32, mint, TextAnchor.MiddleCenter);
            Place(combo.rectTransform, Vector2.zero, Vector2.right, new Vector2(0,270), new Vector2(-50,50), new Vector2(0.5f,0.5f));

            overlay = Image("Overlay", safe, new Color(ink.r, ink.g, ink.b, 0.96f), true).gameObject;
            Stretch(overlay.GetComponent<RectTransform>());
            overlayTitle = Label("Title", overlay.transform, "", 72, mint, TextAnchor.MiddleCenter);
            Center(overlayTitle.rectTransform, 570, 930, 110);
            overlayBody = Label("Description", overlay.transform, "", 29, muted, TextAnchor.MiddleCenter);
            Center(overlayBody.rectTransform, 449, 900, 140);
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i] = Button("Upgrade " + i, overlay.transform, "", () => game.SelectUpgrade(index), out Text unused);
                unused.gameObject.SetActive(false);
                Center(cards[i].GetComponent<RectTransform>(), 220 - i * 211, 900, 184);
                cardTitles[i] = Label("Name", cards[i].transform, "", 38, mint, TextAnchor.MiddleLeft);
                Place(cardTitles[i].rectTransform, Vector2.up, Vector2.one, new Vector2(0,-44), new Vector2(-60,60), new Vector2(0.5f,0.5f));
                cardBodies[i] = Label("Effect", cards[i].transform, "", 30, muted, TextAnchor.MiddleLeft);
                Place(cardBodies[i].rectTransform, Vector2.zero, Vector2.right, new Vector2(0,59), new Vector2(-60,82), new Vector2(0.5f,0.5f));
            }
            mainButton = Button("Continue", overlay.transform, "", () => game.PrimaryAction(), out mainButtonLabel);
            mainButton.image.color = mint; mainButtonLabel.color = ink; mainButtonLabel.fontSize = 36;
            Center(mainButton.GetComponent<RectTransform>(), -460, 850, 110);
            overlayStats = Label("Run Summary", overlay.transform, "", 25, muted, TextAnchor.MiddleCenter);
            Center(overlayStats.rectTransform, -603, 920, 150);
            powerToast = Label("Power Confirmation",overlay.transform,"",30,mint,TextAnchor.MiddleCenter);
            Center(powerToast.rectTransform,-350,920,70);
            Refresh(true);
        }
        public bool BlocksTap(Vector2 point)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(arrowPanel, point)) return true;
            return (pauseButton.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(pauseButton.GetComponent<RectTransform>(), point))
                || RectTransformUtility.RectangleContainsScreenPoint(soundButton.GetComponent<RectTransform>(), point);
        }
        public void ShowToast(string text) { toast = text; toastUntil = Time.unscaledTime + 1.6f; Refresh(true); }
        public void Refresh(bool force = false)
        {
            if (!game || game.Run == null) return;
            RunModel run = game.Run;
            if (!game.BossInformationVisible && bossBar) bossBar.SetActive(false);
            bool changed = previousState != run.State || previousPaused != game.Paused || previousPending != run.PendingUpgrades;
            if (!force && !changed && Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.08f;

            modeLabel.text = "ENDLESS SURVIVAL";
            timer.text = Mathf.FloorToInt(game.SurvivalTime).ToString() + "s";
            health.text = "HERO HP  " + Mathf.CeilToInt(run.Health) + " / " + Mathf.CeilToInt(run.MaxHealth);
            healthFill.fillAmount = run.Health / run.MaxHealth;
            xpFill.fillAmount = Mathf.Clamp01(run.ExperienceBank / (float)run.NextUpgradeCost);
            experience.text = "XP  " + run.ExperienceBank + " / " + run.NextUpgradeCost + "   |   LV " + (run.Level + 1);
            hint.text="AUTO ARROW";arrowPreview.color=Color.white;
            footer.text = "DMG " + Mathf.RoundToInt(run.Damage) + "    SIZE " + run.Radius.ToString("0.00") + "    CHAIN " + run.ChainStacks;
            combo.text = Time.unscaledTime < toastUntil ? toast : (run.State == RunState.Combat ? "DRAG HERO LEFT / RIGHT  |  AUTO FIRE" : "");
            powerToast.text = Time.unscaledTime < toastUntil ? toast : "";
            bossBar.SetActive(game.BossInformationVisible && run.State == RunState.Combat);
            bossFill.fillAmount = game.BossHealthFraction; bossLabel.text = game.BossDisplay;
            pauseButton.gameObject.SetActive(run.State == RunState.Combat);
            soundLabel.text = game.SoundEnabled ? "SND" : "MUTE";
            bool show = run.State != RunState.Combat || game.Paused;
            overlay.SetActive(show);
            if (show && (changed || force)) RenderOverlay(run);
            previousState = run.State; previousPaused = game.Paused; previousPending = run.PendingUpgrades;
        }
        private void RenderOverlay(RunModel run)
        {
            bool ready = run.State == RunState.Ready;
            Center(overlayTitle.rectTransform, ready ? 600 : 570, 930, ready ? 210 : 110);
            Center(overlayBody.rectTransform, ready ? 370 : 449, 900, 140);
            bool upgrade = run.State == RunState.Upgrade && !game.Paused;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(upgrade);
                if (upgrade) { cardTitles[i].text = "0" + (i+1) + "   " + UpgradeName(game.Choices[i]); cardBodies[i].text = UpgradeDescription(game.Choices[i], config, run); }
            }
            mainButton.gameObject.SetActive(!upgrade);
            overlayStats.text = "KILLS " + run.Kills + "    TOTAL XP " + run.TotalExperience + "    LEVEL " + (run.Level + 1)
                + "\n" + "DMG " + Mathf.RoundToInt(run.Damage) + "   SIZE " + run.Radius.ToString("0.00");
            if (game.Paused)
            { overlayTitle.text = "PAUSED"; overlayBody.text = "Combat is paused."; mainButtonLabel.text = "RESUME"; }
            else switch (run.State)
            {
                case RunState.Ready:
                    overlayTitle.text="TAP DEFENSE";
                    overlayBody.text="Drag horizontally in the lower battlefield to move your hero.\nArrows fire straight up. Align with enemies. Enemies chase your hero when they get close, then strike in melee range.";
                    mainButtonLabel.text="START SURVIVAL";
                    overlayStats.text="Kills earn XP. Choose powers as you level up.\nSurvive as long as you can."; break;
                case RunState.Upgrade:
                    overlayTitle.text="CHOOSE A POWER";
                    overlayBody.text=run.PendingUpgrades+" choice(s) remaining. Combat resumes after your picks.";break;
                case RunState.Lost:
                    overlayTitle.text="GAME OVER";
                    overlayBody.text="SURVIVED "+Mathf.FloorToInt(game.SurvivalTime)+" SECONDS\nKILLS "+run.Kills;
                    mainButtonLabel.text="TRY AGAIN";break;
            }
        }

        public static string UpgradeName(UpgradeKind kind)
        {
            switch(kind)
            {
                case UpgradeKind.MultiShot: return "SPLIT ARROWS";
                case UpgradeKind.Pierce: return "PIERCING ARROWS";
                case UpgradeKind.Burst: return "BURST FIRE";
                case UpgradeKind.CriticalChance: return "KEEN EYE";
                case UpgradeKind.CriticalDamage: return "DEADLY STRIKE";
                case UpgradeKind.Damage: return "OVERCHARGE";
                case UpgradeKind.Radius: return "WIDE IMPACT";
                case UpgradeKind.Cooldown: return "RAPID PULSE";
                case UpgradeKind.Chain: return "CHAIN REACTION";
                case UpgradeKind.MaxHealth: return "MAXIMUM HP";
                default: return "RESTORE HP";
            }
        }
        private static string UpgradeDescription(UpgradeKind kind, GameConfig config, RunModel run)
        {
            RunParameters p = config.progression;
            switch(kind)
            {
                case UpgradeKind.MultiShot: return "One extra parallel arrow per volley. Side arrows deal " + (p.sideArrowDamage*100).ToString("0") + "% damage.";
                case UpgradeKind.Burst: return "One extra volley per auto-attack.";
                case UpgradeKind.Pierce: return "Each arrow passes through one extra enemy.";
                case UpgradeKind.CriticalChance: return "Critical chance +" + (p.criticalChanceGain*100).ToString("0") + " percentage points.";
                case UpgradeKind.CriticalDamage: return "Critical damage multiplier +" + p.criticalDamageGain.ToString("0.00") + ".";
                case UpgradeKind.Damage: return "Arrow damage +" + (p.damageGain*100).ToString("0") + "%  |  " + run.Damage.ToString("0") + " > " + (run.Damage*(1+p.damageGain)).ToString("0");
                case UpgradeKind.Radius: return "Bullet size +" + p.radiusGain.ToString("0.00") + "  |  Wider bullets hit more easily.";
                case UpgradeKind.Cooldown: return "Attack interval -" + ((1-p.cooldownMultiplier)*100).ToString("0") + "%  |  " + run.Cooldown.ToString("0.00") + "s > " + Mathf.Max(p.minimumCooldown,run.Cooldown*p.cooldownMultiplier).ToString("0.00") + "s";
                case UpgradeKind.Chain: return run.ChainStacks == 0 ? "Every kill ignites a chain blast.\nChains can spread for " + p.chainMaxDepth + " generations." : "Chain damage +" + (p.chainDamageGain*100).ToString("0") + "% of blast damage.\nChain radius +" + p.chainRadiusGain.ToString("0.00") + ". Stacks with every pick.";
                case UpgradeKind.MaxHealth: return "Maximum player HP +" + p.maxHealthGain.ToString("0") + ".\nAlso restore " + p.maxHealthGain.ToString("0") + " HP immediately.";
                default: return "Restore " + (p.healFraction*100).ToString("0") + "% of maximum player HP.\nCurrent HP " + run.Health.ToString("0") + " / " + run.MaxHealth.ToString("0");
            }
        }
        private RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>(); Place(rect,min,max,pos,dimensions,new Vector2(0.5f,0.5f)); return rect;
        }
        private Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var rect = Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Text label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
            label.color = color; label.alignment = align; label.raycastTarget = false; label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0,0,0,0.8f); shadow.effectDistance = new Vector2(1,-2);
            return label;
        }
        private Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            RectTransform rect = Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image;
        }
        private Button Button(string name, Transform parent, string text, UnityAction action, out Text label)
        {
            Image image = Image(name,parent,panel,true);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(0.8f,1,0.95f); colors.pressedColor = new Color(0.45f,0.7f,0.68f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            label = Label("Label",image.transform,text,30,Color.white,TextAnchor.MiddleCenter); return button;
        }
        private Image Bar(string name, Transform parent, float y, Color color, out Text text, bool top = true)
        {
            Image background = Image(name,parent,panel);
            Vector2 left = top ? Vector2.up : Vector2.zero, right = top ? Vector2.one : Vector2.right;
            Place(background.rectTransform,left,right,new Vector2(0,y),new Vector2(-80,36),new Vector2(0.5f,0.5f));
            Image fill = Image("Fill",background.transform,color);
            fill.color = new Color(color.r,color.g,color.b,0.42f);
            fill.sprite = config.squareSprite; fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.preserveAspect = false;
            text = Label("Value",background.transform,"",24,Color.white,TextAnchor.MiddleCenter);
            return fill;
        }
        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pos, Vector2 dimensions, Vector2 pivot)
        { rect.anchorMin=min; rect.anchorMax=max; rect.pivot=pivot; rect.anchoredPosition=pos; rect.sizeDelta=dimensions; }
        private static void Center(RectTransform rect,float y,float width,float height)
        { Place(rect,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(0,y),new Vector2(width,height),new Vector2(0.5f,0.5f)); }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    }
}
