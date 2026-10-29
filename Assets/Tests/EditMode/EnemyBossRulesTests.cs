using NUnit.Framework;
using TapDefense.Core;
using UnityEngine;

namespace TapDefense.Tests
{
    public sealed class EnemyBossRulesTests
    {
        [Test] public void ShieldAbsorbsOverflowAndBreakStaggers()
        {
            var v = new EnemyVitals(); v.Reset(100); v.RaiseShield(20,7,1.5f);
            var hit = v.Receive(999,DamageSource.Arrow);
            Assert.IsTrue(hit.blocked); Assert.IsTrue(hit.shieldBroken); Assert.AreEqual(100,v.Health);
            Assert.AreEqual(0,v.Shield); Assert.AreEqual(1.5f,v.StaggerLeft);
            Assert.AreEqual(10,v.Receive(10,DamageSource.Area).bodyDamage);
        }
        [Test] public void ShieldTimeoutDoesNotStaggerAndTakesPriorityOverArmor()
        {
            var v = new EnemyVitals(); v.Reset(100); v.RaiseArmor(3); v.RaiseShield(20,1,1.5f);
            v.Receive(5,DamageSource.Burn); Assert.AreEqual(15,v.Shield); Assert.AreEqual(3,v.Armor);
            v.Step(1); Assert.AreEqual(0,v.Shield); Assert.AreEqual(0,v.StaggerLeft);
            v.Receive(5,DamageSource.Arrow); Assert.AreEqual(2,v.Armor); Assert.AreEqual(100,v.Health);
        }
        [Test] public void ArmorOnlyLosesLayersToDirectArrows()
        {
            var v = new EnemyVitals(); v.Reset(100); v.RaiseArmor(2); v.RaiseArmor(1);
            Assert.IsTrue(v.Receive(5,DamageSource.Burn).blocked);
            Assert.IsTrue(v.Receive(5,DamageSource.Area).blocked); Assert.AreEqual(2,v.Armor);
            v.Receive(500,DamageSource.Arrow); v.Receive(500,DamageSource.Arrow);
            Assert.AreEqual(100,v.Health); Assert.AreEqual(0,v.Armor);
            Assert.IsTrue(v.Receive(100,DamageSource.Arrow).killed);
        }
        [Test] public void HealIsCappedAndBodyDamageInterruptsAtThreshold()
        {
            var v = new EnemyVitals(); v.Reset(100); v.Receive(50,DamageSource.Arrow);
            v.BeginHeal(3,0.06f,0.08f,1.5f); v.Step(1); Assert.AreEqual(56,v.Health,.001f);
            v.Receive(7,DamageSource.Burn); Assert.IsTrue(v.Healing);
            Assert.IsTrue(v.Receive(1,DamageSource.Area).interrupted); Assert.IsFalse(v.Healing);
            float hp = v.Health; v.Step(4); Assert.AreEqual(hp,v.Health);
            v.BeginHeal(3,1,0.08f,1.5f); v.Step(10); Assert.AreEqual(100,v.Health); Assert.IsFalse(v.Healing);
        }
        [Test] public void AbsorbedDamageDoesNotInterruptAndPoolResetClearsDefense()
        {
            var v = new EnemyVitals(); v.Reset(100); v.RaiseArmor(3); v.BeginHeal(3,0.06f,0.08f,1.5f);
            v.Receive(999,DamageSource.Arrow); Assert.IsTrue(v.Healing);
            v.RaiseShield(50,7,1.5f); v.Reset(40);
            Assert.AreEqual(40,v.Health); Assert.AreEqual(0,v.Armor); Assert.AreEqual(0,v.Shield); Assert.IsFalse(v.Healing);
        }
        [TestCase(-1f,2f)] [TestCase(0.5f,0.26f)] [TestCase(2f,0.1f)]
        public void CloudStaysInsideTopThird(float x, float height)
        {
            var r = FogRegion.Create(x,0.65f,height);
            Assert.GreaterOrEqual(r.left,0); Assert.LessOrEqual(r.left+r.width,1);
            Assert.LessOrEqual(r.height,1f/3f); Assert.GreaterOrEqual(r.bottom,2f/3f-0.00001f);
            Assert.IsFalse(r.Contains(0.5f,0.5f)); Assert.IsTrue(r.Contains(r.left+r.width/2,0.99f));
        }
        [Test] public void DashHasStationaryTelegraphBeforeAcceleration()
        {
            var route = new RouteDefinition { kind = RouteKind.Dash }; float clock=0;
            Assert.AreEqual(1,EnemyRoutes.Advance(route,ref clock,1).verticalMultiplier);
            var warning = EnemyRoutes.Advance(route,ref clock,1.1f); Assert.IsTrue(warning.warning); Assert.AreEqual(0,warning.verticalMultiplier);
            var dash = EnemyRoutes.Advance(route,ref clock,0.7f); Assert.IsFalse(dash.warning); Assert.Greater(dash.verticalMultiplier,1);
        }
        [Test] public void SineAndStopGoRemainDeterministicWhenTimeIsFrozen()
        {
            var route = new RouteDefinition { kind=RouteKind.Sine }; float clock=0;
            var step=EnemyRoutes.Advance(route,ref clock,route.period/4);Assert.AreEqual(route.amplitude,step.horizontal,.001f);
            Assert.AreEqual(0,EnemyRoutes.Advance(route,ref clock,0).horizontal,.001f);
            route.kind=RouteKind.StopGo;clock=route.moveSeconds;
            Assert.AreEqual(0,EnemyRoutes.Advance(route,ref clock,0.1f).verticalMultiplier);
        }
        [Test] public void ScheduleHasOneBossAndWaitsAfterDeath()
        {
            var schedule = new BossSchedule(60);Assert.IsFalse(schedule.Step(59,false,90));
            Assert.IsTrue(schedule.Step(1,false,90));schedule.Spawned();
            Assert.IsFalse(schedule.Step(500,true,90)); Assert.IsFalse(schedule.Step(89,false,90));
            Assert.IsTrue(schedule.Step(1,false,90));
        }
        [Test] public void SpawnSelectionRespectsTimeWeightsAndExcludesBoss()
        {
            var config=ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.enemies=new[] {
                    new EnemyDefinition {kind=EnemyKind.Normal,spawnWeight=1},
                    new EnemyDefinition {kind=EnemyKind.Charger,spawnWeight=100,availableAfterSeconds=45},
                    new EnemyDefinition {kind=EnemyKind.Boss,spawnWeight=1000}
                };
                var random=new System.Random(1);
                for(int i=0;i<100;i++){Assert.IsTrue(config.PickEnemy(0,random,out EnemyKind kind));Assert.AreEqual(EnemyKind.Normal,kind);}
                bool charger=false;
                for(int i=0;i<100;i++){config.PickEnemy(45,random,out EnemyKind kind);Assert.AreNotEqual(EnemyKind.Boss,kind);charger|=kind==EnemyKind.Charger;}
                Assert.IsTrue(charger);
            }
            finally { Object.DestroyImmediate(config); }
        }
        [Test] public void FrozenBossPausesHealAndShieldRejectsNewControl()
        {
            var go=new GameObject("Test Boss",typeof(SpriteRenderer),typeof(EnemyView));
            try
            {
                var view=go.GetComponent<EnemyView>();
                view.Spawn(new EnemyDefinition {kind=EnemyKind.Boss,health=100,controlResistance=0.6f},new SpawnSettings(),Vector2.zero,1);
                view.Receive(50,DamageSource.Arrow);
                var ice=ElementDefinition.Defaults()[2];view.Chill(ice);view.Chill(ice);view.Chill(ice);Assert.IsTrue(view.Frozen);
                view.Vitals.BeginHeal(3,0.06f,0.08f,1.5f);view.Step(0.1f);Assert.AreEqual(50,view.Health);
                view.Step(1);view.Vitals.RaiseShield(180,7,1.5f);
                view.Chill(ice);view.Chill(ice);view.Chill(ice);Assert.IsFalse(view.Frozen);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
