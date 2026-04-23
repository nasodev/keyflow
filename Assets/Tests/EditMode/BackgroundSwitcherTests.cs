using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using KeyFlow;
using KeyFlow.Feedback;

namespace KeyFlow.Tests.EditMode
{
    public class BackgroundSwitcherTests
    {
        private static (BackgroundSwitcher switcher, Image img, Sprite blue, Sprite yellow, GameObject host)
            Build()
        {
            var host = new GameObject("BgHost");
            var imgGO = new GameObject("BgImage");
            imgGO.transform.SetParent(host.transform);
            var img = imgGO.AddComponent<Image>();

            var blueTex = new Texture2D(1, 1);
            blueTex.SetPixel(0, 0, Color.blue); blueTex.Apply();
            var blue = Sprite.Create(blueTex, new Rect(0, 0, 1, 1), Vector2.zero);
            blue.name = "blue";

            var yellowTex = new Texture2D(1, 1);
            yellowTex.SetPixel(0, 0, Color.yellow); yellowTex.Apply();
            var yellow = Sprite.Create(yellowTex, new Rect(0, 0, 1, 1), Vector2.zero);
            yellow.name = "yellow";

            var switcher = host.AddComponent<BackgroundSwitcher>();
            switcher.SetDependenciesForTest(img, blue, yellow);

            return (switcher, img, blue, yellow, host);
        }

        [Test]
        public void Apply_Nayoon_SetsBlueSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Nayoon);
            Assert.AreSame(blue, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_Soyoon_SetsYellowSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Soyoon);
            Assert.AreSame(yellow, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_NayoonThenSoyoon_SwitchesSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Nayoon);
            switcher.Apply(Profile.Soyoon);
            Assert.AreSame(yellow, img.sprite);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Apply_SoyoonThenNayoon_SwitchesSprite()
        {
            var (switcher, img, blue, yellow, host) = Build();
            switcher.Apply(Profile.Soyoon);
            switcher.Apply(Profile.Nayoon);
            Assert.AreSame(blue, img.sprite);
            Object.DestroyImmediate(host);
        }
    }
}
