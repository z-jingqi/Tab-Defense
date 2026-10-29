using System;
using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CrystalView crystal;
        [SerializeField] private EnemyView enemyPrefab;
        [SerializeField] private FeedbackSystem feedback;
        [SerializeField] private GameHud hud;
        public RunModel Run { get; private set; }
        public GameConfig Config => config;
        public UpgradeKind[] Choices { get; private set; } = Array.Empty<UpgradeKind>();
        public float SurvivalTime => elapsed;
        public int ActiveBullets => bullets == null ? 0 : bullets.Count;
        public float CooldownLeft { get; private set; }
        public bool Paused { get; private set; }
        public bool SoundEnabled => feedback && feedback.SoundEnabled;
        public int AliveEnemies => enemies == null ? 0 : enemies.AliveCount;
        public int AliveBosses => enemies == null ? 0 : enemies.AliveBosses;
        public float BossHealthFraction => enemies == null ? 0 : enemies.BossHealthFraction;
        private SpawnSettings spawnSettings = new SpawnSettings();
        private BulletSystem bullets;
        private BossDirector bossDirector;
        public FogOfWar Fog { get; private set; }
        public BossDirector Bosses => bossDirector;
        public bool BossInformationVisible => bossDirector != null && bossDirector.ActiveBoss
            && !Fog.IsHidden(bossDirector.ActiveBoss.Position,bossDirector.ActiveBoss.Definition.radius);
        public string BossDisplay
        {
            get
            {
                if (!BossInformationVisible) return "";
                var boss = bossDirector.ActiveBoss; var v = boss.Vitals;
                string text = bossDirector.Title;
                if (v.Shield > 0) text += "   SHIELD " + Mathf.CeilToInt(v.Shield);
                if (v.Armor > 0) text += "   ARMOR " + v.Armor;
                if (v.StaggerLeft > 0) text += "   STAGGER";
                else if (v.Healing) text += "   HEALING";
                else if (!string.IsNullOrEmpty(boss.BossAction)) text += "   " + boss.BossAction;
                return text;
            }
        }
        private EnemySystem enemies;
        private Transform hero;
        private bool dragging;
        private int dragFinger = -1;
        private float dragStartX, heroStartX;
        public Vector2 HeroPosition => hero ? (Vector2)hero.position : Vector2.zero;
        public float HeroLimit => Mathf.Max(0, config.arenaHalfWidth - 0.5f);
        public void MoveHero(float x)
        { if (hero) hero.position = new Vector3(Mathf.Clamp(x, -HeroLimit, HeroLimit), hero.position.y, 0); }
        private void CancelDrag() { dragging = false; dragFinger = -1; }
        private void StepHeroDrag()
        {
            Vector2 screen = Input.mousePosition; bool began = false, held = false;
            if (Input.touchCount > 0)
            {
                if (!dragging)
                {
                    Touch first = Input.GetTouch(0);
                    if (first.phase != TouchPhase.Began) return;
                    dragFinger = first.fingerId;
                }
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i); if (touch.fingerId != dragFinger) continue;
                    found = true; screen = touch.position;
                    began = touch.phase == TouchPhase.Began;
                    held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled; break;
                }
                if (!found) { CancelDrag(); return; }
            }
            else
            {
                if (dragFinger >= 0) { CancelDrag(); return; }
                began = Input.GetMouseButtonDown(0); held = Input.GetMouseButton(0);
            }
            HandleHeroPointer(screen, began, held);
        }
        private void HandleHeroPointer(Vector2 screen, bool began, bool held)
        {
            if (!held)
            {
                if (dragging) MoveHero(heroStartX + (screen.x - dragStartX) / Screen.width * worldCamera.orthographicSize * 2 * worldCamera.aspect);
                CancelDrag(); return;
            }
            if (began)
            {
                // Capture only a gesture starting in the lower battlefield, never on HUD controls.
                if (!Screen.safeArea.Contains(screen) || screen.y > Screen.height * 0.45f || hud.BlocksTap(screen)) return;
                dragging = true; dragStartX = screen.x; heroStartX = hero.position.x;
            }
            if (dragging)
                MoveHero(heroStartX + (screen.x - dragStartX) / Screen.width * worldCamera.orthographicSize * 2 * worldCamera.aspect);
        }
        // Editor mouse events also preserve short drag gestures completed between two Update frames.
        private void OnGUI()
        {
            if (Run == null || Run.State != RunState.Combat || Paused || Input.touchCount > 0) return;
            Event e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag && e.type != EventType.MouseUp) return;
            if (e.button != 0) return;
            HandleHeroPointer(new Vector2(e.mousePosition.x, Screen.height-e.mousePosition.y),
                e.type == EventType.MouseDown, e.type != EventType.MouseUp);
        }
        private void CreateHero()
        {
            hero = new GameObject("Prototype Hero (temporary art)").transform; hero.SetParent(transform, false);
            HeroPart("Ground Shadow", new Vector2(0,-0.49f), new Vector2(0.95f,0.18f), new Color(0.03f,0.07f,0.04f,0.25f), config.circleSprite);
            HeroPart("Cloak", new Vector2(0,-0.1f), new Vector2(0.65f,0.75f), new Color(0.13f,0.34f,0.7f));
            HeroPart("Head", new Vector2(0,0.42f), new Vector2(0.32f,0.32f), new Color(1f,0.8f,0.57f));
            HeroPart("Bow", new Vector2(0.42f,0.1f), new Vector2(0.12f,0.8f), new Color(1f,0.8f,0.25f));
            HeroPart("Hero Health Background",new Vector2(0,0.73f),new Vector2(.95f,.11f),new Color(.04f,.06f,.08f));
            HeroPart("Hero Health Fill",new Vector2(0,0.73f),new Vector2(.91f,.075f),new Color(.3f,1f,.5f));
            heroHealthBack=hero.Find("Hero Health Background").GetComponent<SpriteRenderer>();
            heroHealthFill=hero.Find("Hero Health Fill").GetComponent<SpriteRenderer>();
            heroHealthBack.sortingOrder=190;heroHealthFill.sortingOrder=191;
            heroRenderers=hero.GetComponentsInChildren<SpriteRenderer>();heroColors=new Color[heroRenderers.Length];
            for(int i=0;i<heroColors.Length;i++)heroColors[i]=heroRenderers[i].color;
        }
        private void HeroPart(string name, Vector2 position, Vector2 size, Color color, Sprite artwork = null)
        {
            var part = new GameObject(name); part.transform.SetParent(hero, false); part.transform.localPosition = position;
            var sprite = part.AddComponent<SpriteRenderer>(); sprite.sprite = artwork ? artwork : config.squareSprite; sprite.color = color; sprite.sortingOrder = name == "Ground Shadow" ? -20 : 146;
            Vector2 bounds = sprite.sprite.bounds.size; part.transform.localScale = new Vector3(size.x / bounds.x,size.y / bounds.y,1);
        }
        private readonly System.Random random = new System.Random();
        private float elapsed, spawnIn, shake;
        private Vector2 lastScreen;
        private float heroFlash;
        private SpriteRenderer heroHealthBack,heroHealthFill;
        private SpriteRenderer[] heroRenderers;
        private Color[] heroColors;
        private PresentationBudget presentation;

        private void Start()
        {
            if (!config || !worldCamera || !crystal || !enemyPrefab || !feedback || !hud)
            { Debug.LogError($"Main scene references are incomplete: config={!!config}, camera={!!worldCamera}, crystal={!!crystal}, enemyPrefab={!!enemyPrefab}, feedback={!!feedback}, hud={!!hud}.", this); enabled = false; return; }
            string error = config.ValidateData();
            if (error != null) { Debug.LogError(error, config); enabled = false; return; }
            presentation = config.presentation.ForCurrentPlayer();
            Application.targetFrameRate = presentation.targetFrameRate; QualitySettings.vSyncCount = 0;
            Input.simulateMouseWithTouches = false;
            Screen.orientation = ScreenOrientation.Portrait;
            feedback.Initialize(config, presentation);
            Fog = gameObject.AddComponent<FogOfWar>(); Fog.Initialize(worldCamera,config.squareSprite); feedback.Fog = Fog;
            CreateHero(); bullets = new BulletSystem(config,transform); crystal.gameObject.SetActive(false); ResetRun(); hud.Initialize(this, config); FitCamera();
        }
        private void ResetRun()
        {
            if (enemies != null) enemies.Clear();
            Run = new RunModel(config.progression);
            // Reuse the original pool on restart by rebinding the model, avoiding a new set of objects.
            if (enemies == null) enemies = new EnemySystem(config, enemyPrefab, transform, Run, feedback, OnCrystalHit);
            else enemies.BindRun(Run);
            if (bossDirector == null) bossDirector = new BossDirector(config,enemies,Fog,feedback);
            bossDirector.Reset();
            heroFlash=0;RefreshHeroFlash();
            CancelDrag(); if (hero) MoveHero(0);
            Choices = Array.Empty<UpgradeKind>();
            bullets.Clear(); CooldownLeft = 0; elapsed = 0; shake = 0; Paused = false;
            feedback.Clear(); crystal.ResetVisual(); worldCamera.transform.position = new Vector3(0,0,-10);
        }
        public void RestartRun() { ResetRun(); hud.Refresh(true); }
        public void StartRun()
        {
            if(Paused || !Run.StartRun())return;
            spawnIn=0.2f; hud.Refresh(true);
        }
        public void PrimaryAction()
        {
            if (Paused) { SetPaused(false); return; }
            if (Run.State == RunState.Lost) RestartRun();
            else StartRun();
        }
        public void SelectUpgrade(int index)
        {
            if (Paused || Run.State != RunState.Upgrade || index < 0 || index >= Choices.Length) return;
            UpgradeKind selected = Choices[index];
            if (!Run.ApplyUpgrade(selected)) return;
            Choices = Run.State == RunState.Upgrade ? Run.GetChoices(random) : Array.Empty<UpgradeKind>();
            feedback.Upgrade(); hud.ShowToast("POWER + " + GameHud.UpgradeName(selected)); hud.Refresh(true);
        }

        public void ToggleSound()
        { feedback.SoundEnabled = !feedback.SoundEnabled; if (!feedback.SoundEnabled) feedback.PauseAudio(); hud.Refresh(true); }
        public void TogglePause() { if (Run.State == RunState.Combat) SetPaused(!Paused); }
        private void SetPaused(bool paused)
        { CancelDrag(); Paused = paused; if (paused) feedback.PauseAudio(); hud.Refresh(true); }
        private void OnApplicationPause(bool paused)
        { if (paused && Run != null && Run.State == RunState.Combat) SetPaused(true); }
        private void OnApplicationFocus(bool focused)
        { if (!focused && Run != null && Run.State == RunState.Combat) SetPaused(true); }
        private void Update()
        {
            if (Run == null) return;
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height) FitCamera();
            if (!Paused)
            {
                // Clamp stalls: foreground recovery must never deliver a burst of deferred hits/spawns.
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                heroFlash=Mathf.Max(0,heroFlash-dt);RefreshHeroFlash();
                feedback.Step(dt);
                shake = Mathf.MoveTowards(shake, 0, dt * 0.35f);
                worldCamera.transform.position = new Vector3(UnityEngine.Random.Range(-shake,shake),UnityEngine.Random.Range(-shake,shake),-10);
                if (Run.State == RunState.Combat) StepCombat(dt);
            }
            if (Paused || Run.State != RunState.Combat) CancelDrag();
            if(Run!=null && heroHealthFill)
            {
                float fraction=Mathf.Clamp01(Run.Health/Run.MaxHealth);
                Vector2 unit=config.squareSprite.bounds.size;
                heroHealthFill.transform.localScale=new Vector3(.91f*fraction/unit.x,.075f/unit.y,1);
                heroHealthFill.transform.localPosition=new Vector3(-.455f*(1-fraction),.73f,0);
            }
            hud.Refresh();
        }
        private void StepCombat(float dt)
        {
            Fog.Step(dt);
            elapsed += dt; CooldownLeft = Mathf.Max(0, CooldownLeft - dt);
            float scale=1+elapsed/60f*config.difficultyGrowthPerMinute;
            spawnSettings.healthMultiplier=config.spawning.healthMultiplier*scale;
            spawnSettings.speedMultiplier=config.spawning.speedMultiplier*Mathf.Sqrt(scale);
            spawnSettings.maxAlive=config.spawning.maxAlive;
            spawnIn-=dt;
            if(spawnIn<=0)
            { spawnIn=config.spawning.spawnInterval/Mathf.Min(3f,scale);
                if (config.PickEnemy(elapsed,random,out EnemyKind kind)) enemies.Spawn(kind,spawnSettings); }
            bossDirector.Step(dt,spawnSettings);
            StepHeroDrag();enemies.HeroPosition=HeroPosition;
            enemies.Step(dt, elapsed);
            if (Run.State == RunState.Lost) { FinishCombat(); return; }
            if (CooldownLeft <= 0) TryAttackWorld(HeroPosition+Vector2.up);
            bullets.Step(dt,enemies);
            if(Run.SettleExperience())
            { Choices=Run.GetChoices(random); hud.Refresh(true); }
        }
        public bool TryAttackWorld(Vector2 point)
        {
            if (Run == null || Run.State != RunState.Combat || Paused || CooldownLeft > 0) return false;
            ArrowAttack attack = Run.CaptureAttack(config.bulletRadius);
            if (!bullets.Fire(HeroPosition + Vector2.up * 0.65f, attack)) return false;
            CooldownLeft = Run.Cooldown;
            shake = Mathf.Max(shake, config.screenShake * presentation.screenShakeMultiplier); return true;
        }
        private void FinishCombat()
        {
            enemies.Clear(); bossDirector.Stop(); bullets.Clear(); feedback.Clear(); CooldownLeft = 0; shake = 0;
            worldCamera.transform.position = new Vector3(0,0,-10);
            hud.Refresh(true);
        }
        private void OnCrystalHit()
        { heroFlash=0.18f;RefreshHeroFlash();feedback.PlayerHit(HeroPosition); shake = Mathf.Max(shake,config.screenShake * presentation.screenShakeMultiplier * 2); }
        private void RefreshHeroFlash()
        {
            if(heroRenderers==null)return;
            for(int i=0;i<heroRenderers.Length;i++)
                if(heroRenderers[i].name!="Ground Shadow" && !heroRenderers[i].name.Contains("Health"))heroRenderers[i].color=Color.Lerp(heroColors[i],new Color(1,0.35f,0.3f),Mathf.Clamp01(heroFlash/0.18f));
        }
        private void FitCamera()
        {
            CancelDrag();
            lastScreen = new Vector2(Screen.width,Screen.height);
            float aspect = Mathf.Max(0.2f, Screen.width / (float)Mathf.Max(1,Screen.height));
            worldCamera.orthographicSize = Mathf.Max(config.arenaHalfHeight + 2.1f, (config.arenaHalfWidth + 0.25f) / aspect);
            if (hero) { hero.position = new Vector3(hero.position.x, -worldCamera.orthographicSize * 0.54f, 0); MoveHero(hero.position.x); }
            if(enemies!=null) { enemies.HeroPosition=HeroPosition;enemies.Top=worldCamera.orthographicSize; enemies.Bottom=-worldCamera.orthographicSize; }
            if(bullets!=null) bullets.Top=worldCamera.orthographicSize;
        }

    }
}
