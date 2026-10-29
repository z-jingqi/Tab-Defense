using NUnit.Framework;
using TapDefense.Core;
namespace TapDefense.Tests
{
 public sealed class RunModelTests
 {
  [Test] public void EndlessRunDoesNotCompleteAutomatically()
  {var r=new RunModel(new RunParameters());Assert.IsTrue(r.StartRun());Assert.IsFalse(r.StartRun());Assert.IsFalse(r.SettleExperience());Assert.AreEqual(RunState.Combat,r.State);}
  [Test] public void ExperiencePausesAndAllChoicesResumeCombat()
  {var r=new RunModel(new RunParameters());r.StartRun();r.RecordKill(35);Assert.IsTrue(r.SettleExperience());Assert.AreEqual(2,r.PendingUpgrades);Assert.AreEqual(5,r.ExperienceBank);r.ApplyUpgrade(UpgradeKind.Damage);Assert.AreEqual(RunState.Upgrade,r.State);r.ApplyUpgrade(UpgradeKind.Chain);Assert.AreEqual(RunState.Combat,r.State);}
  [Test] public void EscapeCanEndRunAndCannotBeHealedAfterDeath()
  {var r=new RunModel(new RunParameters());r.StartRun();r.DamagePlayer(999);Assert.AreEqual(RunState.Lost,r.State);Assert.IsFalse(r.ApplyUpgrade(UpgradeKind.Heal));}
  [Test] public void ThreeChoicesAreUnique()
  {var r=new RunModel(new RunParameters());r.StartRun();r.RecordKill(12);r.SettleExperience();var c=r.GetChoices(new System.Random(1));Assert.AreEqual(3,c.Length);CollectionAssert.AllItemsAreUnique(c);CollectionAssert.DoesNotContain(c,UpgradeKind.Heal);}

  private static void Grant(RunModel run, UpgradeKind kind)
  { run.RecordKill(run.NextUpgradeCost); Assert.IsTrue(run.SettleExperience()); Assert.IsTrue(run.ApplyUpgrade(kind)); }
  [Test] public void ElementsStartLockedAndUnlockOnlyWithAnUpgrade()
  {
   var r=new RunModel(new RunParameters()); Assert.AreEqual(0,r.Elements.UnlockedCount);
   Assert.IsFalse(r.Elements.Select(ElementKind.Fire)); Assert.IsFalse(r.ApplyUpgrade(UpgradeKind.UnlockFire));
   r.StartRun(); Grant(r,UpgradeKind.UnlockFire);
   Assert.AreEqual(1,r.Elements.UnlockedCount); Assert.AreEqual(ElementKind.None,r.Elements.Selected);
   Assert.AreEqual(3,r.Elements.Get(ElementKind.Fire).Count);
   r.RecordKill(r.NextUpgradeCost);r.SettleExperience();
   for(int seed=0;seed<50;seed++) CollectionAssert.DoesNotContain(r.GetChoices(new System.Random(seed)),UpgradeKind.UnlockFire);
   Assert.IsFalse(r.ApplyUpgrade(UpgradeKind.UnlockFire)); Assert.AreEqual(1,r.PendingUpgrades);
  }
  [Test] public void ChargesRecoverSequentiallyAndDepletionFallsBackToArrows()
  {
   var e=new ElementInventory(ElementDefinition.Defaults());e.Unlock(ElementKind.Fire);e.Select(ElementKind.Fire);
   e.ConsumeSelected();e.ConsumeSelected();e.ConsumeSelected();Assert.AreEqual(ElementKind.None,e.Selected);
   Assert.IsFalse(e.Select(ElementKind.Fire));e.Step(5.9f);Assert.AreEqual(0,e.Get(ElementKind.Fire).Count);
   e.Step(.2f);Assert.AreEqual(1,e.Get(ElementKind.Fire).Count);e.Step(12);Assert.AreEqual(3,e.Get(ElementKind.Fire).Count);
   Assert.AreEqual(0,e.Get(ElementKind.Fire).RechargeLeft);
   e.Select(ElementKind.Fire);e.Select(ElementKind.Fire);Assert.AreEqual(ElementKind.None,e.Selected);
  }
  [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
  public void ElementGroupAlwaysCenters(int count)
  {
   float sum=0;for(int i=0;i<count;i++)sum+=ElementBarLayout.Offset(i,count,188);
   Assert.AreEqual(0,sum,.001f);
   Assert.AreEqual(-ElementBarLayout.Offset(0,count,188),ElementBarLayout.Offset(count-1,count,188),.001f);
  }
  [Test] public void BurstSnapshotSurvivesElementSwitchAndUpgradesAreCapped()
  {
   var r=new RunModel(new RunParameters());r.StartRun();Grant(r,UpgradeKind.UnlockFire);r.Elements.Select(ElementKind.Fire);
   Grant(r,UpgradeKind.MultiShot);Grant(r,UpgradeKind.Burst);Grant(r,UpgradeKind.Pierce);
   var attack=r.CaptureAttack(.13f);r.Elements.Select(ElementKind.None);
   Assert.AreEqual(ElementKind.Fire,attack.element);Assert.AreEqual(2,attack.arrows);Assert.AreEqual(2,attack.bursts);Assert.AreEqual(1,attack.pierce);
   Grant(r,UpgradeKind.MultiShot);r.RecordKill(r.NextUpgradeCost);r.SettleExperience();
   Assert.IsFalse(r.ApplyUpgrade(UpgradeKind.MultiShot));CollectionAssert.DoesNotContain(r.GetChoices(new System.Random(3)),UpgradeKind.MultiShot);
  }
 }
}
