// Copyright (c) 2021 Disastercake. MIT License (https://opensource.org/licenses/MIT).

using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace BoomOperatorTool
{
    /// <summary>
    /// A singleton AudioListener for convenient and simple following and placement commands.
    /// • Use BoomOperator HoldAt() and Release() to handle holding at specific world positions.
    /// • Use BoomOperator.TargetManager to handle follow-targets and their priorities.
    /// • Priorities are fallbacks, so removing or destroying Targets will cause the next priority to be followed.
    /// • If there are no targets, then the BoomOperator just stays in its last position until further instruction is given.
    /// • Contains a lazy initializer, so do not need to instantiate in a scene, but can if required.
    /// • Do not set a BoomOperator's game object to static; it needs to reposition itself throughout the app's life.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioListener))]
    public sealed class BoomOperator : MonoBehaviour
    {
        #region Singleton

        /// <summary>
        /// Cheap way to check if the BoomOperator exists.
        /// The alternative is checking if _instance == null,
        /// but UnityEngine.Object == null is inefficient due to complex backend checks.
        /// </summary>
        private static bool _instantiated = false;

        /// <summary>
        /// The direct reference to the singleton instance.
        /// Note: Use the singleton property to utilize its lazy initializer functionality.
        /// </summary>
        private static BoomOperator _instance = null;

        /// <summary>
        /// Gets the singleton instance.  Will create one if it doesn't exist.
        ///     Note: Most required commands can be executed by using the BoomOperator static methods.
        /// </summary>
        private static BoomOperator singleton
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return null;
#endif

                // If not instantiated, let's create one!
                if (!_instantiated)
                {
                    var go = new GameObject(nameof(BoomOperator));
                    go.AddComponent<AudioListener>();
                    _instance = go.AddComponent<BoomOperator>();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Used during debug logging to check if the GameObject is empty or not.
        ///     An "empty" GameObject could have 3 components: Transform, BoomOperator, AudioSource
        /// </summary>
        private static readonly HashSet<Type> EXPECTED_COMPONENTS =
            new HashSet<Type> {typeof(Transform), typeof(AudioListener), typeof(BoomOperator)};

        /// <summary>
        /// Destroys the current BoomOperator singleton instance.
        /// </summary>
        public static void Destroy()
        {
            if (_instantiated)
            {
                _instantiated = false;
                Destroy(_instance, false);
                _instance = null;
            }
        }

        /// <summary>
        /// Destroys the parameter object and posts a debug log if requested.
        /// </summary>
        private static void Destroy(BoomOperator boomOperator, bool postWarning)
        {
            if (boomOperator == null) return;

            int childCount = 0;
            int compCount = 0;
            bool destroyGameObject = false;
            var gameObject = boomOperator.gameObject;
            var transform = boomOperator.transform;

            // To prevent leaking game objects, this will destroy this GameObject,
            //      but only if it doesn't have children or any other components.

            // Count any unexpected Components and Children game objects.
            var components = gameObject.GetComponents(typeof(Component));
            for (int i = 0; i < components.Length; i++)
                if (!EXPECTED_COMPONENTS.Contains(components[i].GetType()))
                    compCount++;
            childCount = transform.childCount;

            // Only destroy the entire game object if there is nothing unexpected.
            destroyGameObject = compCount <= 0 && childCount <= 0;

            // Post a detailed warning.
            if (postWarning && Debug.isDebugBuild)
            {
                var debugString =
                    new StringBuilder(
                        $"There was already an instance of \"{nameof(BoomOperator)}\" on the game object \"{gameObject.name}\". ");

                if (destroyGameObject)
                    debugString.Append(
                        $"Destroying the entire \"{gameObject.name}\" game object because there were no other components or children on it.");
                else
                {
                    debugString.Append(
                        $"Only destroying the \"{nameof(BoomOperator)}\" component, because there was ");

                    if (compCount > 0)
                    {
                        debugString.Append($" {compCount} components ");
                        if (childCount > 0) debugString.Append("and");
                    }

                    if (childCount > 0)
                    {
                        debugString.Append($" {childCount} children ");
                    }

                    debugString.Append($" on the \"{gameObject.name}\" game object.");
                }

                Debug.LogWarning(debugString.ToString());
            }

            // Finally, destroy what is needed.
            if (destroyGameObject)
                Destroy(gameObject);
            else
                Destroy(boomOperator);
        }

        #endregion Singleton

        #region Static

        /// <summary>
        /// A data structure of targets the BoomOperator should consider in order of priority.
        /// If a target is removed, directly or by being destroyed, then this automatically falls down to the next highest target priority.
        /// Note 1: If the same priority is used for a target, the latest addition is considered the higher priority over the older targets.
        /// Note 2: Added an object more than once will simply update its priority, not add duplicate elements.
        /// </summary>
        public static BoomOperatorTargetManager TargetManager => singleton._targetManager;

        /// <summary>
        /// If true, then the BoomOperator was instructed to HoldAt() its current position until instructed to Release().
        /// </summary>
        public static bool HoldingPosition
        {
            get => singleton._holdingPosition;
            set => singleton._holdingPosition = value;
        }

        /// <summary>
        /// Instructs the BoomOperator to hold at the specified world position until instructed to Release().
        /// </summary>
        public static void HoldAt(Vector3 worldPos)
        {
            HoldingPosition = true;
            singleton.TransformCached.position = worldPos;
        }

        /// <summary>
        /// Releases the current hold instruction and returns to normal target-following behaviour.
        /// </summary>
        public static void Release()
        {
            HoldingPosition = false;
        }

        #endregion Static

        #region Local

        // Local fields to direct code through the singleton for lazy initialization.
        //      Being local, this means once the singleton is destroyed,
        //      all targets and holding commands are reset.
        private readonly BoomOperatorTargetManager _targetManager = new BoomOperatorTargetManager();
        private bool _holdingPosition = false;

        /// <summary>
        /// The cached Transform of this game object.
        /// </summary>
        public Transform TransformCached { get; private set; } = null;

        /// <summary>
        /// The cached GameObject of this object.
        /// </summary>
        public GameObject GameObjectCached { get; private set; } = null;

        /// <summary>
        /// Allows OdinInspector to show the target in the inspector for debugging.
        /// Note: This variable is safe to remove if OdinInspector will not be used in the project.
        /// </summary>
        private Transform _currentTargetInspectorOnly
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return null;
#endif
                Transform t = null;
                TargetManager?.TryGetHighestPriority(out t);
                return t;
            }
        }

        private void Awake()
        {
            // If we already have a singleton instance,
            //      this will commence a safe-ish destroy routine
            //      and log a detailed warning.
            if (_instantiated)
            {
                BoomOperator.Destroy(this, true);
                return;
            }

            // ==============================================================
            // There's no singleton instance yet!  Let's set this bad boy up.

            // Cache the singleton reference.
            _instance = this;
            _instantiated = true;

            // Cache the constant components to prevent inefficient native code calls.
            TransformCached = transform;
            GameObjectCached = gameObject;

            // The BoomOperator should persist through the entire app's life.
            DontDestroyOnLoad(GameObjectCached);
        }

        private void OnDestroy()
        {
            // This can be ran when an extra is instantiated,
            //      so make sure this is the actual singleton instance
            //      before releasing those caches.
            if (_instance == this)
            {
                _instantiated = false;
                _instance = null;
            }
        }

        private void LateUpdate()
        {
            // Move on late update to make sure we keep up if the target moved during Update().

            // If holding a position, then don't do anything.
            if (HoldingPosition) return;

            // If there is a target, the TargetManager will return its world position.
            if (TargetManager.TryGetHighestPriority(out Vector3 pos))
                TransformCached.position = pos;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Depending on your preference, you can setup a gizmo with an icon, a wireframe sphere, etc.
            // Check out these resources for more info:
            // • https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDrawGizmos.html
            // • https://docs.unity3d.com/ScriptReference/Gizmos.DrawIcon.html
            // • https://docs.unity3d.com/ScriptReference/Gizmos.DrawWireSphere.html

            const string gizmo_file_name = "boomoperator_gizmo.png";

            Gizmos.DrawIcon(transform.position, gizmo_file_name, true);
            //Gizmos.DrawWireSphere(TransformCached.position, 0.5f);
        }
#endif

        #endregion Local

        #region Data Models

        /// <summary>
        /// A data structure that handles management of the BoomOperatorTarget objects and their priorities.
        /// </summary>
        public sealed class BoomOperatorTargetManager
        {
            #region Private Fields

            /// <summary>
            /// The main list of objects, manually organized by their priority during the Add() methods.
            /// </summary>
            private readonly List<TargetObject> _targets = new List<TargetObject>();

            #endregion Private Fields

            #region Data Models

            /// <summary>
            /// Caches both a target and its priority.
            /// </summary>
            private sealed class TargetObject
            {
                /// <summary>
                /// The target that will be followed when its priority is the highest.
                /// </summary>
                public Transform Target = null;

                /// <summary>
                /// Higher numbers indicate higher priorities.
                /// </summary>
                public int Priority = 0;

                /// <summary>
                /// Instruct this object to return to its pool so it can be reused.
                /// </summary>
                public void Pool()
                {
                    Target = null;
                    Priority = 0;
                    _unusedTargetObject.Push(this);
                }

                #region Static Pool

                /// <summary>
                /// Cached list of objects to prevent garbage collection.
                /// </summary>
                private static readonly Stack<TargetObject> _unusedTargetObject = new Stack<TargetObject>();

                /// <summary>
                /// Get an unused object from the pool, or create a new one if the pool is empty.
                /// </summary>
                public static TargetObject Get()
                {
                    return _unusedTargetObject.Count > 0 ? _unusedTargetObject.Pop() : new TargetObject();
                }

                #endregion
            }

            #endregion Data Models

            #region Public Getters

            /// <summary>
            /// The number of targets that have been added.
            /// </summary>
            public int Count => _targets.Count;

            /// <summary>
            /// Appends a copy of targets and their priorities to the parameter lists.
            /// The index of each list is associated with the other.
            /// Note 1: Does not clear the lists.
            /// Note 2: Ignores null lists, so if only one list is needed, just send that parm
            /// </summary>
            public void GetAll(List<int> priorityList, List<Transform> targetList)
            {
                for (int i = 0; i < _targets.Count; i++)
                {
                    var t = _targets[i];
                    priorityList?.Add(t.Priority);
                    targetList?.Add(t.Target);
                }
            }

            /// <summary>
            /// Determines the highest priority value.  Returns 0 if there are no targets.
            /// </summary>
            /// <returns>True if a priority was found.  False if there are no targets.</returns>
            public bool TryGetHighestPriority(out int priority)
            {
                priority = 0;

                // Reverse loop through targets, removing NULLs when found.
                //      Return the first not NULL value, since the list was organized during Add() methods.
                for (int i = _targets.Count - 1; i >= 0; i--)
                {
                    var t = _targets[i];

                    // If null, let's remove it.
                    if (t == null)
                    {
                        _targets.RemoveAt(i);
                        continue;
                    }
                    
                    // Priority found.
                    priority = t.Priority;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Determines the highest priority Target's world position.
            /// If NULLs are found, they are handled silently here and removed from this data structure.
            /// </summary>
            /// <param name="worldPos">The highest priority Target's world position.</param>
            /// <returns>True if there was a target, false if not.</returns>
            public bool TryGetHighestPriority(out Vector3 worldPos)
            {
                // Reverse loop through targets, removing NULLs when found.
                //      Return the first not NULL value, since the list was organized during Add() methods.
                for (int i = _targets.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        // Get the world position.
                        worldPos = _targets[i].Target.position;
                        return true;
                    }
                    catch
                    {
                        // This will throw an error if the target Transform was destroyed.
                        //      Remove it if that's the case.
                        if (_targets[i] != null)
                            _targets[i].Pool();
                        _targets.RemoveAt(i);
                    }
                }

                worldPos = Vector3.zero;
                return false;
            }

            /// <summary>
            /// Returns the highest priority Target.
            /// If NULLs are found, they are handled silently here.
            /// </summary>
            /// <param name="target">The highest priority Target.</param>
            /// <returns>True if there was a target, false if not.</returns>
            public bool TryGetHighestPriority(out Transform target)
            {
                target = null;

                // Reverse loop through targets, removing NULLs when found.
                //      Return the first not NULL value, since the list was organized during Add() methods.
                for (int i = _targets.Count - 1; i >= 0; i--)
                {
                    var o = _targets[i];

                    if (o == null)
                    {
                        _targets.RemoveAt(i);
                    }
                    else if (o.Target == null)
                    {
                        o.Pool();
                        _targets.RemoveAt(i);
                    }
                    else
                    {
                        target = o.Target;
                        return true;
                    }
                }

                return false;
            }

            #endregion

            #region Private Methods

            /// <summary>
            /// Get the index of a target.  Returns -1 if it doesn't exist.
            /// </summary>
            private int IndexOf(Transform t)
            {
                for (int i = 0; i < _targets.Count; i++)
                    if (t == _targets[i].Target)
                        return i;

                return -1;
            }

            #endregion Private Methods

            #region Public Methods

            /// <summary>
            /// Caches the target and sets its priority to one higher than the current highest priority.
            /// Note: If the target already exists in the list, then its priority is adjusted (not added again).
            /// </summary>
            /// <param name="target">The target to add.</param>
            public void Add(Transform target)
            {
                // Check if already the highest.  If already highest, then return early.
                if (_targets.Count > 0 &&
                    _targets[_targets.Count - 1] != null &&
                    _targets[_targets.Count - 1].Target == target)
                    return;
                
                TryGetHighestPriority(out int priority);
                Add(target, priority + 1);
            }

            /// <summary>
            /// Add a target, or update a current target's priority.
            /// </summary>
            /// <param name="target">The target</param>
            /// <param name="priority">Set to the specified priority.</param>
            public void Add(Transform target, int priority)
            {
                TargetObject o = null;

                var existingIndex = IndexOf(target);
                if (existingIndex >= 0)
                {
                    o = _targets[existingIndex];
                    _targets.RemoveAt(existingIndex);
                }
                else
                {
                    o = TargetObject.Get();
                }

                o.Target = target;
                o.Priority = priority;

                var count = _targets.Count;

                // If there are no targets, simply add this one and exit early.
                if (count <= 0)
                {
                    _targets.Add(o);
                    return;
                }

                // Priorities are in ascending order, so go backwards through the list to find the correct index.
                for (int i = count - 1; i >= 0; i--)
                {
                    if (priority >= _targets[i].Priority)
                    {
                        _targets.Insert(index: i + 1, item: o); // Index at the index right before.
                        return;
                    }
                }

                // If nothing found, then this is the lowest priority and should be added to the end.
                _targets.Insert(0, o);
            }

            /// <summary>
            /// Removes the target.
            /// </summary>
            /// <returns>True if the target was found and removed.  False if it was not found.</returns>
            public bool Remove(Transform remove)
            {
                if (remove == null) return false;

                var index = IndexOf(remove);

                if (index >= 0)
                {
                    _targets.RemoveAt(index);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            /// <summary>
            /// Clear all targets and priorities.
            /// </summary>
            public void Clear()
            {
                for (int i = 0; i < _targets.Count; i++)
                    _targets[i].Pool();

                _targets.Clear();
            }

            /// <summary>
            /// Sanitizes the data structure by removing all NULL references.
            ///     Note: This class silently handles NULL references when they are found during TryGetHighestPriorityPosition().
            ///             You can use this method if, for some reason, it is more performant to Sanitize at a certain moment.
            /// </summary>
            public void Sanitize()
            {
                for (int i = _targets.Count - 1; i >= 0; i--)
                {
                    var target = _targets[i];

                    if (target == null)
                    {
                        _targets.RemoveAt(i);
                    }
                    else if (target.Target == null)
                    {
                        target.Pool();
                        _targets.RemoveAt(i);
                    }
                }
            }

            #endregion Public Methods
        }

        #endregion Data Models
    }
}
