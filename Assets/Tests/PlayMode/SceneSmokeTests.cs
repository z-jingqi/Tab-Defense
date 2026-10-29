using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TapDefense.Core;
namespace TapDefense.Tests
{
 public sealed class SceneSmokeTests
 {
  [UnityTest] public IEnumerator ArtworkCoversArenaAndIsWiredToHudAndProjectile()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();
   var hud=Object.FindFirstObjectByType<GameHud>();
   var arena=g.transform.Find("Grass Battlefield").GetComponent<SpriteRenderer>();
   var camera=Camera.main;
   Assert.AreSame(g.Config.battlefieldSprite,arena.sprite);
   Assert.Less(arena.sortingOrder,0);
   Assert.GreaterOrEqual(arena.bounds.size.y,camera.orthographicSize*2);
   Assert.GreaterOrEqual(arena.bounds.size.x,camera.orthographicSize*2*camera.aspect);
   for(int i=0;i<4;i++)
   {
    var icon=hud.transform.Find("Safe Area/Element "+(ElementKind)i+"/Icon").GetComponent<UnityEngine.UI.Image>();
    Assert.AreSame(g.Config.ElementIcon((ElementKind)i),icon.sprite);
    Assert.IsFalse(icon.raycastTarget);Assert.IsTrue(icon.preserveAspect);
   }
   g.StartRun();Assert.IsTrue(g.TryAttackWorld(Vector2.zero));
   SpriteRenderer arrow=null;
   foreach(var renderer in g.GetComponentsInChildren<SpriteRenderer>())
    if(renderer.name=="Pooled Arrow") { arrow=renderer;break; }
   Assert.IsNotNull(arrow);Assert.AreSame(g.Config.arrowSprite,arrow.sprite);
   Assert.AreEqual(Color.white,arrow.color);Assert.AreEqual(Quaternion.identity,arrow.transform.rotation);
   var wake=arrow.transform.Find("Air Wake").GetComponent<LineRenderer>();
   Assert.IsFalse(wake.enabled);Assert.AreEqual(0,wake.positionCount);
   yield return null;yield return null;
   Assert.IsTrue(wake.enabled);Assert.AreEqual(3,wake.positionCount);
   Assert.AreSame(arrow.sharedMaterial,wake.sharedMaterial);
   Assert.Less(wake.sortingOrder,arrow.sortingOrder);
   Assert.Less(wake.GetPosition(2).y,wake.GetPosition(0).y);
   Assert.Less(wake.GetPosition(0).y,arrow.transform.position.y);
   Assert.LessOrEqual(Vector3.Distance(wake.GetPosition(0),wake.GetPosition(2)),g.Config.bulletRadius*1.8f+0.001f);
   g.RestartRun();Assert.IsFalse(wake.enabled);Assert.AreEqual(0,wake.positionCount);
   g.StartRun();Assert.IsTrue(g.TryAttackWorld(Vector2.right*2));
   Assert.IsFalse(wake.enabled);Assert.AreEqual(0,wake.positionCount);
   g.RestartRun();LogAssert.NoUnexpectedReceived();
  }

  [UnityTest] public IEnumerator FirePauseLevelAndRestart()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();Assert.IsNotNull(g.Run);g.StartRun();
   Assert.IsTrue(g.TryAttackWorld(Vector2.zero));Assert.AreEqual(1,g.ActiveBullets);Assert.IsFalse(g.TryAttackWorld(Vector2.zero));
   g.TogglePause();float time=g.SurvivalTime;yield return new WaitForSeconds(.15f);Assert.AreEqual(time,g.SurvivalTime);g.TogglePause();
   g.Run.RecordKill(35);yield return null;Assert.AreEqual(RunState.Upgrade,g.Run.State);
   g.SelectUpgrade(0);g.SelectUpgrade(0);Assert.AreEqual(RunState.Combat,g.Run.State);
   int count=g.transform.childCount;g.RestartRun();Assert.AreEqual(0,g.ActiveBullets);Assert.AreEqual(0,g.AliveEnemies);Assert.AreEqual(count,g.transform.childCount);LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator EnemiesMoveDownAndBulletHitsAlongItsPath()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();g.StartRun();yield return new WaitForSeconds(.3f);
   var e=Object.FindFirstObjectByType<EnemyView>();Assert.IsNotNull(e);float y=e.Position.y;
   yield return new WaitForSeconds(.1f);Assert.Less(e.Position.y,y);
   float hp=e.Health;g.TryAttackWorld(e.Position+Vector2.down*.5f);yield return new WaitForSeconds(.15f);
   Assert.IsTrue(!e.gameObject.activeSelf || e.Health<hp);g.RestartRun();LogAssert.NoUnexpectedReceived();
  }

  [UnityTest] public IEnumerator ElementUnlockCentersHudAndUiDoesNotSpendCharges()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();var hud=Object.FindFirstObjectByType<GameHud>();g.StartRun();
   var fire=hud.transform.Find("Safe Area/Element Fire").GetComponent<RectTransform>();
   Assert.IsFalse(fire.gameObject.activeSelf);
   for(int i=0;i<4;i++)
   {
    g.Run.RecordKill(g.Run.NextUpgradeCost);g.Run.SettleExperience();
    Assert.IsTrue(g.Run.ApplyUpgrade((UpgradeKind)((int)UpgradeKind.UnlockFire+i)));hud.Refresh(true);
    Assert.AreEqual(ElementBarLayout.Offset(0,i+1,188),fire.anchoredPosition.x,.001f);
   }
   Canvas.ForceUpdateCanvases();
   Vector2 point=RectTransformUtility.WorldToScreenPoint(null,fire.position);
   Assert.IsTrue(hud.BlocksTap(point));
   g.SelectElement(ElementKind.Fire);Assert.AreEqual(0,g.ActiveBullets);
   Assert.IsTrue(g.TryAttackWorld(Vector2.zero));Assert.AreEqual(2,g.Run.Elements.Get(ElementKind.Fire).Count);
   Assert.IsFalse(g.TryAttackWorld(Vector2.zero));Assert.AreEqual(2,g.Run.Elements.Get(ElementKind.Fire).Count);
   float cooldown=g.CooldownLeft;g.SelectElement(ElementKind.Ice);Assert.AreEqual(cooldown,g.CooldownLeft);
   g.TogglePause();float recharge=g.Run.Elements.Get(ElementKind.Fire).RechargeLeft;
   yield return new WaitForSeconds(.15f);Assert.AreEqual(recharge,g.Run.Elements.Get(ElementKind.Fire).RechargeLeft);
   g.RestartRun();hud.Refresh(true);Assert.AreEqual(0,g.Run.Elements.UnlockedCount);Assert.IsFalse(fire.gameObject.activeSelf);
   LogAssert.NoUnexpectedReceived();
  }
 }
}
