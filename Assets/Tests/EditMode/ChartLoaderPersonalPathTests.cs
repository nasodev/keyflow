using NUnit.Framework;
using KeyFlow.Charts;

namespace KeyFlow.Tests.EditMode
{
    public class ChartLoaderPersonalPathTests
    {
        [Test]
        public void ResolveChartPath_PublicSong_UsesChartsRoot()
        {
            var path = ChartLoader.ResolveChartPath("foo", isPersonal: false);
            string expected = "charts" + System.IO.Path.DirectorySeparatorChar + "foo.kfchart";
            Assert.That(path, Does.EndWith(expected));
            Assert.That(path, Does.Not.Contain("personal"));
        }

        [Test]
        public void ResolveChartPath_PersonalSong_UsesPersonalSubdir()
        {
            var path = ChartLoader.ResolveChartPath("foo", isPersonal: true);
            string expected = "charts" + System.IO.Path.DirectorySeparatorChar
                            + "personal" + System.IO.Path.DirectorySeparatorChar
                            + "foo.kfchart";
            Assert.That(path, Does.EndWith(expected));
        }
    }
}
