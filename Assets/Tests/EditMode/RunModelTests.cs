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




 }
}
