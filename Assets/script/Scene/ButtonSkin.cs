using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 런타임에 만드는 버튼들이 같은 배경 그림을 쓰도록 모아 둔 곳.
// 인스펙터에서 비워 두면 에디터에서 기본 그림(settingPanel)을 찾아 채워 준다.
public static class ButtonSkin
{
    public const string DefaultBackgroundName = "settingPanel";

    // 빌드에서는 AssetDatabase가 없으므로, 씬/프리팹에 직렬화된 값이 그대로 쓰인다.
    public static void ResolveBackground(ref Sprite sprite)
    {
        if (sprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets($"{DefaultBackgroundName} t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != DefaultBackgroundName)
            {
                continue;
            }

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                return;
            }
        }
#endif
    }
}