using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creates Assets/Scenes/OrbitSnake.unity: an otherwise empty scene holding the ORBIT SNAKE manager. Everything else the
// game shows is generated in OrbitSnake.Awake. Idempotent, so the runners can call it before opening the scene.
public static class OrbitSceneBuilder
{
    public const string ScenePath="Assets/Scenes/OrbitSnake.unity";
    [MenuItem("Orbit/Create OrbitSnake scene")]
    public static void Create()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("ORBIT SNAKE // GAME MANAGER").AddComponent<OrbitSnake>();
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("[ORBIT] scene written to "+ScenePath);
    }
    public static void Ensure(){ if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)Create(); }
}
