using UnityEngine;

namespace SomethingDownThere
{
    public sealed class WinchRopeView : MonoBehaviour
    {
        [SerializeField] private LineRenderer rope;
        [SerializeField] private Transform hook;
        public bool Configured => rope!=null && hook!=null;
        public void Initialize(TerrainVolume terrain)
        {
            if(terrain.TryGetComponent<ExcavationDaylight>(out var lighting))
            {
                lighting.Register(rope);
                foreach(var renderer in hook.GetComponentsInChildren<Renderer>()) lighting.Register(renderer);
            }
            Hide();
        }
        public void Hide() { if(rope!=null) rope.enabled=false; if(hook!=null) hook.gameObject.SetActive(false); }
        public void Draw(Transform terrain,Vector3[] path,float distance,int anchorIndex,Vector3? attachment=null)
        {
            if(path==null || path.Length<3) { Hide(); return; }
            var point=ExtractionSnapshot.Point(path,distance,out int segment);
            rope.enabled=true; hook.gameObject.SetActive(true);
            hook.position=attachment ?? terrain.TransformPoint(point);
            if(segment>=anchorIndex)
            {
                rope.positionCount=2; rope.SetPosition(0,terrain.TransformPoint(path[anchorIndex])); rope.SetPosition(1,hook.position);
            }
            else
            {
                rope.positionCount=anchorIndex-segment+1;
                int n=0; for(int i=anchorIndex;i>segment;i--) rope.SetPosition(n++,terrain.TransformPoint(path[i]));
                rope.SetPosition(n,hook.position);
            }
        }
    }
}
