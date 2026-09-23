using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class SalvageWinchSetup
    {
        private const string Folder="Assets/Content/Salvage";
        [MenuItem("Tools/Something Down There/Configure Unique Recovery")]
        public static void Configure()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying || scene.path!=MainGameSceneBuilder.ScenePath) throw new InvalidOperationException("Open MainGame outside Play Mode.");
            RetroComputerSetup.EnsureFolder(Folder);
            foreach(string name in new[]{"WinchFixture","RecoveryPad","ExhibitStand","Hook"})
            {
                string path=Folder+"/Models/"+name+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null) throw new InvalidOperationException("Missing original Blender recovery art.");
                importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
                importer.bakeAxisConversion=true; importer.isReadable=true; importer.SaveAndReimport();
            }
            var settings=AssetDatabase.LoadAssetAtPath<SalvageWinchSettings>(Folder+"/WinchSettings.asset");
            if(settings==null) { settings=ScriptableObject.CreateInstance<SalvageWinchSettings>(); AssetDatabase.CreateAsset(settings,Folder+"/WinchSettings.asset"); }
            var steel=Material("Steel",new Color(.17f,.20f,.21f),.55f);
            var paint=Material("Worksite yellow",new Color(.72f,.43f,.07f),.15f);
            var cable=Material("Cable",new Color(.22f,.23f,.24f),.3f);
            var root=scene.GetRootGameObjects().Single(o=>o.name=="MainGameRoot").transform;
            var terrain=root.GetComponentInChildren<TerrainVolume>(); var field=root.GetComponentInChildren<DiscoveryField>(); var player=root.GetComponentInChildren<FpsPlayer>();
            var station=Child(root.Find("Surface"),"SalvageWinch");
            var winch=Get<SalvageWinch>(station.gameObject);
            var fixture=Visual(station,"WinchFixture",paint); fixture.localPosition=new Vector3(4,0,-13); fixture.localScale=Vector3.one*1.5f;
            var pad=Visual(station,"RecoveryPad",steel); pad.localPosition=new Vector3(6.4f,0,-13);
            var stand=Visual(station,"ExhibitStand",steel); stand.localPosition=new Vector3(8.4f,0,-10);
            Box(pad,new Vector3(0,.12f,0),new Vector3(2.4f,.24f,1.7f));
            Box(stand,new Vector3(0,.52f,0),new Vector3(1.55f,1.04f,1.1f));
            Box(fixture,new Vector3(0,.5f,0),new Vector3(1.5f,1,1.05f));
            var lift=Child(station,"LiftAnchor"); lift.localPosition=new Vector3(4,3.12f,-12.7f);
            var landing=Child(station,"PadAnchor"); landing.localPosition=new Vector3(6.4f,.24f,-13);
            var exhibit=Child(stand,"DisplayAnchor"); exhibit.localPosition=new Vector3(0,1.06f,0);
            exhibit.localRotation=Quaternion.Euler(0,180,0);
            var display=Get<UniqueDisplayStand>(stand.gameObject);
            Set(display,"discoveries",field); Set(display,"displayAnchor",exhibit);
            var viewRoot=Child(station,"Rope"); var view=Get<WinchRopeView>(viewRoot.gameObject);
            var line=Get<LineRenderer>(viewRoot.gameObject); line.useWorldSpace=true; line.sharedMaterial=cable;
            line.startWidth=line.endWidth=settings.RopeRadius*2; line.numCapVertices=3; line.numCornerVertices=2;
            line.generateLightingData=true; line.positionCount=0; line.enabled=false;
            var hook=Visual(viewRoot,"Hook",steel); hook.gameObject.SetActive(false);
            Set(view,"rope",line); Set(view,"hook",hook);
            Set(winch,"terrain",terrain); Set(winch,"discoveries",field); Set(winch,"player",player); Set(winch,"settings",settings);
            Set(winch,"liftAnchor",lift); Set(winch,"padAnchor",landing); Set(winch,"ropeView",view); Set(winch,"displayStand",display);
            Set(player,"winch",winch);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Unique recovery configured in MainGame.");
        }
        private static Transform Child(Transform parent,string name)
        {
            var child=parent.Find(name); if(child!=null) return child;
            child=new GameObject(name).transform; child.SetParent(parent,false); return child;
        }
        private static Transform Visual(Transform parent,string name,Material material)
        {
            var anchor=Child(parent,name); var old=anchor.Find("Visual");
            if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Models/"+name+".fbx"),anchor);
            model.name="Visual";
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=material;
            return anchor;
        }
        private static Material Material(string name,Color color,float metallic)
        {
            string path=Folder+"/"+name+".mat"; var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null) { result=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result,path); }
            result.SetColor("_BaseColor",color); result.SetFloat("_Metallic",metallic); result.SetFloat("_Smoothness",.3f); EditorUtility.SetDirty(result); return result;
        }
        private static T Get<T>(GameObject go) where T:Component => go.TryGetComponent<T>(out var component)?component:go.AddComponent<T>();
        private static void Box(Transform t,Vector3 center,Vector3 size) { var b=Get<BoxCollider>(t.gameObject); b.center=center; b.size=size; }
        private static void Set(UnityEngine.Object owner,string name,UnityEngine.Object value)
        { using var data=new SerializedObject(owner); data.FindProperty(name).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
