using System.Collections.Generic;
using UnityEngine;

namespace TapDefense
{
    public sealed class FeedbackSystem : MonoBehaviour
    {
        [SerializeField] private PulseEffect effectPrefab;
        [SerializeField] private DamageNumber numberPrefab;
        private ComponentPool<PulseEffect> effects;
        private ComponentPool<DamageNumber> numbers;
        private readonly List<PulseEffect> activeEffects = new List<PulseEffect>(128);
        private readonly List<DamageNumber> activeNumbers = new List<DamageNumber>(64);
        private GameConfig config;
        private Font font;
        private AudioSource audioSource;
        private AudioClip tapClip, killClip, hurtClip;
        private float killSoundReady;
        public bool SoundEnabled { get; set; } = true;

        public void Initialize(GameConfig settings)
        {
            config = settings;
            effects = new ComponentPool<PulseEffect>(effectPrefab, config.effectPoolSize, transform);
            numbers = new ComponentPool<DamageNumber>(numberPrefab, config.numberPoolSize, transform);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            tapClip = Tone(155, 65, 0.08f); killClip = Tone(400, 700, 0.06f); hurtClip = Tone(110, 45, 0.2f);
        }
        private static AudioClip Tone(float from, float to, float duration)
        {
            const int sampleRate = 22050;
            float[] samples = new float[(int)(sampleRate * duration)];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                phase += Mathf.Lerp(from, to, t) * 2 * Mathf.PI / sampleRate;
                samples[i] = Mathf.Sin(phase) * (1 - t) * (1 - t) * 0.22f;
            }
            AudioClip clip = AudioClip.Create("Synth feedback", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0); return clip;
        }
        private void Emit(Sprite sprite, Vector2 position, Color color, float start, float end, float life, Vector2 velocity)
        {
            PulseEffect effect = effects.Rent(); if (!effect) return;
            effect.Play(sprite, position, color, start, end, life, velocity); activeEffects.Add(effect);
        }
        public void Blast(Vector2 position, float radius, bool chain)
        {
            Color tint = chain ? new Color(1f, 0.55f, 0.22f) : new Color(0.25f, 1f, 0.85f);
            Emit(config.circleSprite, position, new Color(tint.r, tint.g, tint.b, 0.25f), radius * 1.8f, radius * 2f, 0.14f, Vector2.zero);
            Emit(config.ringSprite, position, tint, radius * 0.55f, radius * 2f, 0.27f, Vector2.zero);
            if (!chain && SoundEnabled) audioSource.PlayOneShot(tapClip, 0.7f);
        }
        public void Hit(Vector2 position, float damage, bool chain)
        {
            DamageNumber number = numbers.Rent(); if (!number) return;
            number.Play(position, damage, chain ? new Color(1, 0.7f, 0.3f) : Color.white, font); activeNumbers.Add(number);
        }
        public void Death(Vector2 position, Color tint, float radius)
        {
            Emit(config.ringSprite, position, tint, radius * 2, radius * 4, 0.23f, Vector2.zero);
            for (int i = 0; i < 3; i++)
                Emit(config.diamondSprite, position, tint, radius * 0.7f, 0, 0.3f, Random.insideUnitCircle * 1.7f);
            if (SoundEnabled && Time.unscaledTime >= killSoundReady)
            { audioSource.PlayOneShot(killClip, 0.38f); killSoundReady = Time.unscaledTime + 0.075f; }
        }
        public void PlayerHit(float y)
        {
            Emit(config.ringSprite, new Vector2(0,y), new Color(1, 0.3f, 0.4f), 0.6f, 1.9f, 0.3f, Vector2.zero);
            if (SoundEnabled) audioSource.PlayOneShot(hurtClip, 0.65f);
        }
        public void Upgrade()
        { Emit(config.ringSprite, Vector2.zero, new Color(0.7f, 0.5f, 1), 0.6f, 4, 0.6f, Vector2.zero); }
        public void Step(float dt)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
                if (!activeEffects[i].Step(dt)) { effects.Return(activeEffects[i]); activeEffects.RemoveAt(i); }
            for (int i = activeNumbers.Count - 1; i >= 0; i--)
                if (!activeNumbers[i].Step(dt)) { numbers.Return(activeNumbers[i]); activeNumbers.RemoveAt(i); }
        }
        public void Clear()
        { effects.ReturnAll(); numbers.ReturnAll(); activeEffects.Clear(); activeNumbers.Clear(); }
        public void PauseAudio() { if (audioSource) audioSource.Stop(); }
        private void OnDestroy()
        { if (tapClip) Destroy(tapClip); if (killClip) Destroy(killClip); if (hurtClip) Destroy(hurtClip); }
    }
}
