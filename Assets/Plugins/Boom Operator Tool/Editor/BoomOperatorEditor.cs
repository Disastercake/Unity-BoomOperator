using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BoomOperatorTool.Editor
{
 
    [CustomEditor(typeof(BoomOperator))]
    public class BoomOperatorEditor : UnityEditor.Editor
    {
        private readonly List<int> _priorities = new List<int>();
        private readonly List<Transform> _targets = new List<Transform>();

        private Transform _newTarget;
        
        public override void OnInspectorGUI()
        {
            var boomOperator = (BoomOperator)target;
            
            // Show default inspector property editor
            DrawDefaultInspector ();
            
            // Add a new target to the list.
            EditorGUILayout.BeginHorizontal();
            
            if (_newTarget == null) GUI.enabled = false;
            if (GUILayout.Button("Add Target", GUILayout.Width(100)))
            {
                AddTarget(_newTarget);
                _newTarget = null;
            }
            
            if (Application.isPlaying)
                GUI.enabled = true;
            
            _newTarget = (Transform)EditorGUILayout.ObjectField("", _newTarget, typeof(Transform), allowSceneObjects:true);
            
            EditorGUILayout.EndHorizontal();
            
            // Exit early
            if (!Application.isPlaying) return;
            
            
            // ==== BELOW IS PLAY MODE CODE ==== //
            
            
            _priorities.Clear();
            _targets.Clear();
            BoomOperator.TargetManager.GetAll(_priorities, _targets);

            int count = _priorities.Count;

            if (!Application.isPlaying)
                GUI.enabled = false;
            
            // Cycle through all existing targets.  Add buttons so they can be increased, decreased, or removed.
            for (int i = count-1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("↑", GUILayout.Width(25))) ChangePriority(_targets[i], _priorities[i] + 1);
                if (GUILayout.Button("↓", GUILayout.Width(25))) ChangePriority(_targets[i], _priorities[i] - 1);
                if (GUILayout.Button("-", GUILayout.Width(25))) Remove(_targets[i]);
                
                GUI.enabled = false;
                
                EditorGUILayout.IntField(string.Empty, _priorities[i], GUILayout.Width(25));
                EditorGUILayout.ObjectField(string.Empty, _targets[i], typeof(Transform), true);
                
                GUI.enabled = true;
                
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddTarget(Transform tar)
        {
            BoomOperator.TargetManager.Add(tar);
        }

        private void ChangePriority(Transform tar, int priority)
        {
            BoomOperator.TargetManager.Add(tar, priority);
        }

        private void Remove(Transform tar)
        {
            BoomOperator.TargetManager.Remove(tar);
        }
    }
}
