using UnityEngine;
using TapDefense.Core;

namespace TapDefense
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyView : MonoBehaviour
    {
        public EnemyDefinition Definition { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Speed { get; private set; }
        public float NextContact;
        public int SpawnId { get; private set; }
        private float burnLeft, burnDps, slowLeft, slow = 1, frozen, pullLeft, pullSpeed;
        private int chill;
        private float burnBank, burnTick;
        private Vector2 pullPoint;
        public Vector2 Position => transform.position;
        private SpriteRenderer body;
        private float flash;
        private float phase;

        public void Spawn(EnemyDefinition definition, SpawnSettings settings, Vector2 position, int spawnId)
        {
            SpawnId = spawnId; burnLeft = burnDps = slowLeft = frozen = pullLeft = 0; slow = 1; chill = 0; burnBank = burnTick = 0;
            body = GetComponent<SpriteRenderer>();
            Definition = definition;
            Health = MaxHealth = definition.health * settings.healthMultiplier;
            Speed = definition.speed * settings.speedMultiplier;
            body.sprite = definition.sprite; body.color = definition.color;
            transform.position = position;
            transform.localScale = Vector3.one * (definition.radius * 2f);
            phase = Random.value * 6.28f; flash = 0; NextContact = 0;
        }
        public float Step(float deltaTime)
        {
            Vector2 point = Position;
            burnBank += Mathf.Min(burnLeft, deltaTime) * burnDps; burnLeft = Mathf.Max(0, burnLeft - deltaTime);
            burnTick += deltaTime; float burn = 0;
            if (burnTick >= 0.25f || burnLeft <= 0) { burn = burnBank; burnBank = 0; burnTick = 0; }
            if (frozen <= 0) point += Vector2.down * Speed * (slowLeft > 0 ? slow : 1) * deltaTime;
            if (pullLeft > 0) point = Vector2.MoveTowards(point, pullPoint, pullSpeed * Mathf.Min(deltaTime, pullLeft));
            transform.position = point;
            frozen = Mathf.Max(0, frozen - deltaTime); pullLeft = Mathf.Max(0, pullLeft - deltaTime);
            slowLeft = Mathf.Max(0, slowLeft - deltaTime); if (slowLeft == 0) chill = 0;
            phase += deltaTime * (Definition.kind == EnemyKind.Fast ? 15 : 6);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 12f);
            flash = Mathf.Max(0, flash - deltaTime * 6);
            body.color = Color.Lerp(Definition.color, Color.white, flash);
            return burn;
        }
        public void Burn(float dps, float duration)
        { burnDps = Mathf.Max(burnDps, dps); burnLeft = Mathf.Max(burnLeft, duration); }
        public void Chill(ElementDefinition element)
        {
            slow = Definition.kind == EnemyKind.Boss ? Mathf.Max(0.75f, element.slowMultiplier) : element.slowMultiplier;
            slowLeft = element.duration;
            if (frozen <= 0 && ++chill >= element.freezeHits)
            { chill = 0; frozen = element.freezeSeconds * (Definition.kind == EnemyKind.Boss ? 0.4f : 1); }
        }
        public void Pull(Vector2 point, ElementDefinition element)
        { pullPoint = point; pullLeft = element.duration; pullSpeed = element.pullSpeed; }
        public bool Hit(float damage)
        {
            Health -= Mathf.Max(0, damage); flash = 1;
            return Health <= 0;
        }
    }
}
