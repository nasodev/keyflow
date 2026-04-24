using NUnit.Framework;
using KeyFlow;

namespace KeyFlow.Tests.EditMode
{
    public class ProfileTests
    {
        [SetUp]
        public void ResetProfile()
        {
            // Static field persists across tests in same AppDomain.
            SessionProfile.Current = Profile.Nayoon;
        }

        [Test]
        public void SessionProfile_DefaultsToNayoon()
        {
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }

        [Test]
        public void SessionProfile_SetSoyoon_PersistsWithinSession()
        {
            SessionProfile.Current = Profile.Soyoon;
            Assert.AreEqual(Profile.Soyoon, SessionProfile.Current);
        }

        [Test]
        public void SessionProfile_RoundTrip_NayoonSoyoonNayoon()
        {
            SessionProfile.Current = Profile.Soyoon;
            SessionProfile.Current = Profile.Nayoon;
            Assert.AreEqual(Profile.Nayoon, SessionProfile.Current);
        }
    }
}
