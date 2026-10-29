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
        public bool Frozen => frozen > 0;
        public string BossAction { get; set; } = "";
        private float burnLeft, burnDps, slowLeft, slow = 1, frozen, pullLeft, pullSpeed;
        private int chill;
        private float burnBank, burnTick, routeClock, bossAnchor, halfWidth;
        private Vector2 pullPoint;
        private SpriteRenderer body, hpBack, hpFill, shieldFill;
        private TextMesh status;
        private float flash, phase;
        private bool warning;

        // Called once for each pooled instance during construction, never from the hot combat loop.
        public void Prepare(Sprite square)
        {
            body = GetComponent<SpriteRenderer>();
            hpBack = Visual("Health Background",square,new Color(0.05f,0.07f,0.1f),7);
            hpFill = Visual("Health",square,new Color(0.4f,1,0.7f),8);
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
            SpawnId = spawnId; burnLeft = burnDps = slowLeft = frozen = pullLeft = 0; slow = 1; chill = 0;
            burnBank = burnTick = routeClock = 0; warning = false; bossAnchor = anchor; halfWidth = width;
            Summoned = summoned; ExperienceReward = experience >= 0 ? experience : definition.experience;
            if (!body) body = GetComponent<SpriteRenderer>();
            Definition = definition; Vitals.Reset(definition.health * settings.healthMultiplier);
            Speed = definition.speed * settings.speedMultiplier;
            body.sprite = definition.sprite; body.color = definition.color;
            transform.position = position; transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (definition.radius * 2f);
            phase = Random.value * 6.28f; flash = 0; BossAction = ""; RefreshVisuals();
        }
        public float Step(float dt)
        {
            Vector2 point = Position;
            // Freezing pauses the boss's defensive timers and heal channel as well as movement.
            if (!Frozen) Vitals.Step(dt);
            burnBank += Mathf.Min(burnLeft,dt) * burnDps; burnLeft = Mathf.Max(0,burnLeft-dt);
            if (burnLeft == 0) burnDps = 0;
            burnTick += dt; float burn = 0;
            if (burnTick >= 0.25f || burnLeft <= 0) { burn = burnBank; burnBank = 0; burnTick = 0; }
            float movementDt = Frozen || Vitals.StaggerLeft > 0 ? 0 : dt * (slowLeft > 0 ? slow : 1);
            if (Definition.kind == EnemyKind.Boss)
                point.y = Mathf.MoveTowards(point.y,bossAnchor,Speed*movementDt);
            else
            {
                RouteStep route = EnemyRoutes.Advance(Definition.route,ref routeClock,movementDt); warning = route.warning;
                point += new Vector2(route.horizontal,-Speed*route.verticalMultiplier*movementDt);
            }
            if (pullLeft > 0) point = Vector2.MoveTowards(point,pullPoint,pullSpeed*Mathf.Min(dt,pullLeft));
            point.x = Mathf.Clamp(point.x,-halfWidth+Definition.radius,halfWidth-Definition.radius);
            if (Definition.kind == EnemyKind.Boss) point.y = Mathf.Max(bossAnchor,point.y);
            transform.position = point;
            frozen = Mathf.Max(0,frozen-dt); pullLeft = Mathf.Max(0,pullLeft-dt);
            slowLeft = Mathf.Max(0,slowLeft-dt); if (slowLeft == 0) chill = 0;
            phase += dt * (Definition.kind == EnemyKind.Fast ? 15 : 6);
            transform.rotation = Quaternion.Euler(0,0,Mathf.Sin(phase)*12f);
            flash = Mathf.Max(0,flash-dt*6);
            body.color = Color.Lerp(warning ? new Color(1,0.3f,0.2f) : Definition.color,Color.white,flash);
            RefreshVisuals(); return burn;
        }
        public void RefreshVisuals()
        {
            if (!hpBack) return;
            bool hp = Health < MaxHealth && Health > 0;
            hpBack.enabled = hp; hpFill.enabled = hp; shieldFill.enabled = Vitals.Shield > 0;
            hpBack.transform.localPosition = new Vector3(0,0.7f,0); hpBack.transform.localScale = new Vector3(1.25f,0.1f,1);
            hpFill.transform.localPosition = new Vector3(-(1-Health/MaxHealth)*0.625f,0.7f,0);
            hpFill.transform.localScale = new Vector3(1.25f*Health/MaxHealth,0.08f,1);
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
        public void Burn(float dps, float duration)
        { burnDps = Mathf.Max(burnDps,dps); burnLeft = Mathf.Max(burnLeft,duration); }
        public void Chill(ElementDefinition element)
        {
            if (Vitals.Shield > 0) return;
            float strength = 1-Definition.controlResistance;
            slow = 1-(1-element.slowMultiplier)*strength; slowLeft = element.duration;
            if (frozen <= 0 && ++chill >= element.freezeHits) { chill = 0; frozen = element.freezeSeconds*strength; }
        }
        public void Pull(Vector2 point, ElementDefinition element)
        {
            if (Vitals.Shield > 0) return;
            pullPoint = point; pullLeft = element.duration; pullSpeed = element.pullSpeed*(1-Definition.controlResistance);
        }
        public DamageResult Receive(float damage, DamageSource source)
        { var result = Vitals.Receive(damage,source); flash = 1; RefreshVisuals(); return result; }
    }
}
