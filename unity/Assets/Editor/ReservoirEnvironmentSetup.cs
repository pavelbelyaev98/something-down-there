using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SomethingDownThere.Editor
{
    /// <summary>Authors ordinary scene objects. Nothing is generated at runtime or stored in a save.</summary>
    public static class ReservoirEnvironmentSetup
    {
        public const string RootName = "Reservoir Surroundings";
        private const string Vendor = "Assets/BK/PureNature_Mountains/Prefabs/";
        private const string Materials = "Assets/Content/Nature/";
        private static readonly Dictionary<string, Material> MaterialCopies = new Dictionary<string, Material>();
        private static System.Random random;

        [MenuItem("Tools/Something Down There/Compose Reservoir Surroundings")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode before composing its surroundings.");
            ConfigureCollisionImports();
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            var old = root.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var surroundings = Group(RootName, root);
            var banks = Group("Exposed Banks", surroundings);
            var gorge = Group("Dry Rockfall Gorge", surroundings);
            var forest = Group("Forest Above Waterline", surroundings);
            var mountains = Group("Distant Peaks", surroundings);
            var edging = Group("Excavation Edge Stones", surroundings);
            var details = Group("Rim Debris", surroundings);
            random = new System.Random(5719);
            MaterialCopies.Clear();

            // Each side has a different silhouette. These are composed clusters, not a repeated rock fence.
            Bank("Cliff2", banks, new Vector3(-25,-1,-18), new Vector3(22,12,19), 18);
            Bank("Cliff1", banks, new Vector3(-27,-1,-2), new Vector3(24,15,25), -12);
            Bank("Cliff3", banks, new Vector3(-22,-1,14), new Vector3(14,12,17), 23);
            Bank("Cliff1", banks, new Vector3(-23,5,29), new Vector3(30,15,26), 57);
            Bank("Cliff2", banks, new Vector3(25,-1,-19), new Vector3(20,14,20), -30);
            Bank("Cliff3", banks, new Vector3(23,-1,-3), new Vector3(16,12,17), 32);
            Bank("Cliff1", banks, new Vector3(27,-1,12), new Vector3(22,14,25), -8);
            Bank("Cliff3", gorge, new Vector3(23,1,31), new Vector3(17,18,24), 12);
            Bank("Cliff2", banks, new Vector3(-12,-1,27), new Vector3(29,8.5f,22), -11);
            Bank("Cliff3", gorge, new Vector3(1,0,32), new Vector3(14,12,23), -20);
            Bank("Cliff3", gorge, new Vector3(20,4,51), new Vector3(20,17,30), 30);
            Bank("Cliff2", gorge, new Vector3(-1,3,52), new Vector3(21,15,30), -35);
            // Broad back rock buttress reserves a future dam location, without building a dam.
            Bank("Cliff1", banks, new Vector3(-9,-1,-27), new Vector3(34,15,24), 10);
            Bank("Cliff2", banks, new Vector3(15,-1,-28), new Vector3(27,17,23), -15);
            Bank("Cliff1", banks, new Vector3(-44,3,8), new Vector3(35,17,50), 5);
            Bank("Cliff2", banks, new Vector3(44,4,4), new Vector3(34,20,54), -12);
            Bank("Cliff1", banks, new Vector3(-15,4,-47), new Vector3(68,18,30), 14);
            // Buried rock skirts close the view through talus and the inaccessible gorge floor.
            Bank("Cliff1", banks, new Vector3(-43,-12,0), new Vector3(54,12,120), 0);
            Bank("Cliff1", banks, new Vector3(43,-12,0), new Vector3(54,12,120), 0);
            Bank("Cliff2", banks, new Vector3(0,-12,55), new Vector3(135,12,78), 0);
            Bank("Cliff2", banks, new Vector3(0,-12,-55), new Vector3(135,12,78), 0);

            // Low, overlapping talus conceals the old square safety faces at player height.
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < 11; i++)
            {
                float along = -16 + i*3.2f + Range(-.55f,.55f);
                Vector3 position = AlongSide(side,along,17.2f+Range(-.3f,.5f));
                position.y = -.35f;
                var go = Rock("Stone" + (i%3+2) + "b", banks, position,
                    new Vector3(Range(3.6f,5.2f),Range(2.3f,3.8f),Range(3.2f,4.5f)),Range(0,360),true);
                AlignInnerFace(go,side,15.55f);
                go.name = "Bank toe " + side + " " + i;
            }
            // A glimpse into a dry gorge is blocked by visibly fallen stone, not an empty doorway.
            for (int i = 0; i < 7; i++)
                Rock("Stone3b", gorge, new Vector3(9+Range(-3,3),-.2f,19+i*2.1f),
                    new Vector3(Range(3,5),Range(1.8f,3.8f),Range(2.8f,4.5f)),Range(0,360),true);

            Physics.SyncTransforms();
            PlantForest(forest, banks, gorge);
            Peak(mountains,"Mountain1",new Vector3(-70,-5,165),new Vector3(290,125,200),-15);
            Peak(mountains,"Mountain2",new Vector3(65,0,210),new Vector3(310,140,190),15);
            Peak(mountains,"Mountain1",new Vector3(195,-10,105),new Vector3(240,145,220),-75);
            Peak(mountains,"Mountain2",new Vector3(-200,-10,40),new Vector3(260,150,200),85);
            Peak(mountains,"Mountain1",new Vector3(160,-10,-170),new Vector3(280,165,180),-40);
            Peak(mountains,"Mountain2",new Vector3(-70,-12,-220),new Vector3(300,160,200),170);

            // Low edge stones stay entirely on the permanent rim; the southern service bay is open.
            for (int side = 0; side < 4; side++)
            for (int i = 0; i < 11; i++)
            {
                float along = -10.7f + i*2.14f + Range(-.22f,.22f);
                if (side == 3 && Mathf.Abs(along)<5.3f) continue;
                Vector3 p = AlongSide(side,along,12.7f); p.y=-.12f;
                Rock("Stone"+(i%3+1)+"b",edging,p,new Vector3(Range(.65f,1.05f),Range(.35f,.62f),.7f),Range(-20,20)+side*90,true);
            }
            for (int i = 0; i < 14; i++)
            {
                int side=i%3; var p=AlongSide(side,Range(-11,11),14.8f); p.y=.025f;
                var debris=Place("Plants/Branchs",details,p,new Vector3(1.1f,.25f,.7f),Range(0,360));
                ConfigureRenderers(debris,false,false);
            }
            ConcealSafetyBoundary(root);
            var camera=root.GetComponentInChildren<Camera>();
            Undo.RecordObject(camera,"Include inexpensive distant mountains");
            camera.farClipPlane=650;
            SunPresentationSetup.Configure();
            // Keep publisher instances connected, including our component and material overrides.
            foreach(var component in surroundings.GetComponentsInChildren<Component>(true))
                if(component!=null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Reservoir surroundings composed: " + forest.childCount + " trees/shrubs, " + mountains.childCount + " distant mountains.");
        }

        private static Transform Group(string name,Transform parent)
        {
            var go=new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go,"Compose reservoir surroundings");
            go.transform.SetParent(parent,false);
            return go.transform;
        }

        private static void ConfigureCollisionImports()
        {
            foreach(string model in new[]{"Cliff1","Cliff2","Cliff3","Stone1","Stone2","Stone3","Stone4"})
            {
                var importer=AssetImporter.GetAtPath("Assets/BK/PureNature_Mountains/Models/Rocks/"+model+".fbx") as ModelImporter;
                if(importer==null) throw new InvalidOperationException("Missing rock model: "+model);
                if(importer.HasPreBakeCollisionMesh(false)) continue;
                importer.SetPreBakeCollisionMesh(false,true);
                importer.SaveAndReimport();
            }
        }

        private static void ConcealSafetyBoundary(Transform root)
        {
            string[] sides={"West","East","North","South"};
            for(int side=0;side<4;side++)
            {
                bool vertical=side<2;
                // Only extend the permanent support outward. The excavation edge and depth stay fixed.
                var rim=root.Find("Surface/"+sides[side]+" rim");
                Undo.RecordObject(rim,"Support reservoir bank approach");
                rim.position=AlongSide(side,0,16.5f)+Vector3.down*.5f;
                rim.localScale=vertical ? new Vector3(9,1,24) : new Vector3(42,1,9);
                string name=side==2 ? "North shoreline" : sides[side];
                var boundary=root.Find("Perimeter/"+name);
                var airspace=root.Find("Perimeter/"+name+" airspace");
                var renderer=boundary.GetComponent<Renderer>();
                var airCollider=airspace.GetComponent<BoxCollider>();
                Undo.RecordObjects(new Object[]{boundary,airspace,renderer,airCollider},"Hide safety boundary inside banks");
                boundary.position=AlongSide(side,0,20.5f)+Vector3.up*.6f;
                boundary.localScale=vertical ? new Vector3(1,1.2f,42) : new Vector3(42,1.2f,1);
                airspace.position=AlongSide(side,0,20.5f)+Vector3.up*65.2f;
                airCollider.size=vertical ? new Vector3(1,128,42) : new Vector3(42,128,1);
                renderer.enabled=false;
            }
        }

        private static GameObject Place(string prefab,Transform parent,Vector3 basePosition,Vector3 size,float yaw)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+prefab+".prefab");
            if(asset==null) throw new InvalidOperationException("Missing approved prefab: "+prefab);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            Undo.RegisterCreatedObjectUndo(go,"Place reservoir asset");
            go.transform.localRotation=Quaternion.identity;
            go.transform.localScale=Vector3.one;
            Bounds bounds=GeometryBounds(go);
            go.transform.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            go.transform.rotation=Quaternion.Euler(0,yaw,0);
            bounds=GeometryBounds(go);
            go.transform.position+=new Vector3(basePosition.x-bounds.center.x,basePosition.y-bounds.min.y,basePosition.z-bounds.center.z);
            return go;
        }

        private static Bounds GeometryBounds(GameObject go)
        {
            var group=go.GetComponent<LODGroup>();
            var renderers=group!=null ? group.GetLODs()[0].renderers : go.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static GameObject Rock(string prefab,Transform parent,Vector3 position,Vector3 size,float yaw,bool collision)
        {
            var go=Place("Rocks/"+prefab,parent,position,size,yaw);
            ConfigureRenderers(go,true,true);
            ReplaceRockMaterials(go,false);
            foreach(var collider in go.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(collider);
            if(collision)
            {
                var lod=go.GetComponent<LODGroup>();
                var mesh=lod!=null ? lod.GetLODs()[Mathf.Min(2,lod.lodCount-1)].renderers[0].GetComponent<MeshFilter>()
                    : go.GetComponentInChildren<MeshFilter>();
                var collider=Undo.AddComponent<MeshCollider>(mesh.gameObject);
                collider.sharedMesh=mesh.sharedMesh;
                Undo.AddComponent<PermanentTerrainBoundary>(go);
            }
            return go;
        }

        private static void Bank(string prefab,Transform parent,Vector3 position,Vector3 size,float yaw)
        {
            var go=Rock(prefab,parent,position,size,yaw,true);
            int side=Mathf.Abs(position.x)>Mathf.Abs(position.z) ? (position.x<0 ? 0 : 1) : (position.z>0 ? 2 : 3);
            var b=GeometryBounds(go);
            if(b.min.x<14.9f && b.max.x>-14.9f && b.min.z<14.9f && b.max.z>-14.9f)
                AlignInnerFace(go,side,14.9f);
        }

        private static void AlignInnerFace(GameObject go,int side,float distance)
        {
            var b=GeometryBounds(go); var p=go.transform.position;
            if(side==0) p.x-=distance+b.max.x;
            else if(side==1) p.x+=distance-b.min.x;
            else if(side==2) p.z+=distance-b.min.z;
            else p.z-=distance+b.max.z;
            go.transform.position=p;
        }

        private static void Peak(Transform parent,string prefab,Vector3 position,Vector3 size,float yaw)
        {
            var go=Place("Mountains/"+prefab,parent,position,size,yaw);
            ReplaceRockMaterials(go,true);
            ConfigureRenderers(go,false,true);
        }

        private static void ReplaceRockMaterials(GameObject go,bool mountain)
        {
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>RockMaterial(source,mountain)).ToArray();
        }

        private static Material RockMaterial(Material source,bool mountain)
        {
            string key="Reservoir_"+source.name.Replace(" ","_")+(mountain ? "_Distant" : "");
            if(MaterialCopies.TryGetValue(key,out var cached)) return cached;
            var path=Materials+key+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Something Down There/Reservoir Rock")); AssetDatabase.CreateAsset(material,path); }
            material.shader=Shader.Find("Something Down There/Reservoir Rock");
            material.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
            if(source.HasProperty("_BumpMap")) material.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
            material.SetColor("_BaseColor",mountain ? new Color(.8f,.9f,1f) : new Color(.92f,.98f,1f));
            material.SetFloat("_BumpScale",mountain ? .3f : .65f);
            material.SetFloat("_Waterline",5.2f);
            material.SetFloat("_StainStrength",mountain ? 0 : .85f);
            material.SetFloat("_HazeStart",mountain ? 55 : 65);
            material.SetFloat("_HazeEnd",mountain ? 380 : 360);
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);
            MaterialCopies.Add(key,material);
            return material;
        }

        private static void ConfigureRenderers(GameObject go,bool shadows,bool opaque)
        {
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode=shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows=true;
                renderer.lightProbeUsage=LightProbeUsage.Off;
                renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.Camera;
                if(opaque) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
                else renderer.sharedMaterials=renderer.sharedMaterials.Select(FoliageMaterial).ToArray();
            }
        }

        private static Material FoliageMaterial(Material source)
        {
            string key="Reservoir_"+source.name.Replace(" ","_")+"_Foliage";
            if(MaterialCopies.TryGetValue(key,out var cached)) return cached;
            string path=Materials+key+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(source); AssetDatabase.CreateAsset(material,path); }
            else material.CopyPropertiesFromMaterial(source);
            material.enableInstancing=true;
            EditorUtility.SetDirty(material);
            MaterialCopies.Add(key,material);
            return material;
        }

        private static void PlantForest(Transform parent,Transform banks,Transform gorge)
        {
            var centers=new[]{new Vector2(-24,-16),new Vector2(-27,1),new Vector2(-24,20),
                new Vector2(-36,22),new Vector2(-42,-10),new Vector2(24,-18),new Vector2(25,0),
                new Vector2(30,20),new Vector2(44,-8),new Vector2(45,18),new Vector2(-12,30),
                new Vector2(-22,-27),new Vector2(0,-30),new Vector2(18,-30),new Vector2(2,52),new Vector2(21,48)};
            var occupied=new List<Vector3>();
            foreach(var center in centers)
            for(int i=0;i<12;i++)
            {
                float x=center.x+Range(-8,8),z=center.y+Range(-8,8);
                if(!Physics.Raycast(new Vector3(x,80,z),Vector3.down,out var hit,80)) continue;
                if(!hit.transform.IsChildOf(banks) && !hit.transform.IsChildOf(gorge)) continue;
                if(hit.point.y<6.2f || hit.normal.y<.5f || occupied.Any(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(x,z))<2.4f)) continue;
                occupied.Add(hit.point);
                string prefab=i%5==0 ? "Pine3" : "Fir"+(i%3+1);
                float height=Range(8,14);
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"Trees/"+prefab+".prefab");
                var b=GeometryBounds(asset); var size=b.size*(height/b.size.y);
                var tree=Place("Trees/"+prefab,parent,hit.point-Vector3.up*.15f,size,Range(0,360));
                foreach(var collider in tree.GetComponentsInChildren<Collider>()) Undo.DestroyObjectImmediate(collider);
                ConfigureRenderers(tree,new Vector2(x,z).magnitude<32,false);
                var lod=tree.GetComponent<LODGroup>();
                if(lod!=null)
                {
                    var levels=lod.GetLODs();
                    for(int level=0;level<levels.Length;level++)
                        levels[level].screenRelativeTransitionHeight=new[]{.55f,.27f,.07f,.004f}[Mathf.Min(level,3)];
                    lod.SetLODs(levels);
                }
                if(i%4==0)
                {
                    var bush=Place("Trees/Bush1",parent,hit.point,new Vector3(2.5f,1.8f,2.5f),Range(0,360));
                    ConfigureRenderers(bush,false,false);
                }
            }
        }

        private static Vector3 AlongSide(int side,float along,float distance)
        {
            switch(side)
            {
                case 0: return new Vector3(-distance,0,along);
                case 1: return new Vector3(distance,0,along);
                case 2: return new Vector3(along,0,distance);
                default: return new Vector3(along,0,-distance);
            }
        }
        private static float Range(float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());
    }
}
