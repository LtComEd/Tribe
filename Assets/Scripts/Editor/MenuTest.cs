// NO #if guard here — plain editor script in the Editor folder
// The Editor folder itself tells Unity this is editor-only
using UnityEngine;
using UnityEditor;

public static class MenuTest
{
    [MenuItem("TribeGame/0. Verify Editor Assembly")]
    public static void Verify()
    {
        Debug.Log("[MenuTest] Editor assembly OK.");
        EditorUtility.DisplayDialog("OK", "Editor scripts are working.", "OK");
    }
}
