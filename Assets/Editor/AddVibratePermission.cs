using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace KeyFlow.Editor
{
    /// <summary>
    /// Injects android.permission.VIBRATE into the generated launcher manifest
    /// after Unity exports the Gradle project but before Gradle builds the APK.
    /// This avoids replacing Unity's auto-generated launcher activity block,
    /// which the old Assets/Plugins/Android/AndroidManifest.xml override did.
    /// </summary>
    public class AddVibratePermission : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string projectPath)
        {
            string manifestPath = Path.Combine(
                projectPath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[KeyFlow] AddVibratePermission: manifest not found at {manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var ns = "http://schemas.android.com/apk/res/android";
            var manifest = doc.DocumentElement;

            foreach (XmlNode child in manifest.ChildNodes)
            {
                if (child.Name == "uses-permission"
                    && child.Attributes?["android:name"]?.Value == "android.permission.VIBRATE")
                {
                    // Already present
                    return;
                }
            }

            var permission = doc.CreateElement("uses-permission");
            var attr = doc.CreateAttribute("android", "name", ns);
            attr.Value = "android.permission.VIBRATE";
            permission.Attributes.Append(attr);
            manifest.PrependChild(permission);

            doc.Save(manifestPath);
            Debug.Log("[KeyFlow] AddVibratePermission: injected android.permission.VIBRATE");
        }
    }
}
