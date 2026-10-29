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
        private ITapInput input;
        private readonly System.Random random = new System.Random();
        private float elapsed, spawnIn, shake;
        private Vector2 lastScreen;
        private Sprite gridSprite;

        private void Start()
        {
            if (!config || !worldCamera || !crystal || !enemyPrefab || !feedback || !hud)
            { Debug.LogError("Main scene references are incomplete.", this); enabled = false; return; }
            string error = config.ValidateData();
            if (error != null) { Debug.LogError(error, config); enabled = false; return; }
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            Input.simulateMouseWithTouches = false;
            Screen.orientation = ScreenOrientation.Portrait;
            input = new UnityTapInput();
            feedback.Initialize(config);
            Fog = gameObject.AddComponent<FogOfWar>(); Fog.Initialize(worldCamera,config.squareSprite); feedback.Fog = Fog;
            CreateArena(); bullets = new BulletSystem(config,transform); crystal.gameObject.SetActive(false); ResetRun(); hud.Initialize(this, config); FitCamera();
        }
        private void ResetRun()
        {
            if (enemies != null) enemies.Clear();
            Run = new RunModel(config.progression, config.elements);
            // Reuse the original pool on restart by rebinding the model, avoiding a new set of objects.
            if (enemies == null) enemies = new EnemySystem(config, enemyPrefab, transform, Run, feedback, OnCrystalHit);
            else enemies.BindRun(Run);
            if (bossDirector == null) bossDirector = new BossDirector(config,enemies,Fog,feedback);
            bossDirector.Reset();
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
        public void SelectElement(ElementKind kind)
        {
            if (Run == null || Run.State != RunState.Combat || Paused) return;
            if (Run.Elements.Select(kind)) hud.Refresh(true);
        }
        public void ToggleSound()
        { feedback.SoundEnabled = !feedback.SoundEnabled; if (!feedback.SoundEnabled) feedback.PauseAudio(); hud.Refresh(true); }
        public void TogglePause() { if (Run.State == RunState.Combat) SetPaused(!Paused); }
        private void SetPaused(bool paused)
        { Paused = paused; if (paused) feedback.PauseAudio(); hud.Refresh(true); }
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
                feedback.Step(dt);
                shake = Mathf.MoveTowards(shake, 0, dt * 0.35f);
                worldCamera.transform.position = new Vector3(UnityEngine.Random.Range(-shake,shake),UnityEngine.Random.Range(-shake,shake),-10);
                if (Run.State == RunState.Combat) StepCombat(dt);
            }
            hud.Refresh();
        }
        private void StepCombat(float dt)
        {
            Fog.Step(dt); Run.Elements.Step(dt);
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
            enemies.Step(dt, elapsed);
            if (Run.State == RunState.Lost) { FinishCombat(); return; }
            if (input.TryGetTap(out Vector2 screen) && Screen.safeArea.Contains(screen) && !hud.BlocksTap(screen))
            {
                Vector3 point = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
                TryAttackWorld(point);
            }
            bullets.Step(dt,enemies);
            if(Run.SettleExperience())
            { Choices=Run.GetChoices(random); hud.Refresh(true); }
        }
        public bool TryAttackWorld(Vector2 point)
        {
            if (Run == null || Run.State != RunState.Combat || Paused || CooldownLeft > 0) return false;
            ArrowAttack attack = Run.CaptureAttack(config.bulletRadius);
            if (!bullets.Fire(point, attack)) return false;
            Run.Elements.ConsumeSelected();
            CooldownLeft = Run.Cooldown;
            shake = Mathf.Max(shake, config.screenShake); return true;
        }
        private void FinishCombat()
        {
            enemies.Clear(); bossDirector.Stop(); bullets.Clear(); feedback.Clear(); CooldownLeft = 0; shake = 0;
            worldCamera.transform.position = new Vector3(0,0,-10);
            hud.Refresh(true);
        }
        private void OnCrystalHit()
        { feedback.PlayerHit(-worldCamera.orthographicSize+0.4f); shake = Mathf.Max(shake,config.screenShake*2); }
        private void FitCamera()
        {
            lastScreen = new Vector2(Screen.width,Screen.height);
            float aspect = Mathf.Max(0.2f, Screen.width / (float)Mathf.Max(1,Screen.height));
            worldCamera.orthographicSize = Mathf.Max(config.arenaHalfHeight + 2.1f, (config.arenaHalfWidth + 0.25f) / aspect);
            if(enemies!=null) { enemies.Top=worldCamera.orthographicSize; enemies.Bottom=-worldCamera.orthographicSize; }
            if(bullets!=null) bullets.Top=worldCamera.orthographicSize;
        }
        private void CreateArena()
        {
            gridSprite = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(0.5f,0.5f),1);
            var root = new GameObject("Arena Grid"); root.transform.SetParent(transform,false);
            for (int i = -6; i <= 6; i++)
            {
                GridLine(root.transform,new Vector2(0,i),new Vector2(config.arenaHalfWidth*2,0.012f));
                if (Mathf.Abs(i) <= 4) GridLine(root.transform,new Vector2(i,0),new Vector2(0.012f,config.arenaHalfHeight*2));
            }

        }
        private void GridLine(Transform parent,Vector2 point,Vector2 scale)
        {
            var line = new GameObject("Grid Line"); line.transform.SetParent(parent,false);
            line.transform.localPosition=point; line.transform.localScale=new Vector3(scale.x,scale.y,1);
            var renderer=line.AddComponent<SpriteRenderer>(); renderer.sprite=gridSprite;
            renderer.color=new Color(0.15f,0.28f,0.35f,0.25f); renderer.sortingOrder=-20;
        }
        private void OnDestroy() { if (gridSprite) Destroy(gridSprite); }
    }
}
