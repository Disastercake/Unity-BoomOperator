using UnityEngine;
using UnityEditor;

namespace BoomOperatorTool.Examples
{
    [CustomEditor(typeof(BoomOperatorTester))]
    public class BoomOperatorTesterEditor : UnityEditor.Editor
    {
        private int _priority = 0;
        
        public override void OnInspectorGUI()
        {
            var comp = (BoomOperatorTester)target;

            Transform tar = null;

            if (Application.isPlaying)
            {
                BoomOperator.TargetManager.TryGetHighestPriority(out tar);
            }
            
            _priority = EditorGUILayout.IntField("Priority", _priority);
            
            if (GUILayout.Button("Add To Boom At Above Priority"))
            {
                AddToBoom(comp, _priority);
            }
            else if (GUILayout.Button("Add To Boom As Highest Priority"))
            {
                AddToBoom(comp);
            }
            else if (GUILayout.Button("Remove From Boom"))
            {
                RemoveFromBoom(comp);
            }
            else if (GUILayout.Button("Hold Boom Position Here"))
            {
                ToggleHoldPosition(comp, true);
            }
            else if (GUILayout.Button("Release Boom Hold"))
            {
                ToggleHoldPosition(comp, false);
            }

            // Show default inspector property editor
            DrawDefaultInspector ();
        }
        
        private void AddToBoom(BoomOperatorTester comp)
        {
            BoomOperator.TargetManager.Add(comp.transform);
        }
    
        private void AddToBoom(BoomOperatorTester comp, int priority)
        {
            BoomOperator.TargetManager.Add(comp.transform, priority);
        }
    
        private void RemoveFromBoom(BoomOperatorTester comp)
        {
            BoomOperator.TargetManager.Remove(comp.transform);
        }
    
        private void ToggleHoldPosition(BoomOperatorTester comp, bool on)
        {
            if (on)
                BoomOperator.HoldAt(comp.transform.position);
            else
                BoomOperator.Release();
        }
    }
}
