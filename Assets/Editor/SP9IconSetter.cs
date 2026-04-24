using UnityEditor;
using UnityEngine;

namespace KeyFlow.Editor
{
    // One-shot utility to assign Android launcher icon from Assets/Textures/icon.png
    // to all Legacy density buckets. Safe to re-run. SP9 implementation.
    public static class SP9IconSetter
    {
        [MenuItem("KeyFlow/Apply Android Icon (SP9)")]
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/icon.png");
            if (icon == null)
            {
                Debug.LogError("[SP9IconSetter] Missing Assets/Textures/icon.png. Aborting.");
                return;
            }

            // PlayerSettings stores one icon slot per density bucket for the Unknown kind
            // (Legacy). Fill every slot with the same texture.
            var iconKind = IconKind.Any;
            int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android, iconKind);
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons, iconKind);
            Debug.Log($"[SP9IconSetter] Assigned {icon.name} to {icons.Length} Android Legacy icon slots.");

            // Save Player Settings to disk.
            AssetDatabase.SaveAssets();
        }
    }
}
