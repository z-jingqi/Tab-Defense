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
  private static EnemySystem Enemies(GameController g) => (EnemySystem)typeof(GameController).GetField("enemies",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(g);
  [UnityTest] public IEnumerator VerticalApproachThenDirectPursuitFollowsMovingHero()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();g.StartRun();var enemies=Enemies(g);enemies.HeroPosition=g.HeroPosition;
   Assert.IsNull(g.transform.Find("Enemy Assembly Line"));float hp=g.Run.Health;
   foreach(var kind in new[]{EnemyKind.Normal,EnemyKind.Fast,EnemyKind.Tank,EnemyKind.Charger})
   {
    var e=enemies.SpawnAt(kind,new SpawnSettings(),new Vector2(2,4));
    for(int i=0;i<10;i++)enemies.Step(.05f,0);
    Assert.AreEqual(2,e.Position.x,.001f);Assert.Less(e.Position.y,4);Assert.IsFalse(e.PursuingHero);enemies.Clear();
   }
   var soldier=enemies.SpawnAt(EnemyKind.Normal,new SpawnSettings(),new Vector2(3,enemies.PursuitStartFeetY+.58f*.92f+.01f));
   enemies.Step(.1f,0);Assert.IsTrue(soldier.PursuingHero);Assert.AreEqual(hp,g.Run.Health);
   Vector2 from=soldier.Feet, target=g.HeroPosition+Vector2.down*.49f;
   Vector2 expected=Vector2.MoveTowards(from,target,soldier.Speed*.1f);
   enemies.Step(.1f,0);Assert.Less(Vector2.Distance(expected,soldier.Feet),.001f);
   Assert.Less(soldier.Feet.x,from.x);Assert.Less(soldier.Feet.y,from.y);
   enemies.HeroPosition=new Vector2(4,g.HeroPosition.y);from=soldier.Feet;
   expected=Vector2.MoveTowards(from,enemies.HeroPosition+Vector2.down*.49f,soldier.Speed*.1f);
   enemies.Step(.1f,0);Assert.Less(Vector2.Distance(expected,soldier.Feet),.001f);Assert.Greater(soldier.Feet.x,from.x);
   for(int i=0;i<180;i++)enemies.Step(.05f,0);
   float distance=Vector2.Distance(soldier.Feet,soldier.HeroFeet);
   Assert.GreaterOrEqual(distance,soldier.ContactDistance-.001f);Assert.LessOrEqual(distance,soldier.MeleeStopDistance+.12f);
   Assert.Greater(soldier.Feet.y,soldier.HeroFeet.y);Assert.AreEqual(1,enemies.AliveCount);
   g.RestartRun();LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator MeleeWindupDodgeCooldownDeathAndRestart()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();g.StartRun();var enemies=Enemies(g);enemies.HeroPosition=g.HeroPosition;
   var e=enemies.SpawnAt(EnemyKind.Normal,new SpawnSettings(),new Vector2(0,g.HeroPosition.y-.49f+1.18f+.58f*.92f));
   float hp=g.Run.Health;enemies.Step(.05f,0);Assert.IsTrue(e.WindingUp);Assert.AreEqual(hp,g.Run.Health);
   enemies.HeroPosition=new Vector2(4,g.HeroPosition.y);enemies.Step(.3f,0);Assert.AreEqual(hp,g.Run.Health);Assert.IsFalse(e.WindingUp);
   enemies.HeroPosition=g.HeroPosition;
   for(int i=0;i<30 && g.Run.Health==hp;i++)enemies.Step(.05f,0);
   Assert.Less(g.Run.Health,hp);hp=g.Run.Health;
   for(int i=0;i<10;i++)enemies.Step(.05f,0);Assert.AreEqual(hp,g.Run.Health);
   g.TogglePause();float time=g.SurvivalTime;yield return new WaitForSeconds(.15f);Assert.AreEqual(time,g.SurvivalTime);Assert.AreEqual(hp,g.Run.Health);g.TogglePause();
   var attack=g.Run.CaptureAttack(g.Config.bulletRadius);
   Assert.IsTrue(enemies.HitBullet(e.Position+Vector2.down*1,e.Position+Vector2.up*1,attack,new int[1],0,out int id));Assert.AreEqual(0,enemies.AliveCount);
   enemies.Step(.5f,0);Assert.AreEqual(hp,g.Run.Health);
   g.Run.RecordKill(g.Run.NextUpgradeCost);g.Run.SettleExperience();enemies.SpawnAt(EnemyKind.Normal,new SpawnSettings(),new Vector2(0,g.HeroPosition.y-.49f+1.18f+.58f*.92f));
   enemies.Step(2,0);Assert.AreEqual(hp,g.Run.Health);g.Run.ApplyUpgrade(UpgradeKind.Damage);
   g.Run.DamagePlayer(g.Run.Health-1);for(int i=0;i<40 && g.Run.State!=RunState.Lost;i++)enemies.Step(.05f,0);
   Assert.AreEqual(RunState.Lost,g.Run.State);Assert.AreEqual(0,g.Run.Health);
   g.RestartRun();Assert.AreEqual(0,g.AliveEnemies);Assert.AreEqual(g.Run.MaxHealth,g.Run.Health);Assert.AreEqual(0,g.ActiveBullets);LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator EmptyArenaFiresContinuouslyAndArrowsRecycle()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();g.StartRun();
   typeof(GameController).GetField("spawnIn",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(g,1000f);
   yield return new WaitForSeconds(.8f);Assert.AreEqual(0,g.AliveEnemies);Assert.Greater(g.ActiveBullets,1);
   g.MoveHero(2);yield return new WaitForSeconds(.4f);bool newLane=false;
   foreach(var arrow in g.GetComponentsInChildren<SpriteRenderer>())
    if(arrow.name=="Pooled Arrow" && Mathf.Abs(arrow.transform.position.x-2)<.001f) { newLane=true;Assert.Less(Quaternion.Angle(Quaternion.identity,arrow.transform.rotation),.01f); }
   Assert.IsTrue(newLane);g.TogglePause();int count=g.ActiveBullets;float clock=g.SurvivalTime;
   var arrowBefore=g.GetComponentsInChildren<LineRenderer>()[0];Vector3 position=arrowBefore.transform.parent.position;
   yield return new WaitForSeconds(.15f);Assert.AreEqual(count,g.ActiveBullets);Assert.AreEqual(clock,g.SurvivalTime);Assert.AreEqual(position,arrowBefore.transform.parent.position);
   g.TogglePause();g.Run.RecordKill(g.Run.NextUpgradeCost);yield return null;Assert.AreEqual(RunState.Upgrade,g.Run.State);
   clock=g.SurvivalTime;yield return new WaitForSeconds(.15f);Assert.AreEqual(clock,g.SurvivalTime);
   g.SelectUpgrade(0);yield return new WaitForSeconds(.15f);Assert.Greater(g.SurvivalTime,clock);
   g.Run.DamagePlayer(g.Run.Health);clock=g.SurvivalTime;count=g.ActiveBullets;
   yield return new WaitForSeconds(.15f);Assert.AreEqual(RunState.Lost,g.Run.State);Assert.AreEqual(clock,g.SurvivalTime);Assert.AreEqual(count,g.ActiveBullets);
   g.RestartRun();Assert.AreEqual(0,g.ActiveBullets);Assert.AreEqual(0,g.AliveEnemies);
   var root=new GameObject("Arrow lifecycle verification");var shots=new BulletSystem(g.Config,root.transform);shots.Top=Camera.main.orthographicSize;
   var attack=g.Run.CaptureAttack(g.Config.bulletRadius);var enemies=Enemies(g);float cooldown=0;
   for(int i=0;i<600;i++)
   {
    cooldown-=.05f;if(cooldown<=0) { Assert.IsTrue(shots.Fire(g.HeroPosition+Vector2.up*.65f,attack));cooldown=g.Run.Cooldown; }
    shots.Step(.05f,enemies);Assert.Less(shots.Count,12);
   }
   shots.Step(3,enemies);Assert.AreEqual(0,shots.Count);
   foreach(var wake in root.GetComponentsInChildren<LineRenderer>(true)) { Assert.IsFalse(wake.enabled);Assert.AreEqual(0,wake.positionCount); }
   Object.Destroy(root);LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator HealthBarsAndPhysicalBottomExperienceBar()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();var hud=Object.FindFirstObjectByType<GameHud>();
   Assert.IsNull(hud.transform.Find("Safe Area/Element Fire"));Assert.IsNull(hud.transform.Find("Safe Area/Arrow Preview/Shot Cooldown"));
   Assert.IsNull(g.transform.Find("Grass Battlefield"));
   var heroBar=g.transform.Find("Prototype Hero (temporary art)/Hero Health Fill").GetComponent<SpriteRenderer>();
   Assert.Greater(heroBar.color.g,heroBar.color.r);Assert.IsTrue(heroBar.enabled);
   g.StartRun();var enemies=Enemies(g);var soldier=enemies.SpawnAt(EnemyKind.Tank,new SpawnSettings(),Vector2.zero);
   var bar=soldier.transform.Find("Health").GetComponent<SpriteRenderer>();Assert.IsTrue(bar.enabled);Assert.Greater(bar.color.r,bar.color.g);
   float width=bar.transform.localScale.x;soldier.Receive(5,DamageSource.Arrow);Assert.Less(bar.transform.localScale.x,width);
   float heroWidth=heroBar.transform.localScale.x;g.Run.DamagePlayer(10);yield return null;Assert.Less(heroBar.transform.localScale.x,heroWidth);
   var xp=hud.transform.Find("Experience").GetComponent<RectTransform>();
   foreach(var size in new[]{new Vector2Int(540,960),new Vector2Int(720,1280)})
   {
    Screen.SetResolution(size.x,size.y,false);yield return null;yield return null;Canvas.ForceUpdateCanvases();
    var corners=new Vector3[4];xp.GetWorldCorners(corners);
    Assert.AreEqual(0,corners[0].x,1f);Assert.AreEqual(0,corners[0].y,1f);Assert.AreEqual(Screen.width,corners[2].x,1f);
    Assert.Less(corners[2].y,Screen.height*.025f);Assert.IsFalse(xp.GetComponent<UnityEngine.UI.Image>().raycastTarget);
   }
   g.Run.RecordKill(g.Run.NextUpgradeCost);yield return null;g.SelectUpgrade(0);hud.Refresh(true);
   Assert.Less(hud.transform.Find("Experience/Fill").GetComponent<UnityEngine.UI.Image>().fillAmount,.01f);
   g.RestartRun();yield return null;Assert.AreEqual(heroWidth,heroBar.transform.localScale.x,.001f);Assert.AreEqual(0,g.AliveEnemies);LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator SoldierReadabilityAndSpawnSpacing()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();
   var field=typeof(GameController).GetField("enemies",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   var enemies=(EnemySystem)field.GetValue(g);var settings=new SpawnSettings();
   var soldier=enemies.SpawnAt(EnemyKind.Normal,settings,Vector2.zero);
   var bounds=soldier.GetComponent<SpriteRenderer>().bounds;
   float usableWorldHeight=Camera.main.orthographicSize*2*(1f-625f/1920f);
   float ratio=bounds.size.y/usableWorldHeight;
   TestContext.WriteLine("READABILITY normal world height="+bounds.size.y.ToString("F3")+"; usable height="+usableWorldHeight.ToString("F3")+"; visible ratio approx="+(ratio*100).ToString("F2")+"%");
   Assert.That(ratio,Is.InRange(.095f,.105f));
   Assert.LessOrEqual(bounds.extents.y,soldier.Definition.radius+.01f);
   Assert.LessOrEqual(bounds.extents.x,soldier.Definition.radius+.01f);
   for(int i=0;i<20;i++) enemies.Spawn(EnemyKind.Normal,settings);
   var all=Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None);
   Assert.Greater(all.Length,2);Assert.Less(all.Length,12);
   for(int i=0;i<all.Length;i++)
   {
    Assert.LessOrEqual(Mathf.Abs(all[i].Position.x)+all[i].Definition.radius,g.Config.arenaHalfWidth+.001f);
    for(int j=i+1;j<all.Length;j++)
     Assert.GreaterOrEqual(Vector2.Distance(all[i].Position,all[j].Position),all[i].Definition.radius+all[j].Definition.radius+.079f);
   }
   g.RestartRun();LogAssert.NoUnexpectedReceived();
  }


  [UnityTest] public IEnumerator HeroBoundsAutoAttackPauseAndRestart()
  {
   yield return SceneManager.LoadSceneAsync("Main"); yield return null;
   var g=Object.FindFirstObjectByType<GameController>();float y=g.HeroPosition.y;
   Assert.Less(y,0);g.MoveHero(-100);Assert.AreEqual(-g.HeroLimit,g.HeroPosition.x,.001f);
   g.MoveHero(100);Assert.AreEqual(g.HeroLimit,g.HeroPosition.x,.001f);Assert.AreEqual(y,g.HeroPosition.y);
   g.MoveHero(1);yield return new WaitForSeconds(.1f);Assert.AreEqual(1,g.HeroPosition.x,.001f);
   Assert.AreEqual(0,g.ActiveBullets);g.StartRun();
   var pointer=typeof(GameController).GetMethod("HandleHeroPointer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   float x=Screen.width*.5f, sy=Screen.height*.35f;
   g.MoveHero(0);pointer.Invoke(g,new object[]{new Vector2(x,sy),true,true});
   pointer.Invoke(g,new object[]{new Vector2(x+Screen.width,sy+100),false,true});
   Assert.AreEqual(g.HeroLimit,g.HeroPosition.x,.001f);Assert.AreEqual(y,g.HeroPosition.y);
   pointer.Invoke(g,new object[]{new Vector2(x-Screen.width,sy-50),false,false});
   Assert.AreEqual(-g.HeroLimit,g.HeroPosition.x,.001f);
   yield return null;Assert.AreEqual(-g.HeroLimit,g.HeroPosition.x,.001f);
   pointer.Invoke(g,new object[]{new Vector2(x,Screen.height*.9f),true,true});
   pointer.Invoke(g,new object[]{new Vector2(x+100,Screen.height*.9f),false,true});
   Assert.AreEqual(-g.HeroLimit,g.HeroPosition.x,.001f);g.MoveHero(0);
   var enemyField=typeof(GameController).GetField("enemies",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   ((EnemySystem)enemyField.GetValue(g)).SpawnAt(EnemyKind.Normal,new SpawnSettings(),g.HeroPosition+Vector2.up*2);
   float until=Time.time+8;
   while(g.Run.Kills==0 && Time.time<until) yield return null;
   Assert.Greater(g.Run.Kills,0,"Hero must kill without tap input");
   g.TogglePause();float elapsed=g.SurvivalTime;int bullets=g.ActiveBullets;
   yield return new WaitForSeconds(.2f);Assert.AreEqual(elapsed,g.SurvivalTime);Assert.AreEqual(bullets,g.ActiveBullets);
   g.TogglePause();yield return new WaitForSeconds(.1f);Assert.Greater(g.SurvivalTime,elapsed);
   g.RestartRun();Assert.AreEqual(0,g.HeroPosition.x,.001f);Assert.AreEqual(y,g.HeroPosition.y);
   Assert.AreEqual(0,g.ActiveBullets);Assert.AreEqual(0,g.AliveEnemies);LogAssert.NoUnexpectedReceived();
  }
  [UnityTest] public IEnumerator VerticalArrowsRequireHorizontalAlignment()
  {
   yield return SceneManager.LoadSceneAsync("Main");yield return null;
   var g=Object.FindFirstObjectByType<GameController>();g.StartRun();
   var field=typeof(GameController).GetField("enemies",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
   var enemies=(EnemySystem)field.GetValue(g);g.MoveHero(-3);
   typeof(GameController).GetField("spawnIn",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(g,10f);
   var settings=new SpawnSettings();var e=enemies.SpawnAt(EnemyKind.Tank,settings,new Vector2(3,0));
   float hp=e.Health;
   yield return new WaitForSeconds(.2f);
   foreach(var arrow in g.GetComponentsInChildren<SpriteRenderer>())
    if(arrow.name=="Pooled Arrow") { Assert.AreEqual(-3,arrow.transform.position.x,.001f);Assert.Less(Quaternion.Angle(Quaternion.identity,arrow.transform.rotation),.01f); }
   yield return new WaitForSeconds(.9f);Assert.AreEqual(hp,e.Health,"Off-lane enemy must not be auto-corrected into a hit");
   g.MoveHero(3);
   yield return new WaitForSeconds(1.2f);Assert.IsTrue(!e.gameObject.activeSelf||e.Health<hp);
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
   int spawnId=e.SpawnId;float hp=e.Health;g.MoveHero(e.Position.x);g.TryAttackWorld(e.Position);yield return new WaitForSeconds(1.8f);
   Assert.IsTrue(!e.gameObject.activeSelf || e.SpawnId!=spawnId || e.Health<hp);g.RestartRun();LogAssert.NoUnexpectedReceived();
  }

 }
}
