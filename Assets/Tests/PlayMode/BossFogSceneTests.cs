using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TapDefense.Core;

namespace TapDefense.Tests
{
    public sealed class BossFogSceneTests
    {
        [UnityTest] public IEnumerator SingleCloudSuppressesFeedbackAndPausesThenResets()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            var g=Object.FindFirstObjectByType<GameController>(); var feedback=Object.FindFirstObjectByType<FeedbackSystem>();
            g.StartRun(); Assert.IsTrue(g.Fog.TryShow(6,0.5f,0.65f,1));
            Assert.LessOrEqual(g.Fog.Region.height,1f/3f);
            Assert.IsFalse(g.Fog.TryShow(99,0,0.95f,0.3f)); Assert.AreEqual(6,g.Fog.Remaining);
            var camera=Camera.main; Vector3 hidden=camera.ViewportToWorldPoint(new Vector3(0.5f,0.9f,10));
            Assert.IsTrue(g.Fog.IsHidden(hidden)); int before=feedback.SuppressedEvents;
            feedback.Hit(hidden,28,false);feedback.Death(hidden,Color.white,0.2f);feedback.Blast(hidden,0.3f,true);
            Assert.AreEqual(before+3,feedback.SuppressedEvents);
            Assert.AreEqual(1,g.transform.GetComponentsInChildren<FogOfWar>().Length);
            var cloud=g.transform.Find("Single Opaque Cloud");Assert.IsNotNull(cloud);
            var cover=cloud.GetComponent<SpriteRenderer>(); Assert.AreEqual(1,cover.color.a);Assert.Greater(cover.sortingOrder,30);
            g.TogglePause();float remaining=g.Fog.Remaining;yield return new WaitForSeconds(0.15f);Assert.AreEqual(remaining,g.Fog.Remaining);
            g.TogglePause(); g.Run.RecordKill(g.Run.NextUpgradeCost);yield return null;Assert.AreEqual(RunState.Upgrade,g.Run.State);
            remaining=g.Fog.Remaining;yield return new WaitForSeconds(0.15f);Assert.AreEqual(remaining,g.Fog.Remaining);
            g.RestartRun();Assert.IsFalse(g.Fog.Active);Assert.IsFalse(cover.enabled);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest] public IEnumerator BossStaysInArenaSummonsWithinLimitAndDeathClearsCloud()
        {
            yield return SceneManager.LoadSceneAsync("Main");yield return null;
            var g=Object.FindFirstObjectByType<GameController>();g.StartRun();
            // Drive the same director with explicit simulation time to avoid a minute-long test.
            g.Bosses.Step(60,g.Config.spawning);var boss=g.Bosses.ActiveBoss;Assert.IsNotNull(boss);Assert.AreEqual(1,g.AliveBosses);
            g.Bosses.Step(60,g.Config.spawning);Assert.AreSame(boss,g.Bosses.ActiveBoss);
            boss.Step(30);Assert.IsTrue(boss.Arrived);float y=boss.Position.y;boss.Step(100);Assert.AreEqual(y,boss.Position.y,.001f);
            g.Bosses.Step(3,g.Config.spawning);Assert.IsTrue(boss.BossAction.Contains("SUMMON"));
            g.Bosses.Step(1,g.Config.spawning);
            int summons=0;
            foreach(var enemy in Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
                if(enemy.Summoned){summons++;Assert.GreaterOrEqual(enemy.Position.y,0);Assert.AreEqual(0,enemy.ExperienceReward);}
            Assert.AreEqual(g.Config.bosses.summonCount,summons);Assert.LessOrEqual(summons,g.Config.bosses.summonAliveLimit);
            g.Bosses.Step(4,g.Config.spawning);g.Bosses.Step(1,g.Config.spawning);Assert.Greater(boss.Vitals.Shield,0);
            Assert.IsTrue(g.Fog.TryShow(6,0.5f,0.65f,0.26f));
            Vector2 original=boss.Position;
            boss.transform.position=Camera.main.ViewportToWorldPoint(new Vector3(0.5f,0.9f,10));
            Assert.IsFalse(g.BossInformationVisible);Object.FindFirstObjectByType<GameHud>().Refresh(true);
            Assert.IsFalse(Object.FindFirstObjectByType<GameHud>().transform.Find("Safe Area/Boss").gameObject.activeSelf);
            boss.transform.position=original;Assert.IsTrue(g.BossInformationVisible);
            int bossId=boss.SpawnId;
            // Hit the boss through the normal arrow/damage pipeline, first breaking its shield.
            g.Run.RecordKill(g.Run.NextUpgradeCost);g.Run.SettleExperience();g.Run.ApplyUpgrade(UpgradeKind.Damage);
            for(int i=0;i<100 && boss.gameObject.activeSelf && boss.SpawnId==bossId;i++)
            {g.TryAttackWorld(boss.Position);yield return new WaitForSeconds(g.Run.Cooldown+0.03f);if(g.Run.State==RunState.Upgrade)g.SelectUpgrade(0);}
            Assert.AreEqual(0,g.AliveBosses);Assert.IsFalse(g.Fog.Active);g.RestartRun();Assert.IsNull(g.Bosses.ActiveBoss);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
