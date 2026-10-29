using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TapDefense.Editor
{
    public static class SceneTools
    {
        // Optional canonical reserialization through Unity APIs; opening the committed scene does not require this.
        [MenuItem("Tap Defense/Rebuild Main Scene and Prefabs")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var config=AssetDatabase.LoadAssetAtPath<GameConfig>(BuildAutomation.ConfigPath);
            if (!config || config.ValidateData()!=null) throw new System.InvalidOperationException("Import valid GameConfig and sprites first.");
            MakeSpritePrefab<EnemyView>("Enemy",config.circleSprite,5);
            MakeSpritePrefab<PulseEffect>("PulseEffect",config.ringSprite,20);
            MakeSpritePrefab<CrystalView>("Crystal",config.diamondSprite,10);
            var number=new GameObject("DamageNumber",typeof(TextMesh),typeof(DamageNumber));
            number.GetComponent<TextMesh>().text="";
            PrefabUtility.SaveAsPrefabAsset(number,"Assets/Prefabs/DamageNumber.prefab"); Object.DestroyImmediate(number);

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>(); camera.orthographic=true;camera.orthographicSize=8.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.043f,.075f);
            camera.allowHDR=false;camera.allowMSAA=false;camera.transform.position=new Vector3(0,0,-10);
            var crystal=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Crystal.prefab")).GetComponent<CrystalView>();
            crystal.name="Central Crystal";
            var feedback=new GameObject("Feedback Pool").AddComponent<FeedbackSystem>();
            var hud=new GameObject("HUD",typeof(RectTransform),typeof(GameHud)).GetComponent<GameHud>();hud.gameObject.layer=5;
            var game=new GameObject("Game Root").AddComponent<GameController>();
            Set(feedback,"effectPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PulseEffect.prefab").GetComponent<PulseEffect>());
            Set(feedback,"numberPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DamageNumber.prefab").GetComponent<DamageNumber>());
            Set(game,"config",config);Set(game,"worldCamera",camera);Set(game,"crystal",crystal);Set(game,"feedback",feedback);Set(game,"hud",hud);
            Set(game,"enemyPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab").GetComponent<EnemyView>());
            EditorSceneManager.SaveScene(scene,BuildAutomation.MainScene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(BuildAutomation.MainScene,true)};
            AssetDatabase.SaveAssets(); Debug.Log("Scene and prefabs rebuilt and wired through Unity APIs.");
        }
        private static void MakeSpritePrefab<T>(string name,Sprite sprite,int order)where T:Component
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.AddComponent<T>();
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=order;
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/Prefabs/"+name+".prefab");Object.DestroyImmediate(go);
        }
        private static void Set(Object target,string field,Object value)
        {
            var serialized=new SerializedObject(target);serialized.FindProperty(field).objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
