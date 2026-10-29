using NUnit.Framework;

namespace TapDefense.Tests
{
    public sealed class PresentationSettingsTests
    {
        [Test] public void DefaultsProvideSeparateValidBudgets()
        {
            var settings = new PresentationSettings();
            Assert.IsTrue(settings.IsValid);
            Assert.AreSame(settings.web, settings.Select(true));
            Assert.AreSame(settings.native, settings.Select(false));
            Assert.AreNotSame(settings.native, settings.web);
            Assert.Less(settings.web.effectPoolLimit, settings.native.effectPoolLimit);
        }

        [Test] public void ChangingWebPresentationDoesNotMutateNativeBudget()
        {
            var settings = new PresentationSettings();
            settings.web.targetFrameRate = 45;
            settings.web.screenShakeMultiplier = 0;
            Assert.AreEqual(60, settings.native.targetFrameRate);
            Assert.AreEqual(1, settings.native.screenShakeMultiplier);
        }

        [Test] public void InvalidBudgetsAreRejected()
        {
            var settings = new PresentationSettings();
            settings.web.damageNumberPoolLimit = 0;
            Assert.IsFalse(settings.IsValid);
            settings.web.damageNumberPoolLimit = 1;
            settings.native.screenShakeMultiplier = float.NaN;
            Assert.IsFalse(settings.IsValid);
            settings.native.screenShakeMultiplier = 1;
            settings.web.targetFrameRate = 15;
            Assert.IsFalse(settings.IsValid);
            settings.web = null;
            Assert.IsFalse(settings.IsValid);
        }
    }
}
