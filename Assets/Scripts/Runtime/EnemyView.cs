using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyView : MonoBehaviour
    {
        public EnemyDefinition Definition { get; private set; }
        public EnemyVitals Vitals { get; } = new EnemyVitals();
        public float Health => Vitals.Health;
        public float MaxHealth => Vitals.MaxHealth;
        public float Speed { get; private set; }
        public int SpawnId { get; private set; }
        public bool Summoned { get; private set; }
        public int ExperienceReward { get; private set; }
        public Vector2 Position => transform.position;
        public bool Arrived => Definition.kind == EnemyKind.Boss && Position.y <= bossAnchor + 0.01f;
        public bool HoldingLine { get; private set; }
        public float AssemblyFeetY { get; set; } = -3.4f;
        public Vector2 HeroFeet { get; set; }
        public Vector2 Feet => Position + Vector2.down * (Definition.radius * 0.92f);
        public float AttackCooldown { get; private set; }
        public bool WindingUp => windup > 0;
        private float windup, slashLeft;
        private SpriteRenderer attackSlash;
        public bool CanReachHero => Mathf.Abs(Feet.x-HeroFeet.x) <= 0.65f && Vector2.Distance(Feet,HeroFeet) <= 1.95f;
        public bool StepMelee(float dt)
        {
            if (Definition.kind == EnemyKind.Boss || !HoldingLine || Vitals.StaggerLeft > 0 || Health <= 0) return false;
            AttackCooldown = Mathf.Max(0,AttackCooldown-dt);
            if (windup > 0)
            {
                if (!CanReachHero) { windup=0; warning=false; AttackCooldown=0.35f; return false; }
                windup=Mathf.Max(0,windup-dt);warning=windup>0;
                if (windup==0) { AttackCooldown=1.25f;slashLeft=0.16f;RefreshVisuals();return CanReachHero; }
            }
            else if(AttackCooldown<=0 && CanReachHero) { windup=0.3f;warning=true; }
            return false;
        }
        public string BossAction { get; set; } = "";
        private float bossAnchor, halfWidth;
        private SpriteRenderer body, hpBack, hpFill, shieldFill, groundShadow;
        private TextMesh status;
        private float flash, phase;
        private float hitFlashDuration = 0.12f, hitFlashStrength = 0.18f, appliedFlash = -1;
        private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
        private MaterialPropertyBlock hitProperties;
        private bool warning;
        private readonly SpriteAnimationPlayback movementAnimation = new SpriteAnimationPlayback();

        // Called once for each pooled instance during construction, never from the hot combat loop.
        public void Prepare(Sprite square, Material spriteMaterial = null, float flashDuration = 0.12f, float flashStrength = 0.18f)
        {
            body = GetComponent<SpriteRenderer>();
            groundShadow = Visual("Ground Shadow", square, new Color(0.03f,0.07f,0.04f,0.22f), -20);
            groundShadow.transform.localPosition = new Vector3(0,-0.46f,0);
            groundShadow.transform.localScale = new Vector3(0.82f,0.16f,1);
            if (spriteMaterial) body.sharedMaterial = spriteMaterial;
            hitFlashDuration = Mathf.Clamp(flashDuration, 0.05f, 0.3f);
            hitFlashStrength = Mathf.Clamp(flashStrength, 0, 0.35f);
            hitProperties = new MaterialPropertyBlock();
            attackSlash = Visual("Melee Slash",square,new Color(1,0.8f,0.3f,0.9f),20);
            attackSlash.transform.localPosition = new Vector3(0,-0.72f,0);
            attackSlash.transform.localScale = new Vector3(0.10f,0.65f,1);
            attackSlash.transform.localRotation=Quaternion.Euler(0,0,-35);attackSlash.enabled=false;
            hpBack = Visual("Health Background",square,new Color(0.05f,0.07f,0.1f),7);
            hpFill = Visual("Health",square,new Color(1f,0.2f,0.25f),8);
            shieldFill = Visual("Shield",square,new Color(0.4f,0.7f,1),9);
            var go = new GameObject("Enemy State"); go.transform.SetParent(transform,false);
            status = go.AddComponent<TextMesh>(); status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 40; status.characterSize = 0.06f; status.anchor = TextAnchor.LowerCenter;
            var mesh = go.GetComponent<MeshRenderer>(); mesh.sharedMaterial = status.font.material; mesh.sortingOrder = 10;
        }
        private SpriteRenderer Visual(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform,false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }
        public void Spawn(EnemyDefinition definition, SpawnSettings settings, Vector2 position, int spawnId,
            float width = 4.5f, bool summoned = false, int experience = -1, float anchor = 0)
        {
            HoldingLine=false;windup=slashLeft=0;AttackCooldown=0;
            SpawnId=spawnId;warning=false;bossAnchor=anchor;halfWidth=width;
            Summoned = summoned; ExperienceReward = experience >= 0 ? experience : definition.experience;
            if (!body) body = GetComponent<SpriteRenderer>();
            Definition = definition; Vitals.Reset(definition.health * settings.healthMultiplier);
            Speed = definition.speed * settings.speedMultiplier;
            movementAnimation.Reset(definition.movementAnimation, definition.sprite);
            body.sprite = movementAnimation.Current; body.color = definition.color;
            transform.position = position; transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (definition.radius * 2f);
            phase = Random.value * 6.28f; flash = 0; appliedFlash = -1; BossAction = ""; ApplyHitFeedback(); RefreshVisuals();
        }
        public float Step(float dt)
        {
            Vector2 point = Position;
            Vitals.Step(dt);
            float movementDt=Vitals.StaggerLeft>0?0:dt;
            if (Definition.kind == EnemyKind.Boss)
                point.y = Mathf.MoveTowards(point.y,bossAnchor,Speed*movementDt);
            else
            {
                float centerLine=AssemblyFeetY+Definition.radius*0.92f;
                if (!HoldingLine)
                {
                    point.y=Mathf.MoveTowards(point.y,centerLine,Speed*movementDt);
                    HoldingLine=point.y<=centerLine+0.001f;
                }
                else
                {
                    point.y=centerLine;
                    if(windup<=0 && Mathf.Abs(point.x-HeroFeet.x)>0.4f)
                        point.x=Mathf.MoveTowards(point.x,HeroFeet.x,Speed*movementDt);
                }
            }
            // Only self-directed locomotion advances walking frames. Wind-up, waiting at
            // the boss anchor and forced displacement must not make a stationary enemy walk.
            Vector2 locomotionPoint = point;
            locomotionPoint.x = Mathf.Clamp(locomotionPoint.x,-halfWidth+Definition.radius,halfWidth-Definition.radius);
            if (Definition.kind == EnemyKind.Boss) locomotionPoint.y = Mathf.Max(bossAnchor,locomotionPoint.y);
            bool locomoting = movementDt > 0 && (locomotionPoint - Position).sqrMagnitude > 0;
            point.x = Mathf.Clamp(point.x,-halfWidth+Definition.radius,halfWidth-Definition.radius);
            if (Definition.kind == EnemyKind.Boss) point.y = Mathf.Max(bossAnchor,point.y);
            if(Definition.kind!=EnemyKind.Boss) point.y=Mathf.Max(point.y,AssemblyFeetY+Definition.radius*0.92f);
            transform.position = point;
            slashLeft=Mathf.Max(0,slashLeft-dt);
            body.sprite = movementAnimation.Step(locomoting ? movementDt : 0);
            if (movementAnimation.HasAnimation || !Definition.swayWhileMoving) transform.rotation = Quaternion.identity;
            else
            {
                phase += (locomoting ? movementDt : 0) * (Definition.kind == EnemyKind.Fast ? 15 : 6);
                transform.rotation = Quaternion.Euler(0,0,Mathf.Sin(phase)*12f);
            }
            flash = Mathf.Max(0,flash-dt);
            ApplyHitFeedback();
            RefreshVisuals(); return 0;
        }
        private void ApplyHitFeedback()
        {
            Color tint = warning ? new Color(1,0.3f,0.2f,Definition.color.a) : Definition.color;
            float amount = Mathf.Clamp01(flash / hitFlashDuration);
            body.color = Definition.kind == EnemyKind.Boss ? Color.Lerp(tint, Color.white, amount) : tint;
            float lift = Definition.kind == EnemyKind.Boss ? 0 : amount * hitFlashStrength;
            if (lift == appliedFlash) return;
            if (hitProperties == null) hitProperties = new MaterialPropertyBlock();
            body.GetPropertyBlock(hitProperties);
            hitProperties.SetFloat(HitFlashId, lift);
            body.SetPropertyBlock(hitProperties); appliedFlash = lift;
        }
        public void RefreshVisuals()
        {
            if (!hpBack) return;
            // Keep painted sprites upright; lower feet occlude higher feet in the 2D ground plane.
            int order = 100 - Mathf.RoundToInt(Position.y * 10);
            body.sortingOrder = order; hpBack.sortingOrder = order+1; hpFill.sortingOrder = order+2;
            attackSlash.sortingOrder=order+5;attackSlash.enabled=slashLeft>0;
            shieldFill.sortingOrder = order+3; status.GetComponent<MeshRenderer>().sortingOrder = order+4;
            bool hp = Health > 0;
            hpBack.enabled = hp; hpFill.enabled = hp; shieldFill.enabled = Vitals.Shield > 0;
            hpBack.transform.localPosition = new Vector3(0,0.64f,0); hpBack.transform.localScale = new Vector3(0.72f,0.1f,1);
            hpFill.transform.localPosition = new Vector3(-(1-Health/MaxHealth)*0.36f,0.64f,0);
            hpFill.transform.localScale = new Vector3(0.72f*Health/MaxHealth,0.08f,1);
            shieldFill.transform.localPosition = new Vector3(0,0.85f,0);
            shieldFill.transform.localScale = new Vector3(Vitals.MaxShield>0 ? 1.25f*Vitals.Shield/Vitals.MaxShield : 0,0.09f,1);
            status.transform.localPosition = new Vector3(0,0.95f,0);
            string label = warning ? "!" : "";
            if (Definition.kind == EnemyKind.Boss)
                label = Vitals.StaggerLeft > 0 ? "STAGGER" : Vitals.Healing ? "HEALING" : BossAction;
            if (Vitals.Armor > 0) label += " A:" + Vitals.Armor;
            // Avoid assigning TextMesh.text when unchanged (mesh rebuilds are relatively expensive).
            if (status.text != label) status.text = label;
        }



        public DamageResult Receive(float damage, DamageSource source)
        { var result = Vitals.Receive(damage,source); flash = hitFlashDuration; ApplyHitFeedback(); RefreshVisuals(); return result; }
    }
}
